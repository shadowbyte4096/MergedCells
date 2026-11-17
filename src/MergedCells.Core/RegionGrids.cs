namespace MergedCells.Core;

// The level editor's region bookkeeping: an edit changes a grid copy freely, and these turn the copy back into a valid map
internal static class RegionGrids
{
    // Labels each edge-joined run of equal IDs 1, 2, 3… in reading order of its first square. A block left in two pieces
    // becomes two blocks, and equal layouts give equal grids whatever their old numbers
    internal static int[,] Normalise(int[,] grid)
    {
        var rows = grid.GetLength(0);
        var columns = grid.GetLength(1);
        var labels = new int[rows, columns];
        var next = 0;
        var queue = new Queue<Cell>();
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                if (labels[row, column] != 0)
                {
                    continue;
                }
                next++;
                labels[row, column] = next;
                queue.Enqueue(new Cell(row, column));
                while (queue.TryDequeue(out var cell))
                {
                    foreach (var direction in Solver.Directions)
                    {
                        var neighbour = cell.Step(direction);
                        if (Map.IsInside(neighbour, rows, columns)
                            && labels[neighbour.Row, neighbour.Column] == 0
                            && grid[neighbour.Row, neighbour.Column] == grid[cell.Row, cell.Column])
                        {
                            labels[neighbour.Row, neighbour.Column] = next;
                            queue.Enqueue(neighbour);
                        }
                    }
                }
            }
        }
        return labels;
    }

    internal static Map Rebuild(int[,] regions, Tile[,]? tiles, IReadOnlyList<TeleporterPair> pairs, Cell start, Cell end) =>
        Map.Create(Normalise(regions), start, end, tiles, pairs);

    // An ID no square of the grid has yet, for a square that must stand alone
    internal static int FreshId(int[,] grid)
    {
        var highest = 0;
        foreach (var id in grid)
        {
            highest = Math.Max(highest, id);
        }
        return highest + 1;
    }

    // The same blocks, twists, pairs in the same order, start and end
    internal static bool SameLevel(Map a, Map b)
    {
        if (a.Rows != b.Rows || a.Columns != b.Columns || a.Start != b.Start || a.End != b.End || !a.Teleporters.SequenceEqual(b.Teleporters))
        {
            return false;
        }
        var regionsA = Normalise(a.RegionGrid());
        var regionsB = Normalise(b.RegionGrid());
        for (var row = 0; row < a.Rows; row++)
        {
            for (var column = 0; column < a.Columns; column++)
            {
                var cell = new Cell(row, column);
                if (regionsA[row, column] != regionsB[row, column] || a.TileAt(cell) != b.TileAt(cell))
                {
                    return false;
                }
            }
        }
        return true;
    }
}
