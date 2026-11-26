using System.Text.Json;
using System.Text.Json.Serialization;

namespace MergedCells.Core;

// Every tunable number in the game, read from config/game_config.json
public sealed record GameConfig
{
    private const char ByteOrderMark = (char)0xFEFF;

    public required InputConfig Input { get; init; }

    public required SlideConfig Slide { get; init; }

    public required StarsConfig Stars { get; init; }

    public required CameraConfig Camera { get; init; }

    public required CampaignConfig Campaign { get; init; }

    public required GeneratorConfig Generator { get; init; }

    public required DifficultyConfig Difficulty { get; init; }

    public required ClimbConfig Climb { get; init; }

    public required CustomLevelsConfig CustomLevels { get; init; }

    public required InfiniteConfig Infinite { get; init; }

    public required ServerConfig Server { get; init; }

    public required PrivacyConfig Privacy { get; init; }

    public required LeaderboardConfig Leaderboard { get; init; }

    public required LibraryConfig Library { get; init; }

    public required IntroConfig Intro { get; init; }

    public required CoinsConfig Coins { get; init; }

    public required ShopConfig Shop { get; init; }

    // The one place a level's star thresholds are resolved: its own entry, then its chapter's, then the top-level group
    public StarsConfig StarsFor(Level level)
    {
        ArgumentNullException.ThrowIfNull(level);
        if (Campaign.LevelStars.TryGetValue(level.Id, out var own))
        {
            return own;
        }
        return Campaign.ChapterStars.TryGetValue(level.Chapter, out var chapter) ? chapter : Stars;
    }

    /// <summary>
    /// Runs every number through the code that uses it, so a value out of range stops the game at start-up rather than
    /// part-way through a screen. Throws <see cref="ArgumentOutOfRangeException"/> naming the value. The gate
    /// percentages need the save, so <see cref="CampaignProgress.Of"/> checks those.
    /// </summary>
    public void CheckRanges(IEnumerable<Level> levels)
    {
        ArgumentNullException.ThrowIfNull(levels);
        _ = new SwipeDetector(Input.SwipeThreshold);
        _ = SlideTiming.Seconds(1, Slide);
        _ = TrapTiming.Seconds(Slide);
        _ = TeleportTiming.Seconds(0, Slide);
        // Any board and band will do: the config values are checked first
        _ = new CameraSpace(new BoardRect(0, 0, 64, 64), new System.Numerics.Vector2(720, 1280), 24, 64, Camera);
        _ = new PinchTracker(Camera.GestureSlop);
        _ = new DragTracker(Camera.GestureSlop);
        _ = new PlayRules(Campaign.UndoLimit);
        _ = new MapGenerator(Generator);
        _ = new DifficultyRater(Difficulty);
        _ = new MapClimber(Generator, Difficulty, Climb);
        CustomLevels.CheckRanges();
        _ = new PlayRules(CustomLevels.UndoLimit);
        Infinite.CheckRanges();
        _ = new MapClimber(Generator, Difficulty, Infinite.Climb(Climb));
        Server.CheckRanges();
        Privacy.CheckRanges();
        Leaderboard.CheckRanges();
        Library.CheckRanges(CustomLevels);
        Intro.CheckRanges();
        Coins.CheckRanges();
        Shop.CheckRanges();
        CheckStarLimits(1, Stars, "The config's \"stars\"");
        foreach (var level in levels)
        {
            CheckStarLimits(level.Par, StarsFor(level), $"Level {level.Id}'s star thresholds");
        }
    }

