namespace MergedCells.Core;

// The order MapClimber's edit weights are read in
internal enum ClimbEdit
{
    Split,
    Merge,
    MoveSquare,
    MoveStart,
    MoveTwist,
}

// The climber's small edits. Each draws its random parts from the climb's stream and returns false when they give it
// nothing to do; the climber then throws the copy away. Only valid maps come out: the end and its square never change,
// the start stays in its area off every twist, and twists only move, onto free squares, so their counts and the pairs'
// letters never change. A skeleton's fixed regions and tiles are never touched: each refusal comes straight after the
// draw it depends on, so a map without a skeleton draws and does what it always has
internal static class ClimbEdits
{
    public static bool Apply(ClimbEdit edit, ClimbMap map, GameRandom random, IReadOnlyList<Cell> startArea) => edit switch
    {
        ClimbEdit.Split => Split(map, random),
        ClimbEdit.Merge => Merge(map, random),
        ClimbEdit.MoveSquare => MoveSquare(map, random),
        ClimbEdit.MoveStart => MoveStart(map, random, startArea),
        ClimbEdit.MoveTwist => MoveTwist(map, random),
        _ => throw new ArgumentOutOfRangeException(nameof(edit), edit, "Not a climb edit."),
    };

    // One straight cut between two of the region's rows or two of its columns; every connected piece on either side
    // becomes its own region
    public static bool Split(ClimbMap map, GameRandom random)
    {
        var square = RandomSquare(map, random);
        if (square == map.End || map.HasFixedRegion(square))
        {
            return false;
        }
        var region = map.RegionAt(square);
        int minRow = int.MaxValue, maxRow = int.MinValue, minColumn = int.MaxValue, maxColumn = int.MinValue;
        foreach (var cell in map.Cells())
        {
            if (map.RegionAt(cell) == region)
            {
                minRow = Math.Min(minRow, cell.Row);
                maxRow = Math.Max(maxRow, cell.Row);
                minColumn = Math.Min(minColumn, cell.Column);
                maxColumn = Math.Max(maxColumn, cell.Column);
            }
        }
        var canCutRows = maxRow > minRow;
        var canCutColumns = maxColumn > minColumn;
        if (!canCutRows && !canCutColumns)
        {
            return false;
        }
        var betweenRows = canCutRows && (!canCutColumns || random.Next(2) == 0);
        // The cut runs after this row or column
        var cut = betweenRows ? minRow + random.Next(maxRow - minRow) : minColumn + random.Next(maxColumn - minColumn);
        bool Side(Cell cell) => (betweenRows ? cell.Row : cell.Column) <= cut;

        var next = map.RegionCount + 1;
        foreach (var cell in map.Cells())
        {
            if (map.RegionAt(cell) == region)
            {
                Fill(map, cell, region, next++, other => Side(other) == Side(cell));
            }
        }
        map.Renumber();
        return true;
    }

    // A square's region and its neighbour's in a random direction become one
    public static bool Merge(ClimbMap map, GameRandom random)
    {
        var (square, neighbour) = RandomNeighbours(map, random);
        if (square == map.End || !map.Contains(neighbour) || neighbour == map.End
            || map.HasFixedRegion(square) || map.HasFixedRegion(neighbour))
        {
            return false;
        }
        var kept = map.RegionAt(square);
        var joined = map.RegionAt(neighbour);
        if (kept == joined)
        {
            return false;
        }
        foreach (var cell in map.Cells())
        {
            if (map.RegionAt(cell) == joined)
            {
                map.Regions[cell.Row, cell.Column] = kept;
            }
        }
        map.Renumber();
        return true;
    }

    // A square joins its neighbour's region in a random direction, so long as the region it leaves stays in one piece
    public static bool MoveSquare(ClimbMap map, GameRandom random)
    {
        var (square, neighbour) = RandomNeighbours(map, random);
        if (square == map.End || !map.Contains(neighbour) || neighbour == map.End
            || map.HasFixedRegion(square) || map.HasFixedRegion(neighbour))
        {
            return false;
        }
        var left = map.RegionAt(square);
        var joined = map.RegionAt(neighbour);
        if (left == joined)
        {
            return false;
        }
        map.Regions[square.Row, square.Column] = joined;
        if (!IsWhole(map, left))
        {
            map.Regions[square.Row, square.Column] = left;
            return false;
        }
        map.Renumber();
        return true;
    }

    public static bool MoveStart(ClimbMap map, GameRandom random, IReadOnlyList<Cell> startArea)
    {
        var free = startArea.Where(map.CanHoldStart).ToList();
        if (free.Count == 0)
        {
            return false;
        }
        map.Start = free[random.Next(free.Count)];
        return true;
    }

