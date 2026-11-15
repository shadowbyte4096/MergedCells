namespace MergedCells.Core;

// How far a map is from a target, in score points: 0 hits it. A metric's shortfall counts as its share of the metric's
// cap, the scale the score itself uses, and each move of missing twist reliance as a whole metric's share. Integer only
internal static class TargetDistance
{
    public static void Check(DifficultyTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentOutOfRangeException.ThrowIfNegative(target.MinScore, nameof(target.MinScore));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(target.MinScore, 100, nameof(target.MinScore));
        ArgumentOutOfRangeException.ThrowIfLessThan(target.MaxScore, target.MinScore, nameof(target.MaxScore));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(target.MaxScore, 100, nameof(target.MaxScore));
        ArgumentOutOfRangeException.ThrowIfNegative(target.MinTwistReliance, nameof(target.MinTwistReliance));
        if (target.Minimums is { } minimums)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(minimums.AwayMoves ?? 0, nameof(minimums.AwayMoves));
            ArgumentOutOfRangeException.ThrowIfNegative(minimums.GreedyGap ?? 0, nameof(minimums.GreedyGap));
            ArgumentOutOfRangeException.ThrowIfNegative(minimums.Decoys ?? 0, nameof(minimums.Decoys));
            ArgumentOutOfRangeException.ThrowIfNegative(minimums.NearOptimalRoutes ?? 0, nameof(minimums.NearOptimalRoutes));
            ArgumentOutOfRangeException.ThrowIfNegative(minimums.BruteForceExtraMoves ?? 0, nameof(minimums.BruteForceExtraMoves));
        }
    }

    public static int Of(DifficultyTarget target, DifficultyMetrics metrics, int score, int? twistReliance, DifficultyCaps caps)
    {
        var distance = (long)Math.Max(0, target.MinScore - score) + Math.Max(0, score - target.MaxScore);
        if (target.Minimums is { } minimums)
        {
            distance += Shortfall(minimums.AwayMoves, metrics.AwayMoves, caps.AwayMoves)
                + Shortfall(minimums.GreedyGap, metrics.GreedyGap, caps.GreedyGap)
                + Shortfall(minimums.Decoys, metrics.Decoys, caps.Decoys)
                + Shortfall(minimums.NearOptimalRoutes, metrics.NearOptimalRoutes, caps.NearOptimalRoutes)
                + Shortfall(minimums.BruteForceExtraMoves, metrics.BruteForceExtraMoves, caps.BruteForceExtraMoves);
        }
        if (twistReliance is { } reliance)
        {
            distance += Math.Max(0L, target.MinTwistReliance - reliance) * 100;
        }
        return (int)Math.Min(distance, int.MaxValue);
    }

    // Rounded up, so any shortfall costs at least a point. Clamped first, so a minimum far past the cap can't overflow
    private static long Shortfall(long? minimum, long? value, int cap)
    {
        if (minimum is not { } least || value is not { } count || count >= least)
        {
            return 0;
        }
        var shortfall = Math.Min(least - count, int.MaxValue);
        return (shortfall * 100 + cap - 1) / cap;
    }
}