    // A 1-star limit below the 2-star limit would make 1 star impossible to earn
    private static void CheckStarLimits(int par, StarsConfig stars, string name)
    {
        var twoStar = Core.Stars.TwoStarLimit(par, stars);
        var oneStar = Core.Stars.OneStarLimit(par, stars);
        if (oneStar < twoStar)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stars), $"{name} give a 1-star limit of {oneStar} moves, below the 2-star limit of {twoStar}.");
        }
    }

    /// <summary>
    /// Parses the config file's text. Throws <see cref="JsonException"/>, naming the problem, when a group or value
    /// is missing, null or of the wrong type, when a key is unknown or repeated, or when the text isn't JSON.
    /// </summary>
    public static GameConfig Parse(string json)
    {
        // Some Windows editors save UTF-8 with a byte order mark, which System.Text.Json rejects
        var text = json.TrimStart(ByteOrderMark);
        var config = JsonSerializer.Deserialize(text, GameConfigJsonContext.Default.GameConfig)
            ?? throw new JsonException("The config is null.");
        JsonKeys.RejectDuplicates(text, "config");

        // .NET 8's System.Text.Json enforces required keys but not non-null values, so each group is checked here
        if (config.Input is null)
        {
            throw new JsonException("The config's \"input\" group is null.");
        }
        if (config.Slide is null)
        {
            throw new JsonException("The config's \"slide\" group is null.");
        }
        if (config.Stars is null)
        {
            throw new JsonException("The config's \"stars\" group is null.");
        }
        if (config.Camera is null)
        {
            throw new JsonException("The config's \"camera\" group is null.");
        }
        if (config.Campaign is null)
        {
            throw new JsonException("The config's \"campaign\" group is null.");
        }
        if (config.Generator is null)
        {
            throw new JsonException("The config's \"generator\" group is null.");
        }
        if (config.Campaign.ChapterStars is null)
        {
            throw new JsonException("The config's \"chapterStars\" is null.");
        }
        foreach (var (chapter, stars) in config.Campaign.ChapterStars)
        {
            if (chapter < 1)
            {
                throw new JsonException($"The config's \"chapterStars\" key {chapter} must be at least 1.");
            }
            if (stars is null)
            {
                throw new JsonException($"The config's \"chapterStars\" entry for chapter {chapter} is null.");
            }
        }
        if (config.Campaign.ChapterGatePercent is null)
        {
            throw new JsonException("The config's \"chapterGatePercent\" is null.");
        }
        foreach (var chapter in config.Campaign.ChapterGatePercent.Keys)
        {
            if (chapter < 1)
            {
                throw new JsonException($"The config's \"chapterGatePercent\" key {chapter} must be at least 1.");
            }
        }
        if (config.Campaign.LevelStars is null)
        {
            throw new JsonException("The config's \"levelStars\" is null.");
        }
        foreach (var (id, stars) in config.Campaign.LevelStars)
        {
            if (id < 1)
            {
                throw new JsonException($"The config's \"levelStars\" key {id} must be at least 1.");
            }
            if (stars is null)
            {
                throw new JsonException($"The config's \"levelStars\" entry for level {id} is null.");
            }
        }
        CheckDifficulty(config.Difficulty);
        if (config.Climb is null)
        {
            throw new JsonException("The config's \"climb\" group is null.");
        }
        CheckCustomLevels(config.CustomLevels);
        CheckInfinite(config.Infinite);
        CheckServer(config.Server, config.Privacy);
        if (config.Leaderboard is null)
        {
            throw new JsonException("The config's \"leaderboard\" group is null.");
        }
        if (config.Library is null)
        {
            throw new JsonException("The config's \"library\" group is null.");
        }
        if (config.Library.SizeSteps is null)
        {
            throw new JsonException("The config's \"sizeSteps\" is null.");
        }
        if (config.Intro is null)
        {
            throw new JsonException("The config's \"intro\" group is null.");
        }
        if (config.Coins is null)
        {
            throw new JsonException("The config's \"coins\" group is null.");
        }
        if (config.Shop is null)
        {
            throw new JsonException("The config's \"shop\" group is null.");
        }
        if (config.Shop.Prices is null)
        {
            throw new JsonException("The config's \"prices\" is null.");
        }
        return config;
    }

    private static void CheckServer(ServerConfig? server, PrivacyConfig? privacy)
    {
        if (server is null)
        {
            throw new JsonException("The config's \"server\" group is null.");
        }
        if (server.Url is null)
        {
            throw new JsonException("The config's \"server.url\" is null.");
        }
        if (server.PublishableKey is null)
        {
            throw new JsonException("The config's \"server.publishableKey\" is null.");
        }
        if (privacy is null)
        {
            throw new JsonException("The config's \"privacy\" group is null.");
        }
    }

    private static void CheckInfinite(InfiniteConfig? infinite)
    {
        if (infinite is null)
        {
            throw new JsonException("The config's \"infinite\" group is null.");
        }
        if (infinite.Scoring is null)
        {
            throw new JsonException("The config's \"infinite.scoring\" is null.");
        }
        if (infinite.Difficulties is null)
        {
            throw new JsonException("The config's \"infinite.difficulties\" is null.");
        }
        for (var index = 0; index < infinite.Difficulties.Count; index++)
        {
            var difficulty = infinite.Difficulties[index]
                ?? throw new JsonException($"The config's \"infinite.difficulties\" entry {index} is null.");
            if (difficulty.Plain is null)
            {
                throw new JsonException($"The config's \"infinite.difficulties\" entry {index} has a null \"plain\".");
            }
            if (difficulty.Twists is null)
            {
                throw new JsonException($"The config's \"infinite.difficulties\" entry {index} has a null \"twists\".");
            }
        }
    }

    private static void CheckCustomLevels(CustomLevelsConfig? customLevels)
    {
        if (customLevels is null)
        {
            throw new JsonException("The config's \"customLevels\" group is null.");
        }
        if (customLevels.Bands is null)
        {
            throw new JsonException("The config's \"customLevels.bands\" is null.");
        }
        for (var index = 0; index < customLevels.Bands.Count; index++)
        {
            if (customLevels.Bands[index] is null)
            {
                throw new JsonException($"The config's \"customLevels.bands\" entry {index} is null.");
            }
            if (customLevels.Bands[index].Name is null)
            {
                throw new JsonException($"The config's \"customLevels.bands\" entry {index} has a null \"name\".");
            }
        }
    }

    private static void CheckDifficulty(DifficultyConfig? difficulty)
    {
        if (difficulty is null)
        {
            throw new JsonException("The config's \"difficulty\" group is null.");
        }
        if (difficulty.Caps is null)
        {
            throw new JsonException("The config's \"difficulty.caps\" is null.");
        }
        if (difficulty.Weights is null)
        {
            throw new JsonException("The config's \"difficulty.weights\" is null.");
        }
        if (difficulty.ModeWeights is null)
        {
            throw new JsonException("The config's \"difficulty.modeWeights\" is null.");
        }
        foreach (var (key, weights) in difficulty.ModeWeights)
        {
            if (!DifficultyConfig.IsModeKey(key))
            {
                throw new JsonException($"The config's \"difficulty.modeWeights\" key {DifficultyConfig.NotAModeMessage(key)}");
            }
            if (weights is null)
            {
                throw new JsonException($"The config's \"difficulty.modeWeights\" entry for {key} is null.");
            }
        }
    }
}

