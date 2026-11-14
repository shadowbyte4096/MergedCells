namespace MergedCells.Core;

// Cuts a square grid into mostly thin regions: strips across and down, single squares and small blocks, placed from
// random squares. The end is its own 1x1 region before anything else is placed. Every random number is drawn in a
// fixed order, so one seed always gives one grid.
internal static class RegionCutter
{
    private const int Empty = -1;
    private const int EndRegion = 0;

    public static int[,] Cut(int size, Cell end, GeneratorConfig config, bool bends, GameRandom random)
    {
        var grid = new Grid(size);
        grid.Assign(end.Row * size + end.Column, grid.NewRegion(meantSingle: false));
        var stripCap = Math.Max(2, Percentage.Of(config.MaxStripPercent, size));
        Fill(grid, config, stripCap, random);
        FoldSqueezed(grid, stripCap, random);
        // Only when asked, so the bend numbers never change a plain map's random numbers
        if (bends)
        {
            Bend(grid, config, random);
        }
        return grid.Numbered();
    }

    // Joins neighbouring thin regions into L, T and zigzag shapes: at most maxBendSquares squares, never a rectangle (two
    // strips in line would only make a longer strip) and never holding a 2x2 square, so a bent region is one square wide
    private static void Bend(Grid grid, GeneratorConfig config, GameRandom random)
    {
        var squares = new List<int>[grid.Bounds.Count];
        for (var id = 0; id < squares.Length; id++)
        {
            squares[id] = [];
        }
        for (var square = 0; square < grid.Regions.Length; square++)
        {
            squares[grid.Regions[square]].Add(square);
        }

        // Each square's right neighbour, then its lower one, in reading order
        var pairs = new List<(int First, int Second)>();
        for (var square = 0; square < grid.Regions.Length; square++)
        {
            var (row, column) = grid.CellOf(square);
            if (column + 1 < grid.Size)
            {
                pairs.Add((square, square + 1));
            }
            if (row + 1 < grid.Size)
            {
                pairs.Add((square, square + grid.Size));
            }
        }
        random.Shuffle(pairs);

        foreach (var (a, b) in pairs)
        {
            var first = grid.Regions[a];
            var second = grid.Regions[b];
            if (first == second || first == EndRegion || second == EndRegion)
            {
                continue;
            }
            if (random.Next(100) >= config.BendPercent)
            {
                continue;
            }
            if (CanBend(grid, squares[first], squares[second], first, second, config.MaxBendSquares))
            {
                grid.Join(first, second, squares[second]);
                squares[first].AddRange(squares[second]);
                squares[second].Clear();
            }
        }
    }

    private static bool CanBend(Grid grid, List<int> firstSquares, List<int> secondSquares, int first, int second, int maxSquares)
    {
        var count = firstSquares.Count + secondSquares.Count;
        if (count > maxSquares)
        {
            return false;
        }
        var a = grid.Bounds[first];
        var b = grid.Bounds[second];
        var area = (Math.Max(a.MaxRow, b.MaxRow) - Math.Min(a.MinRow, b.MinRow) + 1)
            * (Math.Max(a.MaxColumn, b.MaxColumn) - Math.Min(a.MinColumn, b.MinColumn) + 1);
        if (area == count)
        {
            return false;
        }
        bool InUnion(int square) => grid.Regions[square] == first || grid.Regions[square] == second;
        foreach (var square in firstSquares.Concat(secondSquares))
        {
            var (row, column) = grid.CellOf(square);
            if (row + 1 < grid.Size && column + 1 < grid.Size
                && InUnion(square + 1) && InUnion(square + grid.Size) && InUnion(square + grid.Size + 1))
            {
                return false;
            }
        }
        return true;
    }

