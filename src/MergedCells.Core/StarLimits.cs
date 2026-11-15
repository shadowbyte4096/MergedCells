namespace MergedCells.Core;

/// <summary>
/// Star limits a player can reach without finding par (chunk 12's rule): a 2-star limit that only one route meets feels
/// impossible, so it's set at the first length by which enough other winning routes finish.
/// </summary>
public static class StarLimits
{
    /// <summary>
    /// Counts the move sequences that win in par + 1 up to par + <paramref name="maxExtraMoves"/>, each ending at its
    /// first win, and gives the 2-star limit at the shortest length whose running total reaches
    /// <paramref name="minRoutes"/>, and the 1-star limit <paramref name="oneStarExtraMoves"/> past it, both as fixed
    /// moves over par. Null when the total never gets there. Throws <see cref="ArgumentOutOfRangeException"/> for
    /// <paramref name="minRoutes"/> below 1, <paramref name="maxExtraMoves"/> outside 1 to 20 or
    /// <paramref name="oneStarExtraMoves"/> below 0, and <see cref="ArgumentException"/> when the map can't be won.
    /// </summary>
    public static StarsConfig? Reachable(Map map, int minRoutes, int maxExtraMoves, int oneStarExtraMoves)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentOutOfRangeException.ThrowIfLessThan(minRoutes, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxExtraMoves, 1);
        // The count's work grows with par plus this, and 20 is far past any star limit a level would want
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxExtraMoves, 20);
        ArgumentOutOfRangeException.ThrowIfNegative(oneStarExtraMoves);

        var graph = MoveGraph.Build(map.Start, map.Slide);
        if (graph.MovesToEnd(0) == MoveGraph.None)
        {
            throw new ArgumentException("The map can't be won.", nameof(map));
        }
        var total = 0L;
        var wins = RouteMetrics.WinsByLength(graph, maxExtraMoves);
        for (var extra = 1; extra <= maxExtraMoves; extra++)
        {
            total = Solver.SaturatingAdd(total, wins[extra - 1]);
            if (total >= minRoutes)
            {
                return new StarsConfig
                {
                    TwoStarMinExtraMoves = extra,
                    TwoStarExtraParPercent = 0,
                    OneStarMinExtraMoves = extra + oneStarExtraMoves,
                    OneStarExtraParPercent = 0,
                };
            }
        }
        return null;
    }
}