public sealed record InputConfig
{
    // In viewport units (the 720-wide base)
    public required float SwipeThreshold { get; init; }
}

public sealed record SlideConfig
{
    // Squares a second, at a constant speed
    public required double SquaresPerSecond { get; init; }

    // In seconds: no slide takes longer, however far it goes
    public required double MaxSeconds { get; init; }

    // In seconds, after a slide into a trap: the player shrinks into it, is hidden, then grows out of the start
    public required double TrapShrinkSeconds { get; init; }

    public required double TrapPauseSeconds { get; init; }

    public required double TrapGrowSeconds { get; init; }

    // In seconds, at each teleporter: the player shrinks into it, then grows out of its partner
    public required double TeleportShrinkSeconds { get; init; }

    public required double TeleportGrowSeconds { get; init; }
}

// Each star limit is par plus the larger of its minimum and its percentage of par, rounded up
public sealed record StarsConfig
{
    public required int TwoStarMinExtraMoves { get; init; }

    public required int TwoStarExtraParPercent { get; init; }

    public required int OneStarMinExtraMoves { get; init; }

    public required int OneStarExtraParPercent { get; init; }
}

public sealed record CameraConfig
{
    // In viewport units. A level whose whole map would show smaller squares opens zoomed in to this size
    public required float ReadableSquareSize { get; init; }

    // In viewport units: the closest zoom
    public required float ClosestSquareSize { get; init; }

    // The middle part of the screen, on each axis, inside which a stop doesn't move the camera
    public required float FollowZonePercent { get; init; }

    public required double GlideSeconds { get; init; }

    public required double OpeningSeconds { get; init; }

    // On the whole map, before the opening zoom starts
    public required double OpeningHoldSeconds { get; init; }

    // The zoom factor for one mouse-wheel notch or zoom-key press
    public required float ZoomStep { get; init; }

    // In viewport units: how far a two-finger gesture or a mouse drag must move before it moves the view
    public required float GestureSlop { get; init; }
}

