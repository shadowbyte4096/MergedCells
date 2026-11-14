namespace MergedCells.Core;

// The metrics measured along routes on a move graph that can be won. A move is measured by where it lands, a win by the end
internal static class RouteMetrics
{
    public static (int AwayMoves, int Retreat) Backtracking(MoveGraph graph, Cell end) =>
        (FewestAlongOptimalRoutes(graph, (stop, target) => Landing(graph, end, target) > Here(graph, end, stop) ? 1 : 0),
            FewestAlongOptimalRoutes(graph, (stop, target) => Math.Max(0, Landing(graph, end, target) - Here(graph, end, stop))));

    public static int Decoys(MoveGraph graph, Cell end) =>
        FewestAlongOptimalRoutes(graph, (stop, target) => DecoysBeside(graph, end, stop, Landing(graph, end, target)));

    // A breadth-first search from the start over moves that land no further from the end than they began
    public static int? GreedyGap(MoveGraph graph, Cell end)
    {
        var moves = new int[graph.StopCount];
        Array.Fill(moves, MoveGraph.None);
        moves[0] = 0;
        var queue = new Queue<int>();
        queue.Enqueue(0);
        while (queue.Count > 0)
        {
            var stop = queue.Dequeue();
            for (var direction = 0; direction < 4; direction++)
            {
                var target = graph.Move(stop, direction);
                if (target == MoveGraph.Win)
                {
                    return moves[stop] + 1 - graph.MovesToEnd(0);
                }
                if (target >= 0 && moves[target] == MoveGraph.None && Landing(graph, end, target) <= Here(graph, end, stop))
                {
                    moves[target] = moves[stop] + 1;
                    queue.Enqueue(target);
                }
            }
        }
        return null;
    }

    // Move sequences counted by length: those that end at each stop without having won, and the wins at each length
    public static long NearOptimalRoutes(MoveGraph graph, int extraMoves)
    {
        var par = graph.MovesToEnd(0);
        var endingAt = new long[graph.StopCount];
        var next = new long[graph.StopCount];
        endingAt[0] = 1;
        var near = 0L;
        for (var length = 1; length <= par + extraMoves; length++)
        {
            Array.Clear(next);
            var wins = 0L;
            for (var stop = 0; stop < graph.StopCount; stop++)
            {
                if (endingAt[stop] == 0)
                {
                    continue;
                }
                for (var direction = 0; direction < 4; direction++)
                {
                    var target = graph.Move(stop, direction);
                    if (target == MoveGraph.Win)
                    {
                        wins = Solver.SaturatingAdd(wins, endingAt[stop]);
                    }
                    else if (target >= 0)
                    {
                        next[target] = Solver.SaturatingAdd(next[target], endingAt[stop]);
                    }
                }
            }
            if (length > par)
            {
                near = Solver.SaturatingAdd(near, wins);
            }
            (endingAt, next) = (next, endingAt);
        }
        return near;
    }

    // The other moves from a stop that aren't optimal but land no further from the end than the route's move
    private static int DecoysBeside(MoveGraph graph, Cell end, int stop, int routeLanding)
    {
        var decoys = 0;
        for (var direction = 0; direction < 4; direction++)
        {
            var target = graph.Move(stop, direction);
            if (target != MoveGraph.None && !IsOptimal(graph, stop, target) && Landing(graph, end, target) <= routeLanding)
            {
                decoys++;
            }
        }
        return decoys;
    }

    // The least total cost of any optimal route, the cost of each optimal move given by cost(stop, target). Stops are
    // taken in order of moves to the end, so each optimal move's target is done before the stop it's made from
    private static int FewestAlongOptimalRoutes(MoveGraph graph, Func<int, int, int> cost)
    {
        var fewest = new int[graph.StopCount];
        foreach (var stop in ByMovesToEnd(graph))
        {
            var best = int.MaxValue;
            for (var direction = 0; direction < 4; direction++)
            {
                var target = graph.Move(stop, direction);
                if (target != MoveGraph.None && IsOptimal(graph, stop, target))
                {
                    best = Math.Min(best, cost(stop, target) + (target == MoveGraph.Win ? 0 : fewest[target]));
                }
            }
            fewest[stop] = best;
        }
        return fewest[0];
    }

    // Every stop that can win, in order of moves to the end and then of stop, so the order never depends on anything else
    private static IEnumerable<int> ByMovesToEnd(MoveGraph graph) =>
        Enumerable.Range(0, graph.StopCount).Where(stop => graph.MovesToEnd(stop) != MoveGraph.None).OrderBy(graph.MovesToEnd);

    private static bool IsOptimal(MoveGraph graph, int stop, int target) =>
        target == MoveGraph.Win || (graph.MovesToEnd(target) != MoveGraph.None && graph.MovesToEnd(target) == graph.MovesToEnd(stop) - 1);

    private static int Landing(MoveGraph graph, Cell end, int target) =>
        target == MoveGraph.Win ? 0 : graph.CellOf(target).DistanceTo(end);

    private static int Here(MoveGraph graph, Cell end, int stop) => graph.CellOf(stop).DistanceTo(end);
}
