namespace MergedCells.Core;

public static class Solver
{
    // The order the path tie-break reads moves in
    private static readonly Direction[] Directions = [Direction.Up, Direction.Down, Direction.Left, Direction.Right];

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
    internal static SolveResult Solve(Cell start, Func<Cell, Direction, SlideResult> slide)
    {
        ArgumentNullException.ThrowIfNull(slide);

        var cells = new List<Cell> { start };
        var indexOf = new Dictionary<Cell, int> { [start] = 0 };
        var distance = new List<int> { 0 };
        var parent = new List<(int Node, Direction Direction)> { (-1, default) };
        var routes = new List<long> { 1 };
        var predecessors = new List<List<int>> { new() };
        var wins = new List<(int Node, Direction Direction)>();
        var loopingSlides = 0;

        // Nodes are listed in distance order, so every route into a node is counted before the node itself is expanded
        for (var node = 0; node < cells.Count; node++)
        {
            foreach (var direction in Directions)
            {
                var result = slide(cells[node], direction);
                if (result.Looped)
                {
                    loopingSlides++;
                    continue;
                }
                if (result.ReachedEnd)
                {
                    wins.Add((node, direction));
                    continue;
                }
                if (result.Stop == cells[node])
                {
                    continue;
                }
                if (!indexOf.TryGetValue(result.Stop, out var next))
                {
                    next = cells.Count;
                    indexOf[result.Stop] = next;
                    cells.Add(result.Stop);
                    distance.Add(distance[node] + 1);
                    parent.Add((node, direction));
                    routes.Add(0);
                    predecessors.Add(new());
                }
                if (distance[next] == distance[node] + 1)
                {
                    routes[next] = SaturatingAdd(routes[next], routes[node]);
                }
                predecessors[next].Add(node);
            }
        }

        var deadEnds = CountDeadEnds(cells.Count, predecessors, wins);
        if (wins.Count == 0)
        {
            return new SolveResult(null, [], cells.Count, deadEnds, 0, loopingSlides);
        }

        var first = wins[0];
        var optimalRoutes = 0L;
        foreach (var win in wins)
        {
            if (distance[win.Node] == distance[first.Node])
            {
                optimalRoutes = SaturatingAdd(optimalRoutes, routes[win.Node]);
            }
        }

        var path = new Direction[distance[first.Node] + 1];
        path[^1] = first.Direction;
        var step = path.Length - 2;
        for (var node = first.Node; node != 0; node = parent[node].Node)
        {
            path[step--] = parent[node].Direction;
        }
        return new SolveResult(path.Length, path, cells.Count + 1, deadEnds, optimalRoutes, loopingSlides);
    }

    private static int CountDeadEnds(int nodeCount, List<List<int>> predecessors, List<(int Node, Direction Direction)> wins)
    {
        var canWin = new bool[nodeCount];
        var queue = new Queue<int>();
        foreach (var win in wins)
        {
            if (!canWin[win.Node])
            {
                canWin[win.Node] = true;
                queue.Enqueue(win.Node);
            }
        }

        var winners = queue.Count;
        while (queue.Count > 0)
        {
            foreach (var predecessor in predecessors[queue.Dequeue()])
            {
                if (!canWin[predecessor])
                {
                    canWin[predecessor] = true;
                    queue.Enqueue(predecessor);
                    winners++;
                }
            }
        }
        return nodeCount - winners;
    }

    private static long SaturatingAdd(long a, long b) => a > long.MaxValue - b ? long.MaxValue : a + b;
}
