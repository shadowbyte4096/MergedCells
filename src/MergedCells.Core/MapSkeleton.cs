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

    /// <summary>
    /// <paramref name="regions"/> holds a fixed region number per square, or null for a free square; the end's square
    /// is null, because the end is always its own region. <paramref name="tiles"/> holds a fixed tile per square
    /// (<see cref="Tile.None"/> keeps it plain), or null where twists may go. <paramref name="starts"/> lists the squares
    /// the start may be on; empty means the generator's start area for the end. Throws <see cref="ArgumentException"/>
    /// naming the square when a rule is broken.
    /// </summary>
    public MapSkeleton(int?[,] regions, Tile?[,] tiles, Cell end, IReadOnlyList<Cell> starts)
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

        this.regions = (int?[,])regions.Clone();
        this.tiles = (Tile?[,])tiles.Clone();
        End = end;
        Starts = [.. starts];
    }

    public int Size { get; }

    public Cell End { get; }

    /// <summary>The squares the start may be on, in the order given; empty means the generator's start area.</summary>
    public IReadOnlyList<Cell> Starts { get; }

    /// <summary>The square's fixed region number, or null when the square is free.</summary>
    public int? RegionAt(Cell cell) => regions[cell.Row, cell.Column];

    /// <summary>The square's fixed tile, or null when twists may go there.</summary>
    public Tile? TileAt(Cell cell) => tiles[cell.Row, cell.Column];

    internal bool HasFixedRegion(Cell cell) => regions[cell.Row, cell.Column] is not null;

    internal bool HasFixedTile(Cell cell) => tiles[cell.Row, cell.Column] is not null;

    private bool Contains(Cell cell) => Map.IsInside(cell, Size, Size);

    private static string Name(Cell cell) => $"[{cell.Row}, {cell.Column}]";

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
