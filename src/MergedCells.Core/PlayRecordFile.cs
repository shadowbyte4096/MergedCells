using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MergedCells.Core;

/// <summary>
/// Reads and writes one play record as one line of JSON: the version, the record's and player's GUIDs, when and where it
/// was played, the play-data wording the player agreed to (since version 2), the mode with the campaign level, the infinite
/// choice or (since version 2) the custom level, the map in the level file's keys, par, the metrics as counted then, and
/// how it was played.
/// </summary>
public static class PlayRecordFile
{
    public const int CurrentVersion = 2;

    private const string DateFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    /// <summary>
    /// The record's line, with no line break. Throws <see cref="ArgumentOutOfRangeException"/> for a consent below 1: a
    /// version 1 record read back can't be written as version 2.
    /// </summary>
    public static string Write(PlayRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var start = record.Start;
        ArgumentOutOfRangeException.ThrowIfLessThan(start.Consent, 1, "record.Start.Consent");
        var map = LevelFile.ArraysOf(start.Map);
        var metrics = start.Metrics;
        var json = new PlayRecordJson
        {
            Version = CurrentVersion,
            Id = start.Id.ToString("D"),
            Player = start.Player.ToString("D"),
            Started = start.Started.UtcDateTime.ToString(DateFormat, CultureInfo.InvariantCulture),
            Build = start.Build,
            Platform = start.Platform,
            Consent = start.Consent,
            Mode = DifficultyConfig.KeyOf(start.Mode),
            Level = start.Level,
            SeenBefore = start.SeenBefore,
            Infinite = start.Infinite is { } choice
                ? new PlayChoiceJson
                {
                    Difficulty = choice.Difficulty,
                    Side = choice.Side,
                    Walls = choice.Walls,
                    Traps = choice.Traps,
                    Teleporters = choice.Teleporters,
                    Irregular = choice.Irregular,
                }
                : null,
            Custom = start.Custom is { } custom ? new CustomPlayJson { Id = custom.Id.ToString("D"), Imported = custom.Imported } : null,
            Regions = map.Regions,
            Tiles = map.Tiles,
            Teleporters = map.Teleporters,
            Start = map.Start,
            End = map.End,
            Par = start.Par,
            MetricsVersion = start.MetricsVersion,
            NearOptimalExtraMoves = start.NearOptimalExtraMoves,
            BruteForceLookahead = start.BruteForceLookahead,
            Metrics = new PlayMetricsJson
            {
                AwayMoves = metrics.AwayMoves,
                Retreat = metrics.Retreat,
                GreedyGap = metrics.GreedyGap,
                Decoys = metrics.Decoys,
                NearOptimalRoutes = metrics.NearOptimalRoutes,
                BruteForceExtraMoves = metrics.BruteForceExtraMoves,
            },
            Score = start.Score,
            Outcome = Word(record.Outcome),
            Moves = record.Moves,
            Undos = record.Undos,
            Restarts = record.Restarts,
            ActiveMs = record.ActiveMs,
            Actions = record.Actions.Select(action => new PlayActionJson { At = action.At, Do = Word(action.Kind) }).ToArray(),
        };
        return JsonSerializer.Serialize(json, PlayRecordJsonContext.Default.PlayRecordJson);
    }

