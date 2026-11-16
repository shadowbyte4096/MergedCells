namespace MergedCells.Core;

// The shapes MapGenerator makes, so a climbed map still looks generated (chunk 18's "long and thin"): the end its own
// square; every other region a strip one square wide up to the strip cap, a small block, or, when bends are on, a thin
// bend that isn't a rectangle, holds no 2x2 square and is at most maxBendSquares. Every generated map passes. A skeleton's
// fixed regions are skipped
internal static class MapShapes
{
    public static bool Fits(int[,] regions, Cell end, GeneratorConfig config, bool bends, MapSkeleton? skeleton = null)
    {
        var size = regions.GetLength(0);
        var stripCap = Math.Max(2, Percentage.Of(config.MaxStripPercent, size));

        // Regions as dense indices in reading order of their first square; the dictionary is only looked up, never walked
        var indexOf = new Dictionary<int, int>();
        var shapes = new List<Shape>();
        var index = new int[size, size];
        for (var row = 0; row < size; row++)
        {
            for (var column = 0; column < size; column++)
            {
                if (!indexOf.TryGetValue(regions[row, column], out var region))
                {
                    region = shapes.Count;
                    indexOf[regions[row, column]] = region;
                    shapes.Add(new Shape(row, column));
                }
                shapes[region].Include(row, column);
                index[row, column] = region;
            }
        }
        for (var row = 0; row + 1 < size; row++)
        {
            for (var column = 0; column + 1 < size; column++)
            {
                var region = index[row, column];
                if (index[row, column + 1] == region && index[row + 1, column] == region && index[row + 1, column + 1] == region)
                {
                    shapes[region].HasSquareOfFour = true;
                }
            }
        }

        for (var region = 0; region < shapes.Count; region++)
        {
            var shape = shapes[region];
            if (skeleton?.HasFixedRegion(new Cell(shape.FirstRow, shape.FirstColumn)) == true)
            {
                continue;
            }
            if (region == index[end.Row, end.Column])
            {
                if (shape.Count != 1)
                {
                    return false;
                }
                continue;
            }
            if (shape.IsRectangle)
            {
                var thin = Math.Min(shape.Height, shape.Width) == 1;
                if (thin ? Math.Max(shape.Height, shape.Width) > stripCap : Math.Max(shape.Height, shape.Width) > config.MaxBlockSide)
                {
                    return false;
                }
                continue;
            }
            if (!bends || shape.Count > config.MaxBendSquares || shape.HasSquareOfFour || !IsConnected(index, region, shape))
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsConnected(int[,] index, int region, Shape shape)
    {
        var size = index.GetLength(0);
        var seen = new bool[size, size];
        var queue = new Queue<Cell>();
        queue.Enqueue(new Cell(shape.FirstRow, shape.FirstColumn));
        seen[shape.FirstRow, shape.FirstColumn] = true;
        var reached = 0;
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            reached++;
            foreach (var direction in Solver.Directions)
            {
                var next = cell.Step(direction);
                if (Map.IsInside(next, size, size) && !seen[next.Row, next.Column] && index[next.Row, next.Column] == region)
                {
                    seen[next.Row, next.Column] = true;
                    queue.Enqueue(next);
                }
            }
        }
        return reached == shape.Count;
    }

    private sealed class Shape(int firstRow, int firstColumn)
    {
        private int minRow = firstRow;
        private int maxRow = firstRow;
        private int minColumn = firstColumn;
        private int maxColumn = firstColumn;

        public int FirstRow { get; } = firstRow;

        public int FirstColumn { get; } = firstColumn;

        public int Count { get; private set; }

        public bool HasSquareOfFour { get; set; }

        public int Height => maxRow - minRow + 1;

        public int Width => maxColumn - minColumn + 1;

        public bool IsRectangle => Count == Height * Width;

        public void Include(int row, int column)
        {
            Count++;
            minRow = Math.Min(minRow, row);
            maxRow = Math.Max(maxRow, row);
            minColumn = Math.Min(minColumn, column);
            maxColumn = Math.Max(maxColumn, column);
        }
    }
}
