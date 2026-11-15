namespace MergedCells.Core;

/// <summary>
/// What <see cref="MapClimber.Climb"/> aims at: a score from <paramref name="MinScore"/> to <paramref name="MaxScore"/>
/// (0 to 100, in the request's mode), optional minimums for any of the metrics, and, for twist chapters, how far the
/// twists must move par (<see cref="TwistReliance"/>).
/// </summary>
public sealed record DifficultyTarget(int MinScore, int MaxScore, MetricMinimums? Minimums = null, int MinTwistReliance = 0);

/// <summary>
/// The least each <see cref="DifficultyMetrics"/> count may be; null is no minimum. A metric that is null on a map (no
/// greedy route, no lookahead player finishing) is as hard as that metric gets, so it meets every minimum.
/// </summary>
public sealed record MetricMinimums(
    int? AwayMoves = null,
    int? GreedyGap = null,
    int? Decoys = null,
    long? NearOptimalRoutes = null,
    int? BruteForceExtraMoves = null);
