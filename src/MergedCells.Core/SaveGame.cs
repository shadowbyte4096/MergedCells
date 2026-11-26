using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MergedCells.Core;

/// <summary>
/// The player's save in memory: each level's best moves, keyed by level ID with the level's fingerprint, and the
/// settings. Stars are never stored; they come from the best. Main thread only.
/// </summary>
public sealed class SaveGame
{
    public const int CurrentVersion = 9;

    private readonly Dictionary<int, LevelRecord> levels = new();

    private readonly HashSet<Guid> sharedLevels = new();

    private readonly Dictionary<ConsentKind, Consent> consents = new();

    private Guid? player;

    private InfiniteChoice? infinite;

    private readonly HashSet<Guid> pendingDeletions = new();

    private string? sessionToken;

    private long recordsSent;

    private readonly Dictionary<Guid, DateTimeOffset> published = new();

    private readonly Dictionary<InfiniteChoice, InfiniteBest> infiniteBests = new();

    private long coins;

    private readonly Dictionary<Guid, long> customPayouts = new();

    private readonly HashSet<string> owned = new(StringComparer.Ordinal);

    private string? chosenTheme;

    private string? chosenLook;

    public Appearance Appearance { get; set; } = Appearance.System;

    /// <summary>The player's coin balance (since version 8). <see cref="Wallet"/> is the one place it rises.</summary>
    public long Coins => coins;

    /// <summary>Adds to the balance, stopping at <see cref="long.MaxValue"/> rather than overflowing.</summary>
    public void AddCoins(long amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        coins = amount > long.MaxValue - coins ? long.MaxValue : coins + amount;
    }

    /// <summary>
    /// The most an imported custom level has paid, across every version under its ID, or null when it never has (since
    /// version 8). "Delete my data" keeps it, as it keeps the balance.
    /// </summary>
    public long? CustomPayout(Guid id) => customPayouts.TryGetValue(id, out var payout) ? payout : null;

