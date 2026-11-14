namespace MergedCells.Core;

// Every square the player can stop on, where each of its four moves leads, and how far each is from winning. Built in one
// breadth-first pass from the start that makes each slide once, so the stops are in order of distance from the start
internal sealed class MoveGraph
{
    // A move that doesn't happen (a no-op, a looping slide, a round trip back to the same square), or a dead end's moves to
    // the end
    public const int None = -1;
    public const int Win = -2;

    // Four per stop, in Solver.Directions order: the stop the move lands on, Win or None
    private readonly int[] moves;
    private readonly Cell[] cells;
    private readonly int[] fromStart;
    private readonly int[] toEnd;

    private MoveGraph(Cell[] cells, int[] moves, int[] fromStart, int loopingSlides)
    {
        this.cells = cells;
        this.moves = moves;
        this.fromStart = fromStart;
        LoopingSlides = loopingSlides;
        toEnd = MovesToEnd(cells.Length, moves);
    }

    public int StopCount => cells.Length;

    // Slides from the start and every other reachable stop that would never stop
    public int LoopingSlides { get; }

    public Cell CellOf(int stop) => cells[stop];

    public int Move(int stop, int direction) => moves[stop * 4 + direction];

    public int DistanceFromStart(int stop) => fromStart[stop];

    public int MovesToEnd(int stop) => toEnd[stop];

    public static MoveGraph Build(Cell start, Func<Cell, Direction, SlideResult> slide)
    {
        ArgumentNullException.ThrowIfNull(slide);

        var cells = new List<Cell> { start };
        var indexOf = new Dictionary<Cell, int> { [start] = 0 };
        var fromStart = new List<int> { 0 };
        var moves = new List<int>();
        var loopingSlides = 0;
        for (var stop = 0; stop < cells.Count; stop++)
        {
            foreach (var direction in Solver.Directions)
            {
                var result = slide(cells[stop], direction);
                if (result.Looped)
                {
                    loopingSlides++;
                    moves.Add(None);
                    continue;
                }
                if (result.ReachedEnd)
                {
                    moves.Add(Win);
                    continue;
                }
                if (result.Stop == cells[stop])
                {
                    moves.Add(None);
                    continue;
                }
                if (!indexOf.TryGetValue(result.Stop, out var next))
                {
                    next = cells.Count;
                    indexOf[result.Stop] = next;
                    cells.Add(result.Stop);
                    fromStart.Add(fromStart[stop] + 1);
                }
                moves.Add(next);
            }
        }
        return new MoveGraph([.. cells], [.. moves], [.. fromStart], loopingSlides);
    }

    // A breadth-first search back from the stops with a winning move, over the moves reversed
    private static int[] MovesToEnd(int stopCount, int[] moves)
    {
        // Each stop's predecessors as one flat array: those of stop s are from firstPredecessor[s] up to firstPredecessor[s + 1]
        var firstPredecessor = new int[stopCount + 1];
        foreach (var target in moves)
        {
            if (target >= 0)
            {
                firstPredecessor[target + 1]++;
            }
        }
        for (var stop = 0; stop < stopCount; stop++)
        {
            firstPredecessor[stop + 1] += firstPredecessor[stop];
        }
        var predecessors = new int[firstPredecessor[stopCount]];
        var filled = (int[])firstPredecessor.Clone();
        for (var index = 0; index < moves.Length; index++)
        {
            if (moves[index] >= 0)
            {
                predecessors[filled[moves[index]]++] = index / 4;
            }
        }

        var toEnd = new int[stopCount];
        Array.Fill(toEnd, None);
        var queue = new Queue<int>();
        for (var stop = 0; stop < stopCount; stop++)
        {
            for (var direction = 0; direction < 4; direction++)
            {
                if (moves[stop * 4 + direction] == Win)
                {
                    toEnd[stop] = 1;
                    queue.Enqueue(stop);
                    break;
                }
            }
        }
        while (queue.Count > 0)
        {
            var stop = queue.Dequeue();
            for (var index = firstPredecessor[stop]; index < firstPredecessor[stop + 1]; index++)
            {
                var predecessor = predecessors[index];
                if (toEnd[predecessor] == None)
                {
                    toEnd[predecessor] = toEnd[stop] + 1;
                    queue.Enqueue(predecessor);
                }
            }
        }
        return toEnd;
    }
}
