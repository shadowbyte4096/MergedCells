namespace MergedCells.Core;

/// <summary>
/// The parts of a map a concept level fixes, for <see cref="MapClimber"/> to build around: regions fixed square by
/// square, tiles fixed square by square, the end, and the squares the start may be on. Everything left free is cut,
/// filled with twists and edited as on a generated map. Immutable: it keeps copies of what it's given.
/// </summary>
public sealed class MapSkeleton
{
    private readonly int?[,] regions;
    private readonly Tile?[,] tiles;
    private readonly HashSet<Cell> pairSquares = [];

    /// <summary>
    /// <paramref name="regions"/> holds a fixed region number per square, or null for a free square; the end's square
    /// is null, because the end is always its own region. <paramref name="tiles"/> holds a fixed tile per square
    /// (<see cref="Tile.None"/> keeps it plain), or null where twists may go. <paramref name="starts"/> lists the squares
    /// the start may be on; empty means the generator's start area for the end. <paramref name="teleporters"/> fixes
    /// pairs, in letter order ahead of any the generator places; their squares are fixed plain squares. Throws
    /// <see cref="ArgumentException"/> naming the square when a rule is broken.
    /// </summary>
    public MapSkeleton(int?[,] regions, Tile?[,] tiles, Cell end, IReadOnlyList<Cell> starts, IReadOnlyList<TeleporterPair>? teleporters = null)
    {
        ArgumentNullException.ThrowIfNull(regions);
        ArgumentNullException.ThrowIfNull(tiles);
        ArgumentNullException.ThrowIfNull(starts);
        var size = regions.GetLength(0);
        if (regions.GetLength(1) != size || tiles.GetLength(0) != size || tiles.GetLength(1) != size)
        {
            throw new ArgumentException("The regions and tiles must be square grids of the same size.", nameof(regions));
        }
        if (size < 2)
        {
            throw new ArgumentException("A skeleton must be at least 2×2.", nameof(regions));
        }
        Size = size;
        if (!Contains(end))
        {
            throw new ArgumentException($"The end {Name(end)} is off the map.", nameof(end));
        }
        if (regions[end.Row, end.Column] is not null)
        {
            throw new ArgumentException($"The end {Name(end)} is always its own region, so it can't be in a fixed one.", nameof(regions));
        }
        if (tiles[end.Row, end.Column] is { } endTile && endTile != Tile.None)
        {
            throw new ArgumentException($"The end {Name(end)} can't hold a twist.", nameof(tiles));
        }
        var seen = new HashSet<Cell>();
        foreach (var start in starts)
        {
            if (!Contains(start))
            {
                throw new ArgumentException($"The start square {Name(start)} is off the map.", nameof(starts));
            }
            if (start == end)
            {
                throw new ArgumentException($"The start square {Name(start)} is the end.", nameof(starts));
            }
            if (!seen.Add(start))
            {
                throw new ArgumentException($"The start square {Name(start)} is listed twice.", nameof(starts));
            }
            if (start.Row > end.Row || start.Column > end.Column)
            {
                throw new ArgumentException($"The start square {Name(start)} is right of or below the end {Name(end)}.", nameof(starts));
            }
            if (tiles[start.Row, start.Column] is { } startTile && startTile != Tile.None)
            {
                throw new ArgumentException($"The start square {Name(start)} can't hold a twist.", nameof(tiles));
            }
        }
        CheckConnected(regions, size);
        var pairs = teleporters ?? [];
        CheckPairs(pairs, tiles, end, seen, nameof(teleporters));

        this.regions = (int?[,])regions.Clone();
        this.tiles = (Tile?[,])tiles.Clone();
        foreach (var pair in pairs)
        {
            this.tiles[pair.First.Row, pair.First.Column] = Tile.None;
            this.tiles[pair.Second.Row, pair.Second.Column] = Tile.None;
            pairSquares.Add(pair.First);
            pairSquares.Add(pair.Second);
        }
        End = end;
        Starts = [.. starts];
        Teleporters = [.. pairs];
    }

