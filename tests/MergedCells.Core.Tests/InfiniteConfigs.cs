namespace MergedCells.Core.Tests;

// Infinite mode's numbers for tests, independent of the shipped file so tuning it never breaks them
internal static class InfiniteConfigs
{
    public static InfiniteConfig With(
        int minSide = 4,
        int maxSide = 30,
        int startDifficulty = 3,
        int startSide = 10,
        int minPar = 2,
        int maxPar = 1000,
        int climbSteps = 300,
        int stallSteps = 100,
        int wallPercent = 6,
        int trapPercent = 3,
        int teleporterPercent = 2,
        IReadOnlyList<InfiniteDifficulty>? difficulties = null,
        double loadingDelaySeconds = 0.25) => new()
    {
        MinSide = minSide,
        MaxSide = maxSide,
        StartDifficulty = startDifficulty,
        StartSide = startSide,
        MinPar = minPar,
        MaxPar = maxPar,
        ClimbSteps = climbSteps,
        StallSteps = stallSteps,
        WallPercent = wallPercent,
        TrapPercent = trapPercent,
        TeleporterPercent = teleporterPercent,
        LoadingDelaySeconds = loadingDelaySeconds,
        Difficulties = difficulties ?? Tenths(minSide, maxSide),
    };

    // Bands of ten score points, 90 to 100 last, each allowing every size
    public static IReadOnlyList<InfiniteDifficulty> Tenths(int minSide = 4, int maxSide = 30) =>
        Enumerable.Range(0, InfiniteConfig.DifficultyCount)
            .Select(index => Difficulty(index * 10, index == 9 ? 100 : index * 10 + 9, Range(minSide, maxSide), Range(minSide, maxSide)))
            .ToArray();

    // The tenths, with one difficulty replaced
    public static IReadOnlyList<InfiniteDifficulty> TenthsWith(int difficulty, InfiniteDifficulty replacement, int minSide = 4, int maxSide = 30)
    {
        var all = Tenths(minSide, maxSide).ToArray();
        all[difficulty - 1] = replacement;
        return all;
    }

    public static InfiniteDifficulty Difficulty(int minScore, int maxScore, SideRange plain, SideRange twists) => new()
    {
        MinScore = minScore,
        MaxScore = maxScore,
        Plain = plain,
        Twists = twists,
    };

    public static SideRange Range(int min, int max) => new() { MinSide = min, MaxSide = max };
}