    /// <summary>
    /// Parses one record's line. Throws <see cref="NewerPlayRecordException"/> when its version is newer than this build's,
    /// whatever else it holds; <see cref="JsonException"/>, naming the problem, when a value is missing, null, of the wrong
    /// type or out of range, when a key is unknown or repeated, or when the text isn't JSON; and
    /// <see cref="InvalidMapException"/> when the map breaks a map rule.
    /// </summary>
    public static PlayRecord Parse(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var version = CheckVersion(line);
        var json = JsonSerializer.Deserialize(line, PlayRecordJsonContext.Default.PlayRecordJson)
            ?? throw new JsonException("The play record is null.");
        JsonKeys.RejectDuplicates(line, "play record");

        var mode = ReadWord(Require(json.Mode, "mode"), Enum.GetValues<DifficultyMode>(), DifficultyConfig.KeyOf, "mode");
        CheckModeKeys(json, mode);
        // Version 1 records were made under chunk 22's wording, which never mentioned sending
        var consent = version >= 2 ? json.Consent ?? throw new JsonException("The play record's \"consent\" is null.") : 0;
        if (version >= 2)
        {
            AtLeast(consent, 1, "consent");
        }
        var metrics = Require(json.Metrics, "metrics");
        AtLeast(json.Par, 1, "par");
        AtLeast(json.MetricsVersion, 1, "metricsVersion");
        AtLeast(json.NearOptimalExtraMoves, 1, "nearOptimalExtraMoves");
        AtLeast(json.BruteForceLookahead, 1, "bruteForceLookahead");
        if (json.Score is < 0 or > 100)
        {
            throw new JsonException($"The play record's \"score\" is {json.Score}; it must be from 0 to 100.");
        }
        AtLeast(metrics.AwayMoves, 0, "awayMoves");
        AtLeast(metrics.Retreat, 0, "retreat");
        AtLeast(metrics.GreedyGap ?? 0, 0, "greedyGap");
        AtLeast(metrics.Decoys, 0, "decoys");
        AtLeast(metrics.NearOptimalRoutes, 0, "nearOptimalRoutes");
        AtLeast(metrics.BruteForceExtraMoves ?? 0, 0, "bruteForceExtraMoves");
        AtLeast(json.Moves, 0, "moves");
        AtLeast(json.Undos, 0, "undos");
        AtLeast(json.Restarts, 0, "restarts");
        AtLeast(json.ActiveMs, 0, "activeMs");
        var actions = Require(json.Actions, "actions").Select(ReadAction).ToArray();

        var map = LevelFile.ReadMap(Require(json.Regions, "regions"), json.Tiles, json.Teleporters, Require(json.Start, "start"), Require(json.End, "end"));
        var start = new PlayRecordStart(
            ReadGuid(json.Id, "id"),
            ReadGuid(json.Player, "player"),
            ReadTime(Require(json.Started, "started")),
            Require(json.Build, "build"),
            Require(json.Platform, "platform"),
            consent,
            mode,
            json.Level,
            json.SeenBefore,
            json.Infinite is { } choice ? new InfiniteChoice(choice.Difficulty, choice.Side, choice.Walls, choice.Traps, choice.Teleporters, choice.Irregular) : null,
            json.Custom is { } custom ? new CustomPlay(ReadGuid(custom.Id, "custom's id"), custom.Imported) : null,
            map,
            json.Par,
            json.MetricsVersion,
            json.NearOptimalExtraMoves,
            json.BruteForceLookahead,
            new DifficultyMetrics(json.Par, metrics.AwayMoves, metrics.Retreat, metrics.GreedyGap, metrics.Decoys, metrics.NearOptimalRoutes, metrics.BruteForceExtraMoves),
            json.Score);
        var outcome = ReadWord(Require(json.Outcome, "outcome"), Enum.GetValues<PlayOutcome>(), Word, "outcome");
        return new PlayRecord(start, outcome, json.Moves, json.Undos, json.Restarts, json.ActiveMs, actions);
    }

    // A campaign record names its level, an infinite one its choice and a custom one its level's ID. Later modes add their
    // own rules
    private static void CheckModeKeys(PlayRecordJson json, DifficultyMode mode)
    {
        if (json.Level is { } level)
        {
            AtLeast(level, 1, "level");
        }
        if (json.Infinite is { } choice)
        {
            if (choice.Difficulty is < 1 or > InfiniteConfig.DifficultyCount)
            {
                throw new JsonException($"The play record's infinite \"difficulty\" is {choice.Difficulty}; it must be from 1 to {InfiniteConfig.DifficultyCount}.");
            }
            AtLeast(choice.Side, 2, "side");
        }
        if (mode == DifficultyMode.Campaign && (json.Level is null || json.SeenBefore is null || json.Infinite is not null || json.Custom is not null))
        {
            throw new JsonException("A campaign play record needs a \"level\" and \"seenBefore\", and no \"infinite\" or \"custom\".");
        }
        if (mode == DifficultyMode.Infinite && (json.Infinite is null || json.Level is not null || json.SeenBefore is not null || json.Custom is not null))
        {
            throw new JsonException("An infinite play record needs \"infinite\", and no \"level\", \"seenBefore\" or \"custom\".");
        }
        if (mode == DifficultyMode.Custom && (json.Custom is null || json.Level is not null || json.SeenBefore is not null || json.Infinite is not null))
        {
            throw new JsonException("A custom play record needs \"custom\", and no \"level\", \"seenBefore\" or \"infinite\".");
        }
    }

    private static PlayAction ReadAction(PlayActionJson? action, int index)
    {
        if (action is null)
        {
            throw new JsonException($"The play record's action {index} is null.");
        }
        AtLeast(action.At, 0, "at");
        return new PlayAction(action.At, ReadWord(Require(action.Do, "do"), Enum.GetValues<PlayActionKind>(), Word, "do"));
    }

    private static string Word<T>(T value) where T : struct, Enum => JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

    // Exact words rather than .NET's enum converter, which accepts numbers and undefined values on .NET 8
    private static T ReadWord<T>(string text, IEnumerable<T> values, Func<T, string> word, string key) where T : struct, Enum =>
        values.Where(value => word(value) == text).Select(value => (T?)value).FirstOrDefault()
        ?? throw new JsonException($"The play record's \"{key}\" \"{text}\" isn't one this build knows.");

