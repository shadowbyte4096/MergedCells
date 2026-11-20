using System.Text.Json;
using System.Text.Json.Serialization;

namespace MergedCells.Core;

/// <summary>
/// The player's save in memory: each level's best moves, keyed by level ID with the level's fingerprint, and the
/// settings. Stars are never stored; they come from the best. Main thread only.
/// </summary>
public sealed class SaveGame
{
    public const int CurrentVersion = 5;

    private readonly Dictionary<int, LevelRecord> levels = new();

    private readonly HashSet<Guid> sharedLevels = new();

    private readonly Dictionary<ConsentKind, Consent> consents = new();

    private Guid? player;

    private InfiniteChoice? infinite;

    private readonly HashSet<Guid> pendingDeletions = new();

    private string? sessionToken;

    private long recordsSent;

    public Appearance Appearance { get; set; } = Appearance.System;

    // The last choice played in infinite mode, or null before any (since version 3). Only what Parse accepts, so a write
    // can never make the next load a broken save
    public InfiniteChoice? Infinite
    {
        get => infinite;
        set
        {
            if (value is { } choice)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan(choice.Difficulty, 1, nameof(value));
                ArgumentOutOfRangeException.ThrowIfGreaterThan(choice.Difficulty, InfiniteConfig.DifficultyCount, nameof(value));
                ArgumentOutOfRangeException.ThrowIfLessThan(choice.Side, 2, nameof(value));
            }
            infinite = value;
        }
    }

    /// <summary>
    /// The random ID on every play record and score, made at the player's first yes to any kind of data and dropped by
    /// "Delete my data" (since version 4; version 3's playtester ID).
    /// </summary>
    public Guid? Player
    {
        get => player;
        set
        {
            if (value == Guid.Empty)
            {
                throw new ArgumentException("The player ID can't be all zeros.", nameof(value));
            }
            player = value;
        }
    }

    /// <summary>
    /// The server session's refresh token, or null (since version 5). Set when the game signs in or renews, and dropped by
    /// "Delete my data"; while it's held, data may have been sent under the current player ID.
    /// </summary>
    public string? SessionToken
    {
        get => sessionToken;
        set
        {
            if (value is { Length: 0 })
            {
                throw new ArgumentException("The session token can't be empty.", nameof(value));
            }
            sessionToken = value;
        }
    }

    /// <summary>Player IDs the server is still to delete everything under (since version 5).</summary>
    public IReadOnlySet<Guid> PendingDeletions => pendingDeletions;

    public bool AddPendingDeletion(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A player ID can't be all zeros.", nameof(id));
        }
        return pendingDeletions.Add(id);
    }

    public bool RemovePendingDeletion(Guid id) => pendingDeletions.Remove(id);

    /// <summary>
    /// How far into the play records file, in bytes, every line has been uploaded or will never be eligible (since
    /// version 5). "Delete my data" puts it back to 0 with the file.
    /// </summary>
    public long RecordsSent
    {
        get => recordsSent;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            recordsSent = value;
        }
    }

    /// <summary>The player's answer about one kind of data, or null when they've never been asked (since version 4).</summary>
    public Consent? ConsentTo(ConsentKind kind) => consents.GetValueOrDefault(kind);

    public void SetConsent(ConsentKind kind, Consent? answer)
    {
        if (answer is null)
        {
            consents.Remove(kind);
        }
        else
        {
            consents[kind] = answer;
        }
    }

    /// <summary>
    /// The player's own custom levels that have been shared from this device. An import of one stays the player's own, so
    /// sharing a level and importing it back never pays coins (chunk 27).
    /// </summary>
    public IReadOnlySet<Guid> SharedLevels => sharedLevels;

    /// <summary>Records a level shared from this device. Returns whether it's new, so the caller writes the save only then.</summary>
    public bool RecordShared(Guid id) => sharedLevels.Add(id);

    // Every saved record, including ones from levels that have since changed or that this build doesn't have
    public int LevelCount => levels.Count;

    /// <summary>The best moves for the level as it is now, or null when it hasn't been finished, when the saved best is
    /// from a layout or par that has since changed, or when the saved best is below par.</summary>
    public int? BestMoves(Level level)
    {
        ArgumentNullException.ThrowIfNull(level);
        if (!levels.TryGetValue(level.Id, out var record) || record.Layout != LevelFingerprint.Of(level))
        {
            return null;
        }
        // Only a hand-edited file can hold a matching best below par, because par is in the fingerprint
        return record.BestMoves < level.Par ? null : record.BestMoves;
    }

    public int? Stars(Level level, StarsConfig config) =>
        BestMoves(level) is { } best ? Core.Stars.Earned(best, level.Par, config) : null;

    /// <summary>Records a finish, keeping it as the best when it beats the saved best or there's no usable one.
    /// Throws <see cref="ArgumentOutOfRangeException"/>, recording nothing, when moves are below par.</summary>
    public LevelFinish RecordFinish(Level level, int moves, StarsConfig config)
    {
        ArgumentNullException.ThrowIfNull(level);
        var stars = Core.Stars.Earned(moves, level.Par, config);
        var previousBest = BestMoves(level);
        int? previousStars = previousBest is { } best ? Core.Stars.Earned(best, level.Par, config) : null;
        var finish = new LevelFinish(moves, stars, previousBest, previousStars);
        if (finish.IsNewBest)
        {
            levels[level.Id] = new LevelRecord(moves, LevelFingerprint.Of(level));
        }
        return finish;
    }

    /// <summary>Forgets every level's record, including those for levels this build doesn't have. The settings, the shared
    /// levels, the answers, the player ID and the server section stay.</summary>
    public void ClearProgress() => levels.Clear();

    /// <summary>
    /// Reads a save file's text. Throws <see cref="NewerSaveException"/> when its version is newer than this build's,
    /// checked before anything else, and <see cref="JsonException"/>, naming the problem, for anything else wrong with it.
    /// </summary>
    public static SaveGame Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        // Some Windows editors save UTF-8 with a byte order mark, which System.Text.Json rejects
        var text = json.TrimStart(ByteOrderMark);
        var keys = CheckVersion(text);

        var file = JsonSerializer.Deserialize(text, SaveJsonContext.Default.SaveJson)
            ?? throw new JsonException("The save is null.");
        JsonKeys.RejectDuplicates(text, "save");
        // .NET 8's System.Text.Json enforces required keys but not non-null values, so each is checked by name
        if (file.Settings is null)
        {
            throw new JsonException("The save's \"settings\" is null.");
        }
        if (file.Levels is null)
        {
            throw new JsonException("The save's \"levels\" is null.");
        }

        var save = new SaveGame { Appearance = file.Settings.Appearance };
        foreach (var (id, record) in file.Levels)
        {
            if (id < 1)
            {
                throw new JsonException($"The save's level ID {id} must be at least 1.");
            }
            if (record is null)
            {
                throw new JsonException($"The save's level {id} is null.");
            }
            if (record.BestMoves < 1)
            {
                throw new JsonException($"The save's level {id} has \"bestMoves\" {record.BestMoves}; it must be at least 1.");
            }
            if (!IsFingerprint(record.Layout))
            {
                throw new JsonException($"The save's level {id} has a \"layout\" that isn't 16 lowercase hexadecimal digits.");
            }
            save.levels[id] = new LevelRecord(record.BestMoves, record.Layout);
        }
        keys.Require(keys.Root, "sharedLevels", 2);
        foreach (var shared in file.SharedLevels ?? (keys.Number >= 2 ? throw new JsonException("The save's \"sharedLevels\" is null.") : []))
        {
            // Lowercase only, as the custom level files name them
            if (shared is null || !Guid.TryParseExact(shared, "D", out var id) || id == Guid.Empty || shared != id.ToString("D"))
            {
                throw new JsonException($"The save's shared level \"{shared}\" isn't a lowercase GUID, or is all zeros.");
            }
            if (!save.sharedLevels.Add(id))
            {
                throw new JsonException($"The save lists shared level {shared} twice.");
            }
        }
        keys.Require(keys.Settings, "recordPlayData", 3, last: 3);
        keys.Require(keys.Root, "infinite", 3);
        keys.Require(keys.Root, "playtester", 3, last: 3);
        keys.Require(keys.Root, "consents", 4);
        keys.Require(keys.Root, "player", 4);
        keys.Require(keys.Root, "server", 5);
        if (keys.Number >= 3)
        {
            save.Infinite = ReadChoice(file.Infinite);
        }
        if (keys.Number == 3)
        {
            // Chunk 22's switch: on was a yes to its wording, version 0, which has since changed
            if (file.Settings.RecordPlayData ?? throw new JsonException("The save's \"recordPlayData\" is null."))
            {
                save.consents[ConsentKind.PlayData] = new Consent(true, 0);
            }
            save.player = ReadGuid(file.Playtester, "playtester");
        }
        if (keys.Number >= 4)
        {
            var answers = file.Consents ?? throw new JsonException("The save's \"consents\" is null.");
            save.SetConsent(ConsentKind.PlayData, ReadConsent(answers.PlayData, "playData"));
            save.SetConsent(ConsentKind.Leaderboards, ReadConsent(answers.Leaderboards, "leaderboards"));
            save.player = ReadGuid(file.Player, "player");
        }
        if (keys.Number >= 5)
        {
            ReadServer(save, file.Server ?? throw new JsonException("The save's \"server\" is null."));
        }
        return save;
    }

    private static void ReadServer(SaveGame save, ServerJson server)
    {
        if (server.Session is { Length: 0 })
        {
            throw new JsonException("The save's \"session\" is empty.");
        }
        save.sessionToken = server.Session;
        foreach (var pending in server.PendingDeletions ?? throw new JsonException("The save's \"pendingDeletions\" is null."))
        {
            if (pending is null || !Guid.TryParseExact(pending, "D", out var id) || id == Guid.Empty || pending != id.ToString("D"))
            {
                throw new JsonException($"The save's pending deletion \"{pending}\" isn't a lowercase GUID, or is all zeros.");
            }
            if (!save.pendingDeletions.Add(id))
            {
                throw new JsonException($"The save lists pending deletion {pending} twice.");
            }
        }
        if (server.RecordsSent < 0)
        {
            throw new JsonException($"The save's \"recordsSent\" is {server.RecordsSent}; it must be 0 or more.");
        }
        save.recordsSent = server.RecordsSent;
    }

    private static Consent? ReadConsent(ConsentJson? answer, string key)
    {
        if (answer is null)
        {
            return null;
        }
        // Exact words, as the appearance is read
        var yes = answer.Answer switch
        {
            "yes" => true,
            "no" => false,
            _ => throw new JsonException($"The save's \"{key}\" answer must be \"yes\" or \"no\"."),
        };
        if (answer.Wording < 0)
        {
            throw new JsonException($"The save's \"{key}\" wording is {answer.Wording}; it must be 0 or more.");
        }
        return new Consent(yes, answer.Wording);
    }

    private static InfiniteChoice? ReadChoice(InfiniteChoiceJson? choice)
    {
        if (choice is null)
        {
            return null;
        }
        // Only what any config could hold: InfiniteChoice.Fitted makes a saved choice fit the config of the day
        if (choice.Difficulty is < 1 or > InfiniteConfig.DifficultyCount)
        {
            throw new JsonException($"The save's infinite \"difficulty\" is {choice.Difficulty}; it must be from 1 to {InfiniteConfig.DifficultyCount}.");
        }
        if (choice.Side < 2)
        {
            throw new JsonException($"The save's infinite \"side\" is {choice.Side}; it must be at least 2.");
        }
        return new InfiniteChoice(choice.Difficulty, choice.Side, choice.Walls, choice.Traps, choice.Teleporters, choice.Irregular);
    }

    private static Guid? ReadGuid(string? text, string key)
    {
        if (text is null)
        {
            return null;
        }
        if (!Guid.TryParseExact(text, "D", out var id) || id == Guid.Empty || text != id.ToString("D"))
        {
            throw new JsonException($"The save's \"{key}\" \"{text}\" isn't a lowercase GUID, or is all zeros.");
        }
        return id;
    }

    // Indented, with levels in ID order, so a save reads well by eye
    public string ToJson()
    {
        var file = new SaveJson
        {
            Version = CurrentVersion,
            Settings = new SettingsJson { Appearance = Appearance },
            Levels = levels.OrderBy(pair => pair.Key).ToDictionary(
                pair => pair.Key,
                pair => new LevelRecordJson { BestMoves = pair.Value.BestMoves, Layout = pair.Value.Layout }),
            SharedLevels = sharedLevels.Select(id => id.ToString("D")).Order(StringComparer.Ordinal).ToArray(),
            Infinite = Infinite is { } choice
                ? new InfiniteChoiceJson
                {
                    Difficulty = choice.Difficulty,
                    Side = choice.Side,
                    Walls = choice.Walls,
                    Traps = choice.Traps,
                    Teleporters = choice.Teleporters,
                    Irregular = choice.Irregular,
                }
                : null,
            Consents = new ConsentsJson
            {
                PlayData = WriteConsent(ConsentTo(ConsentKind.PlayData)),
                Leaderboards = WriteConsent(ConsentTo(ConsentKind.Leaderboards)),
            },
            Player = player?.ToString("D"),
            Server = new ServerJson
            {
                Session = sessionToken,
                PendingDeletions = pendingDeletions.Select(id => id.ToString("D")).Order(StringComparer.Ordinal).ToArray(),
                RecordsSent = recordsSent,
            },
        };
        return JsonSerializer.Serialize(file, SaveJsonContext.Default.SaveJson) + "\n";
    }

    private static ConsentJson? WriteConsent(Consent? answer) =>
        answer is null ? null : new ConsentJson { Answer = answer.Yes ? "yes" : "no", Wording = answer.Wording };

    private const char ByteOrderMark = (char)0xFEFF;

    // The version decides how the rest is read, so a newer file with keys this build doesn't know is never called broken
    private static SaveKeys CheckVersion(string text)
    {
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("The save isn't a JSON object.");
        }
        if (!root.TryGetProperty("version", out var version))
        {
            throw new JsonException("The save has no \"version\".");
        }
        if (version.ValueKind != JsonValueKind.Number)
        {
            throw new JsonException("The save's \"version\" isn't a whole number.");
        }
        if (!version.TryGetInt32(out var number))
        {
            // No build writes 2.0 or a number too big for an int, so whatever did is newer than this one
            var whole = Math.Floor(version.GetDouble());
            if (whole > CurrentVersion)
            {
                throw new NewerSaveException((int)Math.Min(whole, int.MaxValue));
            }
            throw new JsonException("The save's \"version\" isn't a whole number.");
        }
        if (number > CurrentVersion)
        {
            throw new NewerSaveException(number);
        }
        if (number < 1)
        {
            throw new JsonException($"The save's \"version\" is {number}; it must be at least 1.");
        }
        // Read here because the strict reader can't tell a missing key from a null one
        var settings = root.TryGetProperty("settings", out var element) && element.ValueKind == JsonValueKind.Object
            ? element.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal)
            : [];
        return new SaveKeys(number, root.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal), settings);
    }

    private static bool IsFingerprint(string? layout) =>
        layout is { Length: 16 } && layout.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    private sealed record LevelRecord(int BestMoves, string Layout);

    // The keys at the save's root and in its settings, so each section checks its own key against the versions that have it
    private sealed record SaveKeys(int Number, IReadOnlySet<string> Root, IReadOnlySet<string> Settings)
    {
        // last: the final version with the key, for one a later version dropped
        public void Require(IReadOnlySet<string> keys, string key, int since, int? last = null)
        {
            var expected = Number >= since && (last is null || Number <= last);
            if (keys.Contains(key) != expected)
            {
                throw new JsonException(expected
                    ? $"The save has no \"{key}\"."
                    : $"The save is version {Number}, which has no \"{key}\".");
            }
        }
    }
}