public sealed record CampaignConfig
{
    // A chapter's gate is this percentage of the stars in all earlier chapters, rounded up
    public required int GatePercent { get; init; }

    // Gate percentages for chapters that don't use gatePercent, keyed by chapter number
    public required IReadOnlyDictionary<int, int> ChapterGatePercent { get; init; }

    // Star thresholds for chapters that don't use the top-level "stars" group, keyed by chapter number
    public required IReadOnlyDictionary<int, StarsConfig> ChapterStars { get; init; }

    // Star thresholds for levels that don't use their chapter's, keyed by level ID
    public required IReadOnlyDictionary<int, StarsConfig> LevelStars { get; init; }

    // Undos per attempt; a restart gives them all back
    public required int UndoLimit { get; init; }
}

// Every number is a whole number, so a seed generates the same map on every device
public sealed record GeneratorConfig
{
    // Relative chances of each kind of region, in the order across, down, single, block
    public required int AcrossWeight { get; init; }

    public required int DownWeight { get; init; }

    public required int SingleWeight { get; init; }

    public required int BlockWeight { get; init; }

    // The longest strip, as a percentage of the map's side (rounded up, at least 2 squares)
    public required int MaxStripPercent { get; init; }

    // In squares, on each side of a block
    public required int MaxBlockSide { get; init; }

    // When a request turns bends on: the chance, in percent, that two neighbouring thin regions are joined
    public required int BendPercent { get; init; }

    public required int MaxBendSquares { get; init; }

    // The side of the top-left area the start is in, and of the bottom-right area the end is in, as a percentage of the map's side
    public required int CornerPercent { get; init; }

    // Rows plus columns between the start and the end, as a percentage of the corner-to-corner distance
    public required int MinDistancePercent { get; init; }

    // Maps tried per request before giving up
    public required int MaxAttempts { get; init; }

    // The longest straight run of walls placed at once, in squares
    public required int MaxWallRun { get; init; }
}

public sealed record DifficultyConfig
{
    // Near-optimal routes are those winning in par + 1 up to par + this many moves
    public required int NearOptimalExtraMoves { get; init; }

    // The deepest lookahead player: players looking 1 up to this many moves ahead are tried
    public required int BruteForceLookahead { get; init; }

    public required DifficultyCaps Caps { get; init; }

    public required DifficultyWeights Weights { get; init; }

    // Weights for modes that don't use the shared ones, keyed by the mode's camelCase name
    public required IReadOnlyDictionary<string, DifficultyWeights> ModeWeights { get; init; }

    internal static string KeyOf(DifficultyMode mode) => JsonNamingPolicy.CamelCase.ConvertName(mode.ToString());

    // Exact words rather than .NET's enum converter, which accepts undefined values on .NET 8
    internal static bool IsModeKey(string key) => Enum.GetValues<DifficultyMode>().Any(mode => KeyOf(mode) == key);

    internal static string NotAModeMessage(string key) =>
        $"\"{key}\" isn't a mode: use {string.Join(", ", Enum.GetValues<DifficultyMode>().Select(KeyOf))}.";
}

// How MapClimber searches: steps in all, steps without getting nearer before it starts again from a fresh map, and the
// relative chances of its five edits
public sealed record ClimbConfig
{
    public required int MaxSteps { get; init; }

    public required int StallSteps { get; init; }

    public required int SplitWeight { get; init; }

    public required int MergeWeight { get; init; }

    public required int MoveSquareWeight { get; init; }

    public required int MoveStartWeight { get; init; }

    public required int MoveTwistWeight { get; init; }
}

// Each metric counts towards the score up to its cap, and no further
public sealed record DifficultyCaps
{
    public required int AwayMoves { get; init; }

    public required int GreedyGap { get; init; }

    public required int Decoys { get; init; }

    public required int NearOptimalRoutes { get; init; }

    public required int BruteForceExtraMoves { get; init; }
}

// Whole-number percentages, 100 being 1x; only how they compare matters
public sealed record DifficultyWeights
{
    public required int AwayMoves { get; init; }

    public required int GreedyGap { get; init; }

    public required int Decoys { get; init; }

    public required int NearOptimalRoutes { get; init; }

    public required int BruteForceExtraMoves { get; init; }
}

// Source-generated, so parsing needs no reflection and survives trimming
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(GameConfig))]
internal partial class GameConfigJsonContext : JsonSerializerContext;