    public int Size { get; }

    public Cell End { get; }

    /// <summary>The squares the start may be on, in the order given; empty means the generator's start area.</summary>
    public IReadOnlyList<Cell> Starts { get; }

    /// <summary>The fixed teleporter pairs, in the order given; empty when there are none.</summary>
    public IReadOnlyList<TeleporterPair> Teleporters { get; }

    /// <summary>The square's fixed region number, or null when the square is free.</summary>
    public int? RegionAt(Cell cell) => regions[cell.Row, cell.Column];

    /// <summary>The square's fixed tile, or null when twists may go there.</summary>
    public Tile? TileAt(Cell cell) => tiles[cell.Row, cell.Column];

    internal bool HasFixedRegion(Cell cell) => regions[cell.Row, cell.Column] is not null;

    internal bool HasFixedTile(Cell cell) => tiles[cell.Row, cell.Column] is not null;

    internal bool IsFixedTeleporter(Cell cell) => pairSquares.Contains(cell);

    private bool Contains(Cell cell) => Map.IsInside(cell, Size, Size);

    private static string Name(Cell cell) => $"[{cell.Row}, {cell.Column}]";

    // A teleporter square is plain, never the start or the end, and in one pair only (chunk 16's rules), and every pair
    // needs a letter of its own
    private void CheckPairs(IReadOnlyList<TeleporterPair> pairs, Tile?[,] tiles, Cell end, HashSet<Cell> starts, string parameter)
    {
        if (pairs.Count > TeleporterLetters.Count)
        {
            throw new ArgumentException(
                $"A skeleton fixes at most {TeleporterLetters.Count} pairs, one per letter, and this one has {pairs.Count}.", parameter);
        }
        var paired = new HashSet<Cell>();
        for (var index = 0; index < pairs.Count; index++)
        {
            var pair = pairs[index];
            if (pair.First == pair.Second)
            {
                throw new ArgumentException($"Pair {index} is on one square, {Name(pair.First)}.", parameter);
            }
            foreach (var square in new[] { pair.First, pair.Second })
            {
                var problem = !Contains(square) ? "is off the map"
                    : square == end ? "is the end"
                    : starts.Contains(square) ? "is a start square"
                    : tiles[square.Row, square.Column] is { } tile && tile != Tile.None ? "holds a fixed twist"
                    : !paired.Add(square) ? "is in another pair"
                    : null;
                if (problem is not null)
                {
                    throw new ArgumentException($"Pair {index}'s square {Name(square)} {problem}.", parameter);
                }
            }
        }
    }

    // Each fixed number's squares must join edge to edge, as any region's must
    private static void CheckConnected(int?[,] regions, int size)
    {
        var checkedNumbers = new HashSet<int>();
        for (var row = 0; row < size; row++)
        {
            for (var column = 0; column < size; column++)
            {
                if (regions[row, column] is not { } number || !checkedNumbers.Add(number))
                {
                    continue;
                }
                var total = 0;
                foreach (var square in regions)
                {
                    if (square == number)
                    {
                        total++;
                    }
                }
                var reached = new bool[size, size];
                var queue = new Queue<Cell>();
                queue.Enqueue(new Cell(row, column));
                reached[row, column] = true;
                var count = 0;
                while (queue.Count > 0)
                {
                    var cell = queue.Dequeue();
                    count++;
                    foreach (var direction in Solver.Directions)
                    {
                        var next = cell.Step(direction);
                        if (Map.IsInside(next, size, size) && !reached[next.Row, next.Column] && regions[next.Row, next.Column] == number)
                        {
                            reached[next.Row, next.Column] = true;
                            queue.Enqueue(next);
                        }
                    }
                }
                if (count != total)
                {
                    throw new ArgumentException(
                        $"Fixed region {number} is in more than one piece; its square {Name(new Cell(row, column))} doesn't reach all of them.", nameof(regions));
                }
            }
        }
    }
}
