namespace MergedCells.Core;

// Puts twists on a cut map: walls in short straight runs, then single sticky squares, single traps and teleporter pairs,
// all on squares no twist has used and never on the start or the end, so every placement is a valid map. A twist with a
// count of 0 draws no random numbers, so a plain map's numbers are untouched.
internal static class TwistPlacer
{
    public static (Tile[,]? Tiles, TeleporterPair[] Teleporters) Place(
        int size, Cell start, Cell end, TwistDensities twists, int maxWallRun, GameRandom random)
    {
        var squares = size * size;
        var free = new FreeSquares(size, start, end);
        Tile[,]? tiles = null;
        foreach (var run in WallRuns(free, Percentage.Of(twists.WallPercent, squares), maxWallRun, random))
        {
            foreach (var cell in run)
            {
                Put(ref tiles, size, cell, Tile.Wall);
            }
        }
        // Sticky squares that land side by side join into one pad, which the developer is happy to see now and then
        PlaceSingles(free, Percentage.Of(twists.StickyPercent, squares), Tile.Sticky, size, ref tiles, random);
        PlaceSingles(free, Percentage.Of(twists.TrapPercent, squares), Tile.Trap, size, ref tiles, random);

        // No more pairs than letters, so no two pairs on a map look alike
        var pairCount = Math.Min(TeleporterLetters.Count, (Percentage.Of(twists.TeleporterPercent, squares) + 1) / 2);
        var pairs = new List<TeleporterPair>(pairCount);
        while (pairs.Count < pairCount && free.Count >= 2)
        {
            var first = free.Take(random);
            pairs.Add(new TeleporterPair(first, free.Take(random)));
        }
        return (tiles, pairs.ToArray());
    }

    // Each run starts on a picked square and grows along its row or column as RegionCutter grows a strip: when both ends
    // can grow, a coin picks the lower (0) or the higher (1). The direction is drawn only for a run longer than 1
    public static List<Cell[]> WallRuns(FreeSquares free, int count, int maxWallRun, GameRandom random)
    {
        var runs = new List<Cell[]>();
        var walled = 0;
        while (walled < count && free.Count > 0)
        {
            var low = free.Take(random);
            var high = low;
            var have = 1;
            walled++;
            var length = random.Next(1, maxWallRun + 1);
            var down = length > 1 && random.Next(2) == 1;
            while (have < length && walled < count)
            {
                var beforeLow = low.Step(down ? Direction.Up : Direction.Left);
                var afterHigh = high.Step(down ? Direction.Down : Direction.Right);
                var canLow = free.Contains(beforeLow);
                var canHigh = free.Contains(afterHigh);
                if (!canLow && !canHigh)
                {
                    break;
                }
                if (canLow && (!canHigh || random.Next(2) == 0))
                {
                    free.Remove(beforeLow);
                    low = beforeLow;
                }
                else
                {
                    free.Remove(afterHigh);
                    high = afterHigh;
                }
                have++;
                walled++;
            }
            runs.Add(Line(low, have, down));
        }
        return runs;
    }

    private static Cell[] Line(Cell first, int length, bool down)
    {
        var line = new Cell[length];
        for (var i = 0; i < length; i++)
        {
            line[i] = down ? new Cell(first.Row + i, first.Column) : new Cell(first.Row, first.Column + i);
        }
        return line;
    }

    private static void PlaceSingles(FreeSquares free, int count, Tile tile, int size, ref Tile[,]? tiles, GameRandom random)
    {
        for (var placed = 0; placed < count && free.Count > 0; placed++)
        {
            Put(ref tiles, size, free.Take(random), tile);
        }
    }

    // The grid is made only once a tile is placed, so a map with only teleporters has no tiles
    private static void Put(ref Tile[,]? tiles, int size, Cell cell, Tile tile)
    {
        tiles ??= new Tile[size, size];
        tiles[cell.Row, cell.Column] = tile;
    }
}

// The squares no twist has used yet, starting with every square in reading order but the start and the end. A pick
// swaps the last square into the gap, so the order depends only on the picks, and a square a wall run grows into is
// removed the same way, so Count is always the squares left
internal sealed class FreeSquares
{
    private readonly int size;
    private readonly List<Cell> squares;
    // Each square's place in the list, -1 once it's used (and for the start and the end)
    private readonly int[] places;

    public FreeSquares(int size, Cell start, Cell end)
    {
        this.size = size;
        squares = new List<Cell>(size * size);
        places = new int[size * size];
        for (var row = 0; row < size; row++)
        {
            for (var column = 0; column < size; column++)
            {
                var cell = new Cell(row, column);
                if (cell == start || cell == end)
                {
                    places[row * size + column] = -1;
                    continue;
                }
                places[row * size + column] = squares.Count;
                squares.Add(cell);
            }
        }
    }

    public int Count => squares.Count;

    public bool Contains(Cell cell) => Map.IsInside(cell, size, size) && places[cell.Row * size + cell.Column] >= 0;

    public Cell Take(GameRandom random)
    {
        var cell = squares[random.Next(squares.Count)];
        Remove(cell);
        return cell;
    }

    public void Remove(Cell cell)
    {
        if (!Contains(cell))
        {
            throw new InvalidOperationException($"{cell} isn't a free square.");
        }
        var index = cell.Row * size + cell.Column;
        var place = places[index];
        var last = squares[^1];
        squares[place] = last;
        places[last.Row * size + last.Column] = place;
        squares.RemoveAt(squares.Count - 1);
        places[index] = -1;
    }
}