    private static Guid ReadGuid(string? text, string key)
    {
        // Lowercase only, as the build writes them, so one record has one spelling
        if (text is null || !Guid.TryParseExact(text, "D", out var id) || id == Guid.Empty || text != id.ToString("D"))
        {
            throw new JsonException($"The play record's \"{key}\" isn't a lowercase GUID, or is all zeros.");
        }
        return id;
    }

    private static DateTimeOffset ReadTime(string value) =>
        DateTimeOffset.TryParseExact(
            value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var time)
            ? time
            : throw new JsonException($"The play record's \"started\" \"{value}\" isn't a UTC time written as {DateFormat}.");

    private static void AtLeast(long value, long min, string key)
    {
        if (value < min)
        {
            throw new JsonException($"The play record's \"{key}\" is {value}; it must be at least {min}.");
        }
    }

    // .NET 8's System.Text.Json enforces required keys but not non-null values
    private static T Require<T>(T? value, string key) where T : class =>
        value ?? throw new JsonException($"The play record's \"{key}\" is null.");

    // The version decides how the rest is read, so a newer record with keys this build doesn't know is never called broken.
    // The keys a version has are checked here too, because the strict reader can't tell a missing key from a null one
    private static int CheckVersion(string line)
    {
        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("The play record isn't a JSON object.");
        }
        if (!root.TryGetProperty("version", out var version))
        {
            throw new JsonException("The play record has no \"version\".");
        }
        if (version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number))
        {
            throw new JsonException("The play record's \"version\" isn't a whole number.");
        }
        if (number > CurrentVersion)
        {
            throw new NewerPlayRecordException(number);
        }
        if (number < 1)
        {
            throw new JsonException($"The play record's \"version\" is {number}; it must be at least 1.");
        }
        foreach (var key in new[] { "consent", "custom" })
        {
            if (root.TryGetProperty(key, out _) != number >= 2)
            {
                throw new JsonException(number >= 2
                    ? $"The play record has no \"{key}\"."
                    : $"The play record is version {number}, which has no \"{key}\".");
            }
        }
        return number;
    }
}

// Every key required, null or not, so a record always has the same keys in the same order. The two version 2 keys can't be
// required, or version 1 lines wouldn't read: the version pass checks them
internal sealed record PlayRecordJson
{
    public required int Version { get; init; }

    public required string Id { get; init; }

    public required string Player { get; init; }

    public required string Started { get; init; }

    public required string Build { get; init; }

    public required string Platform { get; init; }

    // Since version 2
    public int? Consent { get; init; }

    public required string Mode { get; init; }

    public required int? Level { get; init; }

    public required bool? SeenBefore { get; init; }

    public required PlayChoiceJson? Infinite { get; init; }

    // Since version 2
    public CustomPlayJson? Custom { get; init; }

    public required int[][] Regions { get; init; }

    public required int[][]? Tiles { get; init; }

    public required int[][][]? Teleporters { get; init; }

    public required int[] Start { get; init; }

    public required int[] End { get; init; }

    public required int Par { get; init; }

    public required int MetricsVersion { get; init; }

    public required int NearOptimalExtraMoves { get; init; }

    public required int BruteForceLookahead { get; init; }

    public required PlayMetricsJson Metrics { get; init; }

    public required int Score { get; init; }

    public required string Outcome { get; init; }

    public required int Moves { get; init; }

    public required int Undos { get; init; }

    public required int Restarts { get; init; }

    public required long ActiveMs { get; init; }

    public required PlayActionJson[] Actions { get; init; }
}

// The record's own copy of the choice's shape, so a change to the save's never changes a version 1 record
internal sealed record PlayChoiceJson
{
    public required int Difficulty { get; init; }

    public required int Side { get; init; }

    public required bool Walls { get; init; }

    public required bool Traps { get; init; }

    public required bool Teleporters { get; init; }

    public required bool Irregular { get; init; }
}

// The level's GUID and whether it was imported; never its name, which is free text
internal sealed record CustomPlayJson
{
    public required string Id { get; init; }

    public required bool Imported { get; init; }
}

internal sealed record PlayMetricsJson
{
    public required int AwayMoves { get; init; }

    public required int Retreat { get; init; }

    public required int? GreedyGap { get; init; }

    public required int Decoys { get; init; }

    public required long NearOptimalRoutes { get; init; }

    public required int? BruteForceExtraMoves { get; init; }
}

internal sealed record PlayActionJson
{
    public required long At { get; init; }

    public required string Do { get; init; }
}

// Source-generated, so parsing needs no reflection and survives trimming
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(PlayRecordJson))]
internal partial class PlayRecordJsonContext : JsonSerializerContext;