    public void SetCustomPayout(Guid id, long payout)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A level ID can't be all zeros.", nameof(id));
        }
        ArgumentOutOfRangeException.ThrowIfLessThan(payout, 1);
        customPayouts[id] = payout;
    }

    /// <summary>
    /// Takes the amount off the balance, or returns false and changes nothing when the balance is short. Only
    /// <see cref="Wallet"/> spends.
    /// </summary>
    public bool SpendCoins(long amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        if (amount > coins)
        {
            return false;
        }
        coins -= amount;
        return true;
    }

    /// <summary>The cosmetics bought from the shop, by catalogue ID (since version 9). The free defaults are never listed.</summary>
    public IReadOnlySet<string> Owned => owned;

    /// <summary>Records a bought cosmetic, returning false when it was already owned. Only <see cref="Wallet"/> adds.</summary>
    public bool AddOwned(string id)
    {
        if (!Core.Cosmetics.IsId(id))
        {
            throw new ArgumentException($"\"{id}\" isn't a cosmetics ID.", nameof(id));
        }
        return owned.Add(id);
    }

    /// <summary>The theme being worn: an owned ID, or null for the free default.</summary>
    public string? ChosenTheme
    {
        get => chosenTheme;
        set => chosenTheme = CheckChoice(value);
    }

    /// <summary>The player look being worn: an owned ID, or null for the free default.</summary>
    public string? ChosenLook
    {
        get => chosenLook;
        set => chosenLook = CheckChoice(value);
    }

    private string? CheckChoice(string? id)
    {
        if (id is not null && !owned.Contains(id))
        {
            throw new ArgumentException($"\"{id}\" isn't owned, so it can't be chosen.", nameof(id));
        }
        return id;
    }

    /// <summary>The personal bests for one infinite choice, or null when no run at it has raised any (since version 7).</summary>
    public InfiniteBest? InfiniteBestFor(InfiniteChoice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        return infiniteBests.GetValueOrDefault(choice);
    }

    /// <summary>
    /// Keeps the larger of each saved best and the candidate's. Returns whether any rose, so the caller writes the save only
    /// then. Throws <see cref="ArgumentOutOfRangeException"/>, changing nothing, for anything <see cref="Parse"/> would refuse.
    /// </summary>
    public bool RecordInfiniteBest(InfiniteChoice choice, InfiniteBest candidate)
    {
        ArgumentNullException.ThrowIfNull(choice);
        ArgumentNullException.ThrowIfNull(candidate);
        CheckChoice(choice);
        ArgumentOutOfRangeException.ThrowIfNegative(candidate.Streak, nameof(candidate));
        ArgumentOutOfRangeException.ThrowIfNegative(candidate.Run, nameof(candidate));
        if (candidate.Efficiency is { } efficiency)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(efficiency, nameof(candidate));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(efficiency, MaxEfficiency, nameof(candidate));
        }
        var saved = infiniteBests.GetValueOrDefault(choice) ?? InfiniteBest.None;
        var merged = saved.Max(candidate);
        // All zeros is only ever give-ups filling the window: no best to show yet
        if (merged == saved || merged is { Streak: 0, Run: 0, Efficiency: null or 0 })
        {
            return false;
        }
        infiniteBests[choice] = merged;
        return true;
    }

    private const int MaxEfficiency = 10000;

    private static void CheckChoice(InfiniteChoice choice)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(choice.Difficulty, 1, nameof(choice));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(choice.Difficulty, InfiniteConfig.DifficultyCount, nameof(choice));
        ArgumentOutOfRangeException.ThrowIfLessThan(choice.Side, 2, nameof(choice));
    }

    // The last choice played in infinite mode, or null before any (since version 3). Only what Parse accepts, so a write
    // can never make the next load a broken save
    public InfiniteChoice? Infinite
    {
        get => infinite;
        set
        {
            if (value is { } choice)
            {
                CheckChoice(choice);
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

    /// <summary>
    /// The levels this device published to the library, each with the <see cref="CustomLevel.Edited"/> time of the version
    /// published (since version 6). It shows "In the library" and "Publish update" with no request; "Delete my data"
    /// clears it, as the server deletes the levels with the player.
    /// </summary>
    public IReadOnlyDictionary<Guid, DateTimeOffset> Published => published;

    public void RecordPublished(Guid id, DateTimeOffset edited)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A level ID can't be all zeros.", nameof(id));
        }
        published[id] = WholeSeconds.Of(edited);
    }

    public bool RemovePublished(Guid id) => published.Remove(id);

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
        keys.Require(keys.Root, "library", 6);
        keys.Require(keys.Root, "infiniteBests", 7);
        keys.Require(keys.Root, "coins", 8);
        keys.Require(keys.Root, "cosmetics", 9);
        if (keys.Number >= 4)
        {
            keys.Require(keys.Consents, "library", 6);
        }
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
            save.SetConsent(ConsentKind.Library, ReadConsent(answers.Library, "library"));
            save.player = ReadGuid(file.Player, "player");
        }
        if (keys.Number >= 5)
        {
            ReadServer(save, file.Server ?? throw new JsonException("The save's \"server\" is null."));
        }
        if (keys.Number >= 6)
        {
            ReadLibrary(save, file.Library ?? throw new JsonException("The save's \"library\" is null."));
        }
        if (keys.Number >= 7)
        {
            ReadInfiniteBests(save, file.InfiniteBests ?? throw new JsonException("The save's \"infiniteBests\" is null."));
        }
        if (keys.Number >= 8)
        {
            ReadCoins(save, file.Coins ?? throw new JsonException("The save's \"coins\" is null."));
        }
        if (keys.Number >= 9)
        {
            ReadCosmetics(save, file.Cosmetics ?? throw new JsonException("The save's \"cosmetics\" is null."));
        }
        return save;
    }

    private static void ReadCoins(SaveGame save, CoinsJson coins)
    {
        if (coins.Balance < 0)
        {
            throw new JsonException($"The save's \"balance\" is {coins.Balance}; it must be at least 0.");
        }
        save.coins = coins.Balance;
        foreach (var (key, payout) in coins.CustomPayouts ?? throw new JsonException("The save's \"customPayouts\" is null."))
        {
            // Lowercase only, as the custom level files name them
            if (!Guid.TryParseExact(key, "D", out var id) || id == Guid.Empty || key != id.ToString("D"))
            {
                throw new JsonException($"The save's custom payout \"{key}\" isn't a lowercase GUID, or is all zeros.");
            }
            if (payout < 1)
            {
                throw new JsonException($"The save's custom payout for {key} is {payout}; it must be at least 1.");
            }
            save.customPayouts[id] = payout;
        }
    }

    private static void ReadCosmetics(SaveGame save, CosmeticsSaveJson cosmetics)
    {
        foreach (var id in cosmetics.Owned ?? throw new JsonException("The save's \"owned\" is null."))
        {
            if (!Core.Cosmetics.IsId(id))
            {
                throw new JsonException($"The save's owned cosmetic \"{id}\" isn't a cosmetics ID.");
            }
            if (!save.owned.Add(id!))
            {
                throw new JsonException($"The save owns \"{id}\" twice.");
            }
        }
        save.chosenTheme = ReadChoice(save, cosmetics.Theme, "theme");
        save.chosenLook = ReadChoice(save, cosmetics.Look, "look");
    }

    // A bought item, or null for the free default: the save never names a free item, since it doesn't know which they are
    private static string? ReadChoice(SaveGame save, string? id, string key)
    {
        if (id is not null && !save.owned.Contains(id))
        {
            throw new JsonException($"The save's \"{key}\" is \"{id}\", which it doesn't own.");
        }
        return id;
    }

    private static void ReadInfiniteBests(SaveGame save, InfiniteBestJson?[] bests)
    {
        foreach (var entry in bests)
        {
            if (entry is null)
            {
                throw new JsonException("The save has a null infinite best.");
            }
            var choice = ReadChoice(new InfiniteChoiceJson
            {
                Difficulty = entry.Difficulty,
                Side = entry.Side,
                Walls = entry.Walls,
                Traps = entry.Traps,
                Teleporters = entry.Teleporters,
                Irregular = entry.Irregular,
            })!;
            if (entry.Streak < 0 || entry.Run < 0)
            {
                throw new JsonException("The save's infinite best has a \"streak\" or \"run\" below 0.");
            }
            if (entry.Efficiency is < 0 or > MaxEfficiency)
            {
                throw new JsonException($"The save's infinite best has \"efficiency\" {entry.Efficiency}; it must be from 0 to {MaxEfficiency}.");
            }
            if (!save.infiniteBests.TryAdd(choice, new InfiniteBest(entry.Streak, entry.Run, entry.Efficiency)))
            {
                throw new JsonException("The save lists one infinite choice's bests twice.");
            }
        }
    }

    private static void ReadLibrary(SaveGame save, LibraryJson library)
    {
        foreach (var (key, value) in library.Published ?? throw new JsonException("The save's \"published\" is null."))
        {
            // Lowercase only, as the custom level files name them
            if (!Guid.TryParseExact(key, "D", out var id) || id == Guid.Empty || key != id.ToString("D"))
            {
                throw new JsonException($"The save's published level \"{key}\" isn't a lowercase GUID, or is all zeros.");
            }
            if (value is null
                || !DateTimeOffset.TryParseExact(
                    value, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var edited))
            {
                throw new JsonException($"The save's published level {key} has a time that isn't yyyy-MM-ddTHH:mm:ssZ.");
            }
            save.published[id] = edited;
        }
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
                Library = WriteConsent(ConsentTo(ConsentKind.Library)),
            },
            Player = player?.ToString("D"),
            Server = new ServerJson
            {
                Session = sessionToken,
                PendingDeletions = pendingDeletions.Select(id => id.ToString("D")).Order(StringComparer.Ordinal).ToArray(),
                RecordsSent = recordsSent,
            },
            Library = new LibraryJson
            {
                Published = published.OrderBy(pair => pair.Key.ToString("D"), StringComparer.Ordinal).ToDictionary(
                    pair => pair.Key.ToString("D"),
                    pair => (string?)pair.Value.UtcDateTime.ToString(TimeFormat, CultureInfo.InvariantCulture)),
            },
            InfiniteBests = infiniteBests
                .OrderBy(pair => pair.Key.Difficulty)
                .ThenBy(pair => pair.Key.Side)
                .ThenBy(pair => pair.Key.Walls)
                .ThenBy(pair => pair.Key.Traps)
                .ThenBy(pair => pair.Key.Teleporters)
                .ThenBy(pair => pair.Key.Irregular)
                .Select(pair => (InfiniteBestJson?)new InfiniteBestJson
                {
                    Difficulty = pair.Key.Difficulty,
                    Side = pair.Key.Side,
                    Walls = pair.Key.Walls,
                    Traps = pair.Key.Traps,
                    Teleporters = pair.Key.Teleporters,
                    Irregular = pair.Key.Irregular,
                    Streak = pair.Value.Streak,
                    Run = pair.Value.Run,
                    Efficiency = pair.Value.Efficiency,
                })
                .ToArray(),
            Coins = new CoinsJson
            {
                Balance = coins,
                CustomPayouts = customPayouts.OrderBy(pair => pair.Key.ToString("D"), StringComparer.Ordinal).ToDictionary(
                    pair => pair.Key.ToString("D"),
                    pair => pair.Value),
            },
            Cosmetics = new CosmeticsSaveJson
            {
                Owned = owned.Order(StringComparer.Ordinal).ToArray(),
                Theme = chosenTheme,
                Look = chosenLook,
            },
        };
        return JsonSerializer.Serialize(file, SaveJsonContext.Default.SaveJson) + "\n";
    }

    private static ConsentJson? WriteConsent(Consent? answer) =>
        answer is null ? null : new ConsentJson { Answer = answer.Yes ? "yes" : "no", Wording = answer.Wording };

    private const char ByteOrderMark = (char)0xFEFF;

    // The custom level files' time format, so a published level's time compares with its file's Edited
    private const string TimeFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

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
        return new SaveKeys(
            number, root.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal), KeysOf(root, "settings"), KeysOf(root, "consents"));
    }

    private static IReadOnlySet<string> KeysOf(JsonElement root, string key) =>
        root.TryGetProperty(key, out var element) && element.ValueKind == JsonValueKind.Object
            ? element.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>();

    private static bool IsFingerprint(string? layout) =>
        layout is { Length: 16 } && layout.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    private sealed record LevelRecord(int BestMoves, string Layout);

    // The keys at the save's root, in its settings and in its consents, so each section checks its own key against the
    // versions that have it
    private sealed record SaveKeys(int Number, IReadOnlySet<string> Root, IReadOnlySet<string> Settings, IReadOnlySet<string> Consents)
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

    // Since version 6
    public LibraryJson? Library { get; init; }

    // Since version 7
    public InfiniteBestJson?[]? InfiniteBests { get; init; }

    // Since version 8
    public CoinsJson? Coins { get; init; }

    // Since version 9 (chunk 28)
    public CosmeticsSaveJson? Cosmetics { get; init; }
}

internal sealed record CosmeticsSaveJson
{
    public required string?[]? Owned { get; init; }

    public required string? Theme { get; init; }

    public required string? Look { get; init; }
}

internal sealed record CoinsJson
{
    public required long Balance { get; init; }

    // An imported custom level's ID to the most it has paid
    public required Dictionary<string, long>? CustomPayouts { get; init; }
}

internal sealed record InfiniteBestJson
{
    public required int Difficulty { get; init; }

    public required int Side { get; init; }

    public required bool Walls { get; init; }

    public required bool Traps { get; init; }

    public required bool Teleporters { get; init; }

    public required bool Irregular { get; init; }

    public required int Streak { get; init; }

    public required long Run { get; init; }

    // Hundredths of a percent; null while no run at the choice has reached the window
    public required int? Efficiency { get; init; }
}

internal sealed record LibraryJson
{
    // A level's ID to the Edited time of the version published
    public required Dictionary<string, string?>? Published { get; init; }
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

    // Since version 6
    public ConsentJson? Library { get; init; }
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