    // A strip or block squeezed to one square by its neighbours joins a neighbouring region it lines up with: one square
    // wide along that side, not the end, not meant to be single, and no longer than the strip cap once joined. So the
    // single-square weight means what it says
    private static void FoldSqueezed(Grid grid, int stripCap, GameRandom random)
    {
        var squeezed = new List<int>();
        for (var square = 0; square < grid.Regions.Length; square++)
        {
            var id = grid.Regions[square];
            if (id != EndRegion && !grid.MeantSingle[id] && grid.Bounds[id].Count == 1)
            {
                squeezed.Add(square);
            }
        }
        random.Shuffle(squeezed);
        var candidates = new List<int>(4);
        foreach (var square in squeezed)
        {
            var id = grid.Regions[square];
            // Another squeezed square may have joined this one already
            if (grid.Bounds[id].Count != 1)
            {
                continue;
            }
            var (row, column) = grid.CellOf(square);
            candidates.Clear();
            AddIfLinesUp(grid, candidates, row - 1, column, vertical: true, stripCap);
            AddIfLinesUp(grid, candidates, row + 1, column, vertical: true, stripCap);
            AddIfLinesUp(grid, candidates, row, column - 1, vertical: false, stripCap);
            AddIfLinesUp(grid, candidates, row, column + 1, vertical: false, stripCap);
            if (candidates.Count > 0)
            {
                grid.Move(square, candidates[random.Next(candidates.Count)]);
            }
        }
    }

    private static void AddIfLinesUp(Grid grid, List<int> candidates, int row, int column, bool vertical, int stripCap)
    {
        if (row < 0 || column < 0 || row >= grid.Size || column >= grid.Size)
        {
            return;
        }
        var other = grid.Regions[grid.IndexOf(row, column)];
        if (other == EndRegion || grid.MeantSingle[other])
        {
            return;
        }
        var bounds = grid.Bounds[other];
        var linesUp = vertical ? bounds.MinColumn == bounds.MaxColumn : bounds.MinRow == bounds.MaxRow;
        if (linesUp && bounds.Count + 1 <= stripCap)
        {
            candidates.Add(other);
        }
    }

    private static void Fill(Grid grid, GeneratorConfig config, int stripCap, GameRandom random)
    {
        var empties = new List<int>();
        for (var square = 0; square < grid.Regions.Length; square++)
        {
            if (grid.Regions[square] == Empty)
            {
                empties.Add(square);
            }
        }
        var total = config.AcrossWeight + config.DownWeight + config.SingleWeight + config.BlockWeight;
        while (empties.Count > 0)
        {
            var pick = random.Next(empties.Count);
            var square = empties[pick];
            empties[pick] = empties[^1];
            empties.RemoveAt(empties.Count - 1);
            if (grid.Regions[square] != Empty)
            {
                continue;
            }
            var roll = random.Next(total);
            if (roll < config.AcrossWeight)
            {
                Strip(grid, square, 0, 1, stripCap, random);
            }
            else if (roll < config.AcrossWeight + config.DownWeight)
            {
                Strip(grid, square, 1, 0, stripCap, random);
            }
            else if (roll < config.AcrossWeight + config.DownWeight + config.SingleWeight)
            {
                grid.Assign(square, grid.NewRegion(meantSingle: true));
            }
            else
            {
                Block(grid, square, config.MaxBlockSide, stripCap, random);
            }
        }
    }

    // Grows from the square one step at a time; when both ends can grow, a coin picks the lower (0) or the higher (1)
    private static void Strip(Grid grid, int square, int rowStep, int columnStep, int cap, GameRandom random)
    {
        var id = grid.NewRegion(meantSingle: false);
        grid.Assign(square, id);
        var (row, column) = grid.CellOf(square);
        var length = random.Next(2, cap + 1);
        var low = 0;
        var high = 0;
        for (var have = 1; have < length; have++)
        {
            var canLow = grid.IsEmpty(row - rowStep * (low + 1), column - columnStep * (low + 1));
            var canHigh = grid.IsEmpty(row + rowStep * (high + 1), column + columnStep * (high + 1));
            if (!canLow && !canHigh)
            {
                return;
            }
            if (canLow && (!canHigh || random.Next(2) == 0))
            {
                low++;
                grid.Assign(grid.IndexOf(row - rowStep * low, column - columnStep * low), id);
            }
            else
            {
                high++;
                grid.Assign(grid.IndexOf(row + rowStep * high, column + columnStep * high), id);
            }
        }
    }

