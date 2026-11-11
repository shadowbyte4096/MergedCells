namespace MergedCells.Core;

internal static class MapValidator
{
    public static IReadOnlyList<MapProblem> Validate(int[,] regions, Cell start, Cell end, Tile[,]? tiles = null)
    {
        ArgumentNullException.ThrowIfNull(regions);
        var rows = regions.GetLength(0);
        var columns = regions.GetLength(1);
        if (rows == 0 || columns == 0)
        {
            return [new MapProblem(MapProblemKind.EmptyGrid, $"The grid is {rows}x{columns}; it needs at least one row and one column.")];
        }

        var problems = new List<MapProblem>();
        var bounds = RegionBounds.Measure(regions, out _);
        AddRegionProblems(problems, regions, bounds);
        AddStartAndEndProblems(problems, regions, bounds, start, end);
        if (tiles != null)
        {
            AddTileProblems(problems, tiles, rows, columns, start, end);
        }
        return problems;
    }

    // Regions are visited in reading order of their first square, so problems come out in first-seen order
    private static void AddRegionProblems(List<MapProblem> problems, int[,] regions, Dictionary<int, RegionBounds> bounds)
    {
        var reached = new bool[regions.GetLength(0), regions.GetLength(1)];
        var flooded = new HashSet<int>();
        for (var row = 0; row < regions.GetLength(0); row++)
        {
            for (var column = 0; column < regions.GetLength(1); column++)
            {
                var id = regions[row, column];
                if (!flooded.Add(id))
                {
                    continue;
                }
                var region = bounds[id];
                if (Flood(regions, reached, new Cell(row, column)) != region.Count)
                {
                    problems.Add(new MapProblem(
                        MapProblemKind.RegionNotConnected,
                        $"Region {id} is in more than one piece: its squares span rows {region.MinRow}-{region.MaxRow} and columns {region.MinColumn}-{region.MaxColumn}."));
                }
            }
        }
    }

    // Counts the squares joined edge to edge to the first one with the same region ID
    private static int Flood(int[,] regions, bool[,] reached, Cell first)
    {
        var id = regions[first.Row, first.Column];
        var rows = regions.GetLength(0);
        var columns = regions.GetLength(1);
        var queue = new Queue<Cell>();
        reached[first.Row, first.Column] = true;
        queue.Enqueue(first);
        var count = 0;
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            count++;
            foreach (var direction in Directions)
            {
                var next = cell.Step(direction);
                if (Map.IsInside(next, rows, columns) && !reached[next.Row, next.Column] && regions[next.Row, next.Column] == id)
                {
                    reached[next.Row, next.Column] = true;
                    queue.Enqueue(next);
                }
            }
        }
        return count;
    }

    private static readonly Direction[] Directions = [Direction.Up, Direction.Down, Direction.Left, Direction.Right];

    private static void AddStartAndEndProblems(
        List<MapProblem> problems, int[,] regions, Dictionary<int, RegionBounds> bounds, Cell start, Cell end)
    {
        var rows = regions.GetLength(0);
        var columns = regions.GetLength(1);
        var endInside = Map.IsInside(end, rows, columns);
        if (!Map.IsInside(start, rows, columns))
        {
            problems.Add(new MapProblem(MapProblemKind.StartOutsideMap, $"The start {Describe(start)} is outside the {rows}x{columns} grid."));
        }
        if (!endInside)
        {
            problems.Add(new MapProblem(MapProblemKind.EndOutsideMap, $"The end {Describe(end)} is outside the {rows}x{columns} grid."));
        }
        if (endInside && bounds[regions[end.Row, end.Column]].Count > 1)
        {
            problems.Add(new MapProblem(
                MapProblemKind.EndNotSingleSquare,
                $"The end {Describe(end)} shares region {regions[end.Row, end.Column]} with other squares; it must be its own 1x1 rectangle."));
        }
        if (start.Column > end.Column)
        {
            problems.Add(new MapProblem(MapProblemKind.StartRightOfEnd, $"The start {Describe(start)} is to the right of the end {Describe(end)}."));
        }
        if (start.Row > end.Row)
        {
            problems.Add(new MapProblem(MapProblemKind.StartBelowEnd, $"The start {Describe(start)} is below the end {Describe(end)}."));
        }
        if (start == end)
        {
            problems.Add(new MapProblem(MapProblemKind.StartOnEnd, $"The start and the end are the same square {Describe(start)}."));
        }
    }

    private static void AddTileProblems(List<MapProblem> problems, Tile[,] tiles, int rows, int columns, Cell start, Cell end)
    {
        if (tiles.GetLength(0) != rows || tiles.GetLength(1) != columns)
        {
            problems.Add(new MapProblem(
                MapProblemKind.TilesSizeMismatch,
                $"The tiles grid is {tiles.GetLength(0)}x{tiles.GetLength(1)}; the regions grid is {rows}x{columns}."));
            return;
        }
        if (FirstUndefined(tiles) is { } cell)
        {
            problems.Add(new MapProblem(
                MapProblemKind.UnknownTile, $"The tile at {Describe(cell)} is {(byte)tiles[cell.Row, cell.Column]}, which isn't a tile."));
        }
        if (Map.IsInside(start, rows, columns) && tiles[start.Row, start.Column] == Tile.Wall)
        {
            problems.Add(new MapProblem(MapProblemKind.WallOnStart, $"The start {Describe(start)} is a wall."));
        }
        if (Map.IsInside(end, rows, columns) && tiles[end.Row, end.Column] == Tile.Wall)
        {
            problems.Add(new MapProblem(MapProblemKind.WallOnEnd, $"The end {Describe(end)} is a wall."));
        }
        if (Map.IsInside(end, rows, columns) && tiles[end.Row, end.Column] == Tile.Sticky)
        {
            problems.Add(new MapProblem(MapProblemKind.StickyOnEnd, $"The end {Describe(end)} is a sticky square."));
        }
        if (Map.IsInside(start, rows, columns) && tiles[start.Row, start.Column] == Tile.Trap)
        {
            problems.Add(new MapProblem(MapProblemKind.TrapOnStart, $"The start {Describe(start)} is a trap."));
        }
        if (Map.IsInside(end, rows, columns) && tiles[end.Row, end.Column] == Tile.Trap)
        {
            problems.Add(new MapProblem(MapProblemKind.TrapOnEnd, $"The end {Describe(end)} is a trap."));
        }
    }

    private static Cell? FirstUndefined(Tile[,] tiles)
    {
        for (var row = 0; row < tiles.GetLength(0); row++)
        {
            for (var column = 0; column < tiles.GetLength(1); column++)
            {
                if (!Enum.IsDefined(tiles[row, column]))
                {
                    return new Cell(row, column);
                }
            }
        }
        return null;
    }

    private static string Describe(Cell cell) => $"[{cell.Row}, {cell.Column}]";
}