    // A random twist square moves: a wall its whole straight run, a sticky square or trap alone, a teleporter its own end
    public static bool MoveTwist(ClimbMap map, GameRandom random)
    {
        var twists = map.Cells()
            .Where(cell => !map.HasFixedTile(cell) && (map.Tiles[cell.Row, cell.Column] != Tile.None || map.IsTeleporter(cell)))
            .ToList();
        if (twists.Count == 0)
        {
            return false;
        }
        var square = twists[random.Next(twists.Count)];
        return map.Tiles[square.Row, square.Column] switch
        {
            Tile.None => MoveTeleporter(map, square, random),
            Tile.Wall => MoveWallRun(map, square, random),
            var tile => MoveSingle(map, square, tile, random),
        };
    }

    private static bool MoveTeleporter(ClimbMap map, Cell square, GameRandom random)
    {
        if (RandomFree(map, random) is not { } target)
        {
            return false;
        }
        var index = map.Teleporters.FindIndex(pair => pair.First == square || pair.Second == square);
        var pair = map.Teleporters[index];
        map.Teleporters[index] = pair.First == square ? pair with { First = target } : pair with { Second = target };
        return true;
    }

    private static bool MoveSingle(ClimbMap map, Cell square, Tile tile, GameRandom random)
    {
        if (RandomFree(map, random) is not { } target)
        {
            return false;
        }
        map.Tiles[square.Row, square.Column] = Tile.None;
        map.Tiles[target.Row, target.Column] = tile;
        return true;
    }

    // The longest straight line of walls through the square, across on a tie, moves with its length and direction kept
    private static bool MoveWallRun(ClimbMap map, Cell square, GameRandom random)
    {
        var across = Run(map, square, Direction.Left, Direction.Right);
        var down = Run(map, square, Direction.Up, Direction.Down);
        var isAcross = across.Count >= down.Count;
        var run = isAcross ? across : down;
        foreach (var cell in run)
        {
            map.Tiles[cell.Row, cell.Column] = Tile.None;
        }

        var row = random.Next(isAcross ? map.Size : map.Size - run.Count + 1);
        var column = random.Next(isAcross ? map.Size - run.Count + 1 : map.Size);
        var placed = Enumerable.Range(0, run.Count)
            .Select(i => isAcross ? new Cell(row, column + i) : new Cell(row + i, column))
            .ToList();
        var target = placed.All(map.IsFree) ? placed : run;
        foreach (var cell in target)
        {
            map.Tiles[cell.Row, cell.Column] = Tile.Wall;
        }
        return target == placed;
    }

    // A skeleton's fixed walls never move, so a run beside one stops short of it
    private static List<Cell> Run(ClimbMap map, Cell square, Direction back, Direction forward)
    {
        bool Movable(Cell cell) => map.Contains(cell) && map.Tiles[cell.Row, cell.Column] == Tile.Wall && !map.HasFixedTile(cell);

        var first = square;
        while (Movable(first.Step(back)))
        {
            first = first.Step(back);
        }
        var run = new List<Cell>();
        for (var cell = first; Movable(cell); cell = cell.Step(forward))
        {
            run.Add(cell);
        }
        return run;
    }

    private static Cell RandomSquare(ClimbMap map, GameRandom random)
    {
        var index = random.Next(map.Size * map.Size);
        return new Cell(index / map.Size, index % map.Size);
    }

    private static (Cell Square, Cell Neighbour) RandomNeighbours(ClimbMap map, GameRandom random)
    {
        var square = RandomSquare(map, random);
        return (square, square.Step(Solver.Directions[random.Next(4)]));
    }

    private static Cell? RandomFree(ClimbMap map, GameRandom random)
    {
        var free = map.Cells().Where(map.IsFree).ToList();
        return free.Count == 0 ? null : free[random.Next(free.Count)];
    }

    // Gives every square of the region connected to the first, and accepted, the new ID
    private static void Fill(ClimbMap map, Cell first, int region, int id, Func<Cell, bool> accepts)
    {
        var queue = new Queue<Cell>();
        map.Regions[first.Row, first.Column] = id;
        queue.Enqueue(first);
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            foreach (var direction in Solver.Directions)
            {
                var next = cell.Step(direction);
                if (map.Contains(next) && map.RegionAt(next) == region && accepts(next))
                {
                    map.Regions[next.Row, next.Column] = id;
                    queue.Enqueue(next);
                }
            }
        }
    }

    // True when the region is in one piece, or has no squares left
    private static bool IsWhole(ClimbMap map, int region)
    {
        var squares = map.Cells().Where(cell => map.RegionAt(cell) == region).ToList();
        if (squares.Count == 0)
        {
            return true;
        }
        var seen = new HashSet<Cell> { squares[0] };
        var queue = new Queue<Cell>();
        queue.Enqueue(squares[0]);
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            foreach (var direction in Solver.Directions)
            {
                var next = cell.Step(direction);
                if (map.Contains(next) && map.RegionAt(next) == region && seen.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }
        return seen.Count == squares.Count;
    }
}
