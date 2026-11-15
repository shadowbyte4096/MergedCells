namespace MergedCells.Core;

public static class Solver
{
    // The order the path tie-break reads moves in, and the order of each stop's moves in MoveGraph
    internal static readonly Direction[] Directions = [Direction.Up, Direction.Down, Direction.Left, Direction.Right];

    /// <summary>
    /// Searches every square the player can stop on, from the map's start, and returns par, the first optimal
    /// path in direction order, and the map's basic difficulty stats.
    /// </summary>
    public static SolveResult Solve(Map map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return Solve(map.Start, map.Slide);
    }

    // The test hook: tests pass in made-up slide rules here
    internal static SolveResult Solve(Cell start, Func<Cell, Direction, SlideResult> slide) => SolveGraph(MoveGraph.Build(start, slide));

    // The climber's way in: it builds a step's graph once and reads the solution and the metrics from it
    internal static SolveResult SolveGraph(MoveGraph graph)
    {
        var deadEnds = 0;
        for (var stop = 0; stop < graph.StopCount; stop++)
        {
            if (graph.MovesToEnd(stop) == MoveGraph.None)
            {
                deadEnds++;
            }
        }
        if (graph.MovesToEnd(0) == MoveGraph.None)
        {
            return new SolveResult(null, [], graph.StopCount, deadEnds, 0, graph.LoopingSlides);
        }

        // Stops are in distance order, so every route into a stop is counted before the stop passes its own on, and the
        // first winning move in stop and direction order is the path's last
        var routes = new long[graph.StopCount];
        routes[0] = 1;
        var parent = new (int Stop, int Direction)[graph.StopCount];
        Array.Fill(parent, (MoveGraph.None, 0));
        var first = (Stop: MoveGraph.None, Direction: 0);
        var optimalRoutes = 0L;
        for (var stop = 0; stop < graph.StopCount; stop++)
        {
            for (var direction = 0; direction < 4; direction++)
            {
                var next = graph.Move(stop, direction);
                if (next == MoveGraph.Win)
                {
                    if (first.Stop == MoveGraph.None)
                    {
                        first = (stop, direction);
                    }
                    if (graph.DistanceFromStart(stop) == graph.DistanceFromStart(first.Stop))
                    {
                        optimalRoutes = SaturatingAdd(optimalRoutes, routes[stop]);
                    }
                }
                else if (next >= 0 && graph.DistanceFromStart(next) == graph.DistanceFromStart(stop) + 1)
                {
                    routes[next] = SaturatingAdd(routes[next], routes[stop]);
                    if (parent[next].Stop == MoveGraph.None)
                    {
                        parent[next] = (stop, direction);
                    }
                }
            }
        }

        var path = new Direction[graph.DistanceFromStart(first.Stop) + 1];
        path[^1] = Directions[first.Direction];
        var step = path.Length - 2;
        for (var stop = first.Stop; stop != 0; stop = parent[stop].Stop)
        {
            path[step--] = Directions[parent[stop].Direction];
        }
        return new SolveResult(path.Length, path, graph.StopCount + 1, deadEnds, optimalRoutes, graph.LoopingSlides);
    }

    internal static long SaturatingAdd(long a, long b) => a > long.MaxValue - b ? long.MaxValue : a + b;
}