internal sealed record SaveJson
{
    public required int Version { get; init; }

    public required SettingsJson Settings { get; init; }

    public required Dictionary<int, LevelRecordJson> Levels { get; init; }

    // Since version 2
    public string[]? SharedLevels { get; init; }

    // Since version 3; null before any choice
    public InfiniteChoiceJson? Infinite { get; init; }

    // Version 3 only; read into the player ID
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Playtester { get; init; }

    // Since version 4
    public ConsentsJson? Consents { get; init; }

    // Since version 4; null until the player's first yes, and after "Delete my data"
    public string? Player { get; init; }

    // Since version 5
    public ServerJson? Server { get; init; }
}

internal sealed record ServerJson
{
    public required string? Session { get; init; }

    public required string[]? PendingDeletions { get; init; }

    public required long RecordsSent { get; init; }
}

internal sealed record ConsentsJson
{
    public required ConsentJson? PlayData { get; init; }

    public required ConsentJson? Leaderboards { get; init; }
}

internal sealed record ConsentJson
{
    public required string Answer { get; init; }

    public required int Wording { get; init; }
}

internal sealed record InfiniteChoiceJson
{
    public required int Difficulty { get; init; }

    public required int Side { get; init; }

    public required bool Walls { get; init; }

    public required bool Traps { get; init; }

