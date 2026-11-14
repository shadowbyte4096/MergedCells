namespace MergedCells.Core.Tests;

// The rater's numbers for tests, independent of the shipped file so tuning it never breaks them
internal static class DifficultyConfigs
{
    public static DifficultyConfig With(
        int nearOptimalExtraMoves = 2,
        int bruteForceLookahead = 3,
        DifficultyCaps? caps = null,
        DifficultyWeights? weights = null,
        IReadOnlyDictionary<string, DifficultyWeights>? modeWeights = null) => new()
    {
        NearOptimalExtraMoves = nearOptimalExtraMoves,
        BruteForceLookahead = bruteForceLookahead,
        Caps = caps ?? Caps(),
        Weights = weights ?? Weights(),
        ModeWeights = modeWeights ?? new Dictionary<string, DifficultyWeights>(),
    };

    public static DifficultyCaps Caps(
        int awayMoves = 3, int greedyGap = 4, int decoys = 8, int nearOptimalRoutes = 100, int bruteForceExtraMoves = 5) => new()
    {
        AwayMoves = awayMoves,
        GreedyGap = greedyGap,
        Decoys = decoys,
        NearOptimalRoutes = nearOptimalRoutes,
        BruteForceExtraMoves = bruteForceExtraMoves,
    };

    public static DifficultyWeights Weights(
        int awayMoves = 100, int greedyGap = 100, int decoys = 100, int nearOptimalRoutes = 100, int bruteForceExtraMoves = 100) => new()
    {
        AwayMoves = awayMoves,
        GreedyGap = greedyGap,
        Decoys = decoys,
        NearOptimalRoutes = nearOptimalRoutes,
        BruteForceExtraMoves = bruteForceExtraMoves,
    };
}
