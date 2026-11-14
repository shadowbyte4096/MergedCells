namespace MergedCells.Core;

// Chunk 12's brute-force players. A k-move player looks at every line of up to k moves and makes the first move of the
// line that wins soonest or, with no win in sight, that lands nearest the end; ties go to the first line in
// Solver.Directions order. It is deterministic, so a player that comes back to a square it has stood on never finishes
internal static class LookaheadPlayers
{
    private const int NoWin = int.MaxValue;

    // The fewest moves any 1- to lookahead-move player takes to win, minus par; null when none finishes
    public static int? ExtraMoves(MoveGraph graph, Cell end, int lookahead)
    {
        int? fewest = null;
        for (var depth = 1; depth <= lookahead; depth++)
        {
            if (Play(graph, end, depth) is { } moves && (fewest is null || moves < fewest))
            {
                fewest = moves;
            }
        }
        return fewest - graph.MovesToEnd(0);
    }

    private static int? Play(MoveGraph graph, Cell end, int depth)
    {
        var visited = new bool[graph.StopCount];
        var stop = 0;
        var moves = 0;
        while (true)
        {
            visited[stop] = true;
            var direction = Best(graph, end, stop, depth).Direction;
            if (direction == MoveGraph.None)
            {
                return null;
            }
            moves++;
            var next = graph.Move(stop, direction);
            if (next == MoveGraph.Win)
            {
                return moves;
            }
            if (visited[next])
            {
                return null;
            }
            stop = next;
        }
    }

    // The first move of the best line of up to depth moves, with how soon that line wins and where it lands
    private static (int Direction, int WinIn, int Distance) Best(MoveGraph graph, Cell end, int stop, int depth)
    {
        var best = (Direction: MoveGraph.None, WinIn: NoWin, Distance: int.MaxValue);
        for (var direction = 0; direction < 4; direction++)
        {
            var next = graph.Move(stop, direction);
            if (next == MoveGraph.None)
            {
                continue;
            }
            (int WinIn, int Distance) line;
            if (next == MoveGraph.Win)
            {
                line = (1, 0);
            }
            else if (depth == 1)
            {
                line = (NoWin, graph.CellOf(next).DistanceTo(end));
            }
            else
            {
                // A line ends early on a square with no moves
                var rest = Best(graph, end, next, depth - 1);
                line = rest.Direction == MoveGraph.None ? (NoWin, graph.CellOf(next).DistanceTo(end))
                    : rest.WinIn == NoWin ? (NoWin, rest.Distance)
                    : (rest.WinIn + 1, 0);
            }
            if (line.WinIn < best.WinIn || (line.WinIn == best.WinIn && line.Distance < best.Distance))
            {
                best = (direction, line.WinIn, line.Distance);
            }
        }
        return best;
    }
}
