namespace MergedCells.Core;

public sealed class SolveResult
{
    internal SolveResult(int? par, Direction[] path, int reachableStops, int deadEnds, long optimalRoutes)
    {
        Par = par;
        Path = Array.AsReadOnly(path);
        ReachableStops = reachableStops;
        DeadEnds = deadEnds;
        OptimalRoutes = optimalRoutes;
    }

    public bool IsSolvable => Par is not null;

    public int? Par { get; }

    /// <summary>
    /// The moves of an optimal route. When several routes tie, this is the first one reading Up before Down
    /// before Left before Right. Empty when the map can't be solved.
    /// </summary>
    public IReadOnlyList<Direction> Path { get; }

    /// <summary>The number of squares the player can stop on, counting the start, and the end when the map is solvable.</summary>
    public int ReachableStops { get; }

    /// <summary>Reachable squares, other than the end, from which no sequence of moves wins.</summary>
    public int DeadEnds { get; }

    /// <summary>The number of move sequences that win in <see cref="Par"/> moves, capped at <see cref="long.MaxValue"/>.</summary>
    public long OptimalRoutes { get; }
}