    // The largest block up to the drawn size that covers the square and fits: heights from the drawn one down, then
    // widths, each size's placements tried round from a random one in reading order of their offsets. 1x1 always fits.
    // A size one square wide is a strip, so one longer than the strip cap is skipped without drawing
    private static void Block(Grid grid, int square, int maxSide, int stripCap, GameRandom random)
    {
        var id = grid.NewRegion(meantSingle: false);
        var (row, column) = grid.CellOf(square);
        var height = random.Next(2, maxSide + 1);
        var width = random.Next(2, maxSide + 1);
        for (var h = height; h >= 1; h--)
        {
            for (var w = width; w >= 1; w--)
            {
                if (Math.Min(h, w) == 1 && Math.Max(h, w) > stripCap)
                {
                    continue;
                }
                var placements = h * w;
                var first = random.Next(placements);
                for (var k = 0; k < placements; k++)
                {
                    var offset = (first + k) % placements;
                    var top = row - offset / w;
                    var left = column - offset % w;
                    if (grid.Fits(top, left, h, w))
                    {
                        for (var r = top; r < top + h; r++)
                        {
                            for (var c = left; c < left + w; c++)
                            {
                                grid.Assign(grid.IndexOf(r, c), id);
                            }
                        }
                        return;
                    }
                }
            }
        }
    }

    private sealed class Grid
    {
        private static readonly RegionBounds NoSquares = new(int.MaxValue, int.MinValue, int.MaxValue, int.MinValue, 0);

        public Grid(int size)
        {
            Size = size;
            Regions = new int[size * size];
            Array.Fill(Regions, Empty);
        }

        public int Size { get; }

        // Each square's region ID, flat in reading order
        public int[] Regions { get; }

        public List<RegionBounds> Bounds { get; } = [];

        public List<bool> MeantSingle { get; } = [];

        public int NewRegion(bool meantSingle)
        {
            Bounds.Add(NoSquares);
            MeantSingle.Add(meantSingle);
            return Bounds.Count - 1;
        }

        public void Assign(int square, int id)
        {
            Regions[square] = id;
            var (row, column) = CellOf(square);
            Bounds[id] = Bounds[id].Include(row, column);
        }

        // Moves a one-square region's square into another region, leaving its old ID with no squares
        public void Move(int square, int id)
        {
            Bounds[Regions[square]] = NoSquares;
            Assign(square, id);
        }

        // Moves the second region's squares into the first, leaving the second ID with no squares
        public void Join(int first, int second, List<int> secondSquares)
        {
            foreach (var square in secondSquares)
            {
                Assign(square, first);
            }
            Bounds[second] = NoSquares;
        }

        public int IndexOf(int row, int column) => row * Size + column;

        public (int Row, int Column) CellOf(int square) => (square / Size, square % Size);

        public bool IsEmpty(int row, int column) =>
            row >= 0 && column >= 0 && row < Size && column < Size && Regions[IndexOf(row, column)] == Empty;

        public bool Fits(int top, int left, int height, int width)
        {
            for (var row = top; row < top + height; row++)
            {
                for (var column = left; column < left + width; column++)
                {
                    if (!IsEmpty(row, column))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        // IDs from 1 in reading order of each region's first square, as hand-written level files number them
        public int[,] Numbered()
        {
            var renumbered = new int[Bounds.Count];
            var next = 1;
            var numbered = new int[Size, Size];
            for (var square = 0; square < Regions.Length; square++)
            {
                var id = Regions[square];
                if (renumbered[id] == 0)
                {
                    renumbered[id] = next++;
                }
                numbered[square / Size, square % Size] = renumbered[id];
            }
            return numbered;
        }
    }
}
