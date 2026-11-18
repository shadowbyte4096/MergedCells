namespace MergedCells.Core;

/// <summary>
/// Rates how hard a map feels: measures its difficulty metrics on the solver's move graph, and blends them into one score
/// with the config's weights. Immutable, so one rater can serve several threads.
/// </summary>
public sealed class DifficultyRater
{
    // Stored with every play record, so a refit can tell counts made under an older definition. Raise it whenever how any
    // metric is counted changes
    public const int MetricsVersion = 1;

    private const int MaxWeight = 10_000;

    private const int FullShare = 10_000;

    private readonly DifficultyConfig config;
    // Indexed by mode: its override if it has one, otherwise the shared weights
    private readonly DifficultyWeights[] weightsByMode;

    /// <summary>
    /// Throws <see cref="ArgumentOutOfRangeException"/> naming any config number out of range, each mode override's
    /// weights included, or a <c>modeWeights</c> key that isn't a mode; <see cref="ArgumentNullException"/> naming any
    /// missing part.
    /// </summary>
    public DifficultyRater(DifficultyConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(config.Caps, "difficulty.caps");
        ArgumentNullException.ThrowIfNull(config.Weights, "difficulty.weights");
        ArgumentNullException.ThrowIfNull(config.ModeWeights, "difficulty.modeWeights");
        // The count's work grows with par plus this, and 10 is far past "a few"
        CheckRange(config.NearOptimalExtraMoves, 1, 10, "difficulty.nearOptimalExtraMoves");
        // A k-move player looks at up to 4^k lines per move
        CheckRange(config.BruteForceLookahead, 1, 4, "difficulty.bruteForceLookahead");
        ArgumentOutOfRangeException.ThrowIfLessThan(config.Caps.AwayMoves, 1, "difficulty.caps.awayMoves");
        ArgumentOutOfRangeException.ThrowIfLessThan(config.Caps.GreedyGap, 1, "difficulty.caps.greedyGap");
        ArgumentOutOfRangeException.ThrowIfLessThan(config.Caps.Decoys, 1, "difficulty.caps.decoys");
        ArgumentOutOfRangeException.ThrowIfLessThan(config.Caps.NearOptimalRoutes, 1, "difficulty.caps.nearOptimalRoutes");
        ArgumentOutOfRangeException.ThrowIfLessThan(config.Caps.BruteForceExtraMoves, 1, "difficulty.caps.bruteForceExtraMoves");
        CheckWeights(config.Weights, "difficulty.weights");
        foreach (var key in config.ModeWeights.Keys)
        {
            if (!DifficultyConfig.IsModeKey(key))
            {
                throw new ArgumentOutOfRangeException("difficulty.modeWeights", DifficultyConfig.NotAModeMessage(key));
            }
        }
        foreach (var mode in Enum.GetValues<DifficultyMode>())
        {
            if (config.ModeWeights.TryGetValue(DifficultyConfig.KeyOf(mode), out var weights))
            {
                var name = $"difficulty.modeWeights.{DifficultyConfig.KeyOf(mode)}";
                ArgumentNullException.ThrowIfNull(weights, name);
                CheckWeights(weights, name);
            }
        }
        this.config = config;
        weightsByMode = Enum.GetValues<DifficultyMode>()
            .Select(mode => config.ModeWeights.TryGetValue(DifficultyConfig.KeyOf(mode), out var own) ? own : config.Weights)
            .ToArray();
    }

    /// <summary>Measures the map's difficulty metrics, or returns null when it can't be won.</summary>
    public DifficultyMetrics? Measure(Map map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return Measure(map.Start, map.End, map.Slide);
    }

    // The test hook, as Solver's: made-up slide rules, with the end's square for distances
    internal DifficultyMetrics? Measure(Cell start, Cell end, Func<Cell, Direction, SlideResult> slide) =>
        Measure(MoveGraph.Build(start, slide), end);

    // The climber's way in, with the graph its filter already built
    internal DifficultyMetrics? Measure(MoveGraph graph, Cell end)
    {
        if (graph.MovesToEnd(0) == MoveGraph.None)
        {
            return null;
        }
        var (awayMoves, retreat) = RouteMetrics.Backtracking(graph, end);
        return new DifficultyMetrics(
            graph.MovesToEnd(0),
            awayMoves,
            retreat,
            RouteMetrics.GreedyGap(graph, end),
            RouteMetrics.Decoys(graph, end),
            RouteMetrics.NearOptimalRoutes(graph, config.NearOptimalExtraMoves),
            LookaheadPlayers.ExtraMoves(graph, end, config.BruteForceLookahead));
    }

    /// <summary>
    /// Blends the metrics into a whole number from 0 to 100 with the mode's weights: each count up to its cap, as a share of
    /// the cap, averaged by weight. A missing greedy route or lookahead finish counts as the full share. Integer arithmetic
    /// only, so every device gives the same score. Throws <see cref="ArgumentOutOfRangeException"/> for a negative count or
    /// a mode that isn't defined, as stored metrics read back from a file could hold.
    /// </summary>
    public int Score(DifficultyMetrics metrics, DifficultyMode mode)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Not a difficulty mode.");
        }
        var weights = weightsByMode[(int)mode];
        var caps = config.Caps;
        var weighted = (long)weights.AwayMoves * Share(metrics.AwayMoves, caps.AwayMoves, nameof(metrics.AwayMoves))
            + (long)weights.GreedyGap * Share(metrics.GreedyGap, caps.GreedyGap, nameof(metrics.GreedyGap))
            + (long)weights.Decoys * Share(metrics.Decoys, caps.Decoys, nameof(metrics.Decoys))
            + (long)weights.NearOptimalRoutes * Share(metrics.NearOptimalRoutes, caps.NearOptimalRoutes, nameof(metrics.NearOptimalRoutes))
            + (long)weights.BruteForceExtraMoves * Share(metrics.BruteForceExtraMoves, caps.BruteForceExtraMoves, nameof(metrics.BruteForceExtraMoves));
        var total = (long)weights.AwayMoves + weights.GreedyGap + weights.Decoys + weights.NearOptimalRoutes + weights.BruteForceExtraMoves;
        return (int)(weighted / (total * 100));
    }

    private static long Share(long? count, int cap, string name)
    {
        if (count is not { } value)
        {
            return FullShare;
        }
        ArgumentOutOfRangeException.ThrowIfNegative(value, name);
        return Math.Min(value, cap) * FullShare / cap;
    }

    private static void CheckRange(int value, int min, int max, string name)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, min, name);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, max, name);
    }

    private static void CheckWeights(DifficultyWeights weights, string name)
    {
        CheckRange(weights.AwayMoves, 0, MaxWeight, $"{name}.awayMoves");
        CheckRange(weights.GreedyGap, 0, MaxWeight, $"{name}.greedyGap");
        CheckRange(weights.Decoys, 0, MaxWeight, $"{name}.decoys");
        CheckRange(weights.NearOptimalRoutes, 0, MaxWeight, $"{name}.nearOptimalRoutes");
        CheckRange(weights.BruteForceExtraMoves, 0, MaxWeight, $"{name}.bruteForceExtraMoves");
        if (weights.AwayMoves + weights.GreedyGap + weights.Decoys + weights.NearOptimalRoutes + weights.BruteForceExtraMoves < 1)
        {
            throw new ArgumentOutOfRangeException(name, "The weights must add up to at least 1.");
        }
    }
}