    public required bool Teleporters { get; init; }

    public required bool Irregular { get; init; }
}

internal sealed record SettingsJson
{
    [JsonConverter(typeof(AppearanceJsonConverter))]
    public required Appearance Appearance { get; init; }

    // Version 3 only; read into the play-data answer
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? RecordPlayData { get; init; }
}

internal sealed record LevelRecordJson
{
    public required int BestMoves { get; init; }

    public required string Layout { get; init; }
}

// Exactly "system", "light" or "dark". JsonStringEnumConverter falls back to Enum.TryParse, which on .NET 8 accepts
// "2", "5" and "light, dark" as enum values; an undefined one then makes ToJson throw at the next save, mid-finish.
internal sealed class AppearanceJsonConverter : JsonConverter<Appearance>
{
    public override Appearance Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        (reader.TokenType == JsonTokenType.String ? reader.GetString() : null) switch
        {
            "system" => Appearance.System,
            "light" => Appearance.Light,
            "dark" => Appearance.Dark,
            _ => throw new JsonException("The save's \"appearance\" must be \"system\", \"light\" or \"dark\"."),
        };

    public override void Write(Utf8JsonWriter writer, Appearance value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value switch
        {
            Appearance.System => "system",
            Appearance.Light => "light",
            Appearance.Dark => "dark",
            _ => throw new JsonException($"Appearance {(int)value} can't be saved."),
        });
}

// Source-generated, so parsing needs no reflection and survives trimming
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    WriteIndented = true)]
[JsonSerializable(typeof(SaveJson))]
internal partial class SaveJsonContext : JsonSerializerContext;
