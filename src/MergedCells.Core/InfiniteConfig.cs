namespace MergedCells.Core;

/// <summary>
/// The config's <c>infinite</c> group: the size slider's ends, the first choice, the par range, infinite mode's own climb
/// budget, each twist's density, and for each of the ten difficulties its score band and the sizes it allows, one range
/// for plain maps and one for maps with walls, traps or teleporters.
/// </summary>
public sealed record InfiniteConfig
{
    public const int DifficultyCount = 10;

    // The size slider's ends; the largest is the mode's cap
    public required int MinSide { get; init; }

    public required int MaxSide { get; init; }

    // The setup screen's choice before the player has made one
    public required int StartDifficulty { get; init; }

    public required int StartSide { get; init; }

    public required int MinPar { get; init; }

    public required int MaxPar { get; init; }

    // Replace the climb group's maxSteps and stallSteps, which are sized for offline curation
    public required int ClimbSteps { get; init; }

    public required int StallSteps { get; init; }

    // A share of the map's squares, in percent, when the twist's switch is on
    public required int WallPercent { get; init; }

    public required int TrapPercent { get; init; }

    public required int TeleporterPercent { get; init; }

    // How long a map may take before its loading bar shows, so a quick map doesn't flicker
    public required double LoadingDelaySeconds { get; init; }

    // Difficulty 1 first
    public required IReadOnlyList<InfiniteDifficulty> Difficulties { get; init; }

    public InfiniteDifficulty Difficulty(int difficulty)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(difficulty, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(difficulty, DifficultyCount);
        return Difficulties[difficulty - 1];
    }

    public ClimbConfig Climb(ClimbConfig shared)
    {
        ArgumentNullException.ThrowIfNull(shared);
        return shared with { MaxSteps = ClimbSteps, StallSteps = StallSteps };
    }

    /// <summary>
    /// Throws <see cref="ArgumentOutOfRangeException"/> naming the first value out of range. <see cref="GameConfig.CheckRanges"/>
    /// calls it at start-up.
    /// </summary>
    public void CheckRanges()
    {
        Check(MinSide, 2, int.MaxValue, "infinite.minSide");
        Check(MaxSide, MinSide, 1000, "infinite.maxSide");
        if (Difficulties.Count != DifficultyCount)
        {
            throw new ArgumentOutOfRangeException("infinite.difficulties", Difficulties.Count, $"There must be exactly {DifficultyCount}.");
        }
        for (var index = 0; index < Difficulties.Count; index++)
        {
            var name = $"infinite.difficulties[{index}]";
            var difficulty = Difficulties[index];
            Check(difficulty.MinScore, 0, 100, $"{name}.minScore");
            Check(difficulty.MaxScore, difficulty.MinScore, 100, $"{name}.maxScore");
            CheckSides(difficulty.Plain, $"{name}.plain");
            CheckSides(difficulty.Twists, $"{name}.twists");
        }
        Check(StartDifficulty, 1, DifficultyCount, "infinite.startDifficulty");
        var start = Difficulty(StartDifficulty).Plain;
        Check(StartSide, start.MinSide, start.MaxSide, "infinite.startSide");
        Check(MinPar, 1, int.MaxValue, "infinite.minPar");
        Check(MaxPar, MinPar, int.MaxValue, "infinite.maxPar");
        // The climber's own bounds, checked here so an error names infinite's numbers rather than the climb group's
        Check(ClimbSteps, 1, 10_000_000, "infinite.climbSteps");
        Check(StallSteps, 1, ClimbSteps, "infinite.stallSteps");
        Check(WallPercent, 0, 100, "infinite.wallPercent");
        Check(TrapPercent, 0, 100, "infinite.trapPercent");
        Check(TeleporterPercent, 0, 100, "infinite.teleporterPercent");
        if (!double.IsFinite(LoadingDelaySeconds) || LoadingDelaySeconds < 0 || LoadingDelaySeconds > 10)
        {
            throw new ArgumentOutOfRangeException("infinite.loadingDelaySeconds", LoadingDelaySeconds, "Must be from 0 to 10 seconds.");
        }
    }

    private void CheckSides(SideRange range, string name)
    {
        Check(range.MinSide, MinSide, MaxSide, $"{name}.minSide");
        Check(range.MaxSide, range.MinSide, MaxSide, $"{name}.maxSide");
    }

    private static void Check(int value, int min, int max, string name)
    {
        if (value < min || value > max)
        {
            throw new ArgumentOutOfRangeException(name, value, max == int.MaxValue ? $"Must be at least {min}." : $"Must be from {min} to {max}.");
        }
    }
}

/// <summary>One difficulty: its score band in infinite mode's weights, and the sizes it allows.</summary>
public sealed record InfiniteDifficulty
{
    public required int MinScore { get; init; }

    public required int MaxScore { get; init; }

    // While walls, traps and teleporters are all off
    public required SideRange Plain { get; init; }

    // While any of walls, traps or teleporters is on
    public required SideRange Twists { get; init; }
}

public sealed record SideRange
{
    public required int MinSide { get; init; }

    public required int MaxSide { get; init; }
}
