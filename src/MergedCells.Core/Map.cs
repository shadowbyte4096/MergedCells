namespace MergedCells.Core;

public sealed class Map
{
    private readonly int[] regions;
    private readonly Dictionary<int, bool> filled;
    // Null when the map has no twist tiles, so a plain map pays nothing for them in Slide
    private readonly Tile[]? tiles;
    // Each square's partner as a flat index, -1 for none; null when the map has no teleporters
    private readonly int[]? partners;
    // A slide keeps one direction, so entering a teleporter square twice repeats everything after it: a slide with more
    // jumps than this has looped. It also bounds every slide, at most (this + 2) * max(Rows, Columns) steps
    private readonly int teleporterSquares;

    private Map(
        int[,] regions, Cell start, Cell end, Rectangle[] rectangles, Dictionary<int, bool> filled, Tile[,]? tiles, TeleporterPair[] teleporters)
    {
        Rows = regions.GetLength(0);
        Columns = regions.GetLength(1);
        Start = start;
        End = end;
        Rectangles = Array.AsReadOnly(rectangles);
        Teleporters = Array.AsReadOnly(teleporters);
        this.filled = filled;
        this.regions = new int[Rows * Columns];
        this.tiles = HasAny(tiles) ? new Tile[Rows * Columns] : null;
        for (var row = 0; row < Rows; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                this.regions[row * Columns + column] = regions[row, column];
                if (this.tiles != null)
                {
                    this.tiles[row * Columns + column] = tiles![row, column];
                }
            }
        }
        if (teleporters.Length > 0)
        {
            partners = new int[Rows * Columns];
            teleporterSquares = teleporters.Length * 2;
            Array.Fill(partners, -1);
            foreach (var pair in teleporters)
            {
                partners[IndexOf(pair.First)] = IndexOf(pair.Second);
                partners[IndexOf(pair.Second)] = IndexOf(pair.First);
            }
        }
    }

    private int IndexOf(Cell cell) => cell.Row * Columns + cell.Column;

    public int Rows { get; }

    public int Columns { get; }

    public Cell Start { get; }

    public Cell End { get; }

    /// <summary>
    /// One entry per region, in reading order of each region's first square: the region itself when it's a rectangle,
    /// otherwise the smallest rectangle round it.
    /// </summary>
    public IReadOnlyList<Rectangle> Rectangles { get; }

    /// <summary>The map's teleporter pairs in the order they were given, empty when it has none.</summary>
    public IReadOnlyList<TeleporterPair> Teleporters { get; }

    /// <summary>
    /// True when the region fills its entry in <see cref="Rectangles"/>. Throws <see cref="ArgumentException"/> for an ID
    /// that isn't on the map.
    /// </summary>
    public bool IsRectangle(int regionId) =>
        filled.TryGetValue(regionId, out var isRectangle)
            ? isRectangle
            : throw new ArgumentException($"Region {regionId} isn't on the map.", nameof(regionId));

    /// <summary>
    /// Checks a region grid, start, end, tiles and teleporter pairs against the map rules. Returns every problem found,
    /// or an empty list when the map is valid.
    /// </summary>
    public static IReadOnlyList<MapProblem> Validate(
        int[,] regions, Cell start, Cell end, Tile[,]? tiles = null, IReadOnlyList<TeleporterPair>? teleporters = null) =>
        MapValidator.Validate(regions, start, end, tiles, teleporters);

    /// <summary>
    /// Builds a map from copies of <paramref name="regions"/> (indexed <c>[row, column]</c>), the tiles and the teleporter
    /// pairs. Throws <see cref="InvalidMapException"/> listing every problem <see cref="Validate"/> finds.
    /// </summary>
    public static Map Create(
        int[,] regions, Cell start, Cell end, Tile[,]? tiles = null, IReadOnlyList<TeleporterPair>? teleporters = null)
    {
        ArgumentNullException.ThrowIfNull(regions);
        // Validate private copies, so a caller changing its arrays mid-check can't produce an invalid map
        var copy = (int[,])regions.Clone();
        var tilesCopy = (Tile[,]?)tiles?.Clone();
        var teleportersCopy = teleporters?.ToArray() ?? [];
        var problems = MapValidator.Validate(copy, start, end, tilesCopy, teleportersCopy);
        if (problems.Count > 0)
        {
            throw new InvalidMapException(problems);
        }
        var bounds = RegionBounds.Measure(copy, out var firstSeenOrder);
        return new Map(
            copy,
            start,
            end,
            firstSeenOrder.Select(id => bounds[id].ToRectangle(id)).ToArray(),
            bounds.ToDictionary(pair => pair.Key, pair => pair.Value.Count == pair.Value.Area),
            tilesCopy,
            teleportersCopy);
    }

    public bool Contains(Cell cell) => IsInside(cell, Rows, Columns);

    // A fresh copy of the region grid, indexed [row, column], for code that builds a changed map
    internal int[,] RegionGrid()
    {
        var grid = new int[Rows, Columns];
        for (var row = 0; row < Rows; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                grid[row, column] = regions[row * Columns + column];
            }
        }
        return grid;
    }

    // A fresh copy of the tile grid, all Tile.None when the map has no tiles
    internal Tile[,] TileGrid()
    {
        var grid = new Tile[Rows, Columns];
        if (tiles != null)
        {
            for (var row = 0; row < Rows; row++)
            {
                for (var column = 0; column < Columns; column++)
                {
                    grid[row, column] = tiles[row * Columns + column];
                }
            }
        }
        return grid;
    }

    /// <summary>True when any square holds a twist tile.</summary>
    public bool HasTiles => tiles != null;

    /// <summary>The square's twist tile, <see cref="Tile.None"/> on a plain square.</summary>
    public Tile TileAt(Cell cell)
    {
        if (!Contains(cell))
        {
            throw new ArgumentOutOfRangeException(nameof(cell), cell, "The cell is outside the map.");
        }
        return tiles == null ? Tile.None : tiles[cell.Row * Columns + cell.Column];
    }

    private static bool HasAny(Tile[,]? tiles)
    {
        if (tiles == null)
        {
            return false;
        }
        foreach (var tile in tiles)
        {
            if (tile != Tile.None)
            {
                return true;
            }
        }
        return false;
    }

    public int RegionAt(Cell cell)
    {
        if (!Contains(cell))
        {
            throw new ArgumentOutOfRangeException(nameof(cell), cell, "The cell is outside the map.");
        }
        return regions[cell.Row * Columns + cell.Column];
    }

    /// <summary>
    /// Slides from <paramref name="from"/> until the next square is off the map (stopping on the last
    /// square) or in a different rectangle (stopping on that first square inside it). Reaching the end
    /// square wins and stops there. A wall stops the slide on the square before it, and a sticky square stops it on
    /// that square. Entering a trap sends the player to the start, with <see cref="SlideResult.Trapped"/> true. Entering
    /// a teleporter moves the player to its partner, and the slide carries on from there, with
    /// <see cref="SlideResult.Teleported"/> true. A slide that can't move returns <paramref name="from"/> with
    /// <see cref="SlideResult.ReachedEnd"/> false, and one that would never stop also has <see cref="SlideResult.Looped"/> true.
    /// </summary>
    public SlideResult Slide(Cell from, Direction direction) => Walk(from, direction, null);

    /// <summary>
    /// As <see cref="Slide"/>, also giving the slide's straight runs in order, for animation; the solver uses
    /// <see cref="Slide"/>.
    /// </summary>
    public SlideTrace Trace(Cell from, Direction direction)
    {
        var runs = new List<SlideRun>();
        var result = Walk(from, direction, runs);
        return new SlideTrace(result, runs.AsReadOnly());
    }

    // Runs are recorded only when tracing, so Slide allocates nothing
    private SlideResult Walk(Cell from, Direction direction, List<SlideRun>? runs)
    {
        if (!Contains(from))
        {
            throw new ArgumentOutOfRangeException(nameof(from), from, "The slide starts outside the map.");
        }

        var current = from;
        var runStart = from;
        var teleported = false;
        var jumps = 0;
        while (true)
        {
            var next = current.Step(direction);
            if (!Contains(next))
            {
                return Finish(runs, runStart, current, new SlideResult(current, false, Teleported: teleported));
            }
            var index = IndexOf(next);
            var tile = tiles == null ? Tile.None : tiles[index];
            if (tile == Tile.Wall)
            {
                return Finish(runs, runStart, current, new SlideResult(current, false, Teleported: teleported));
            }
            // Before the boundary check, so a slide passing over an end that shares a rectangle would
            // still win. Validation keeps the end 1x1 today; this is the safety net if that ever changes.
            if (next == End)
            {
                return Finish(runs, runStart, End, new SlideResult(End, true, Teleported: teleported));
            }
            if (tile == Tile.Sticky)
            {
                return Finish(runs, runStart, next, new SlideResult(next, false, Teleported: teleported));
            }
            if (tile == Tile.Trap)
            {
                return Finish(runs, runStart, next, new SlideResult(Start, false, true, teleported));
            }
            // Before the boundary check, so a teleporter on a new region's first square teleports. Teleporter squares
            // hold no tile, so the tests above never see one. The slide carries on from the partner.
            if (partners != null && partners[index] >= 0)
            {
                if (++jumps > teleporterSquares)
                {
                    runs?.Clear();
                    return Finish(runs, from, from, new SlideResult(from, false, Looped: true));
                }
                runs?.Add(new SlideRun(runStart, next));
                current = new Cell(partners[index] / Columns, partners[index] % Columns);
                runStart = current;
                teleported = true;
                continue;
            }
            if (RegionAt(next) != RegionAt(current))
            {
                return Finish(runs, runStart, next, new SlideResult(next, false, Teleported: teleported));
            }
            current = next;
        }
    }

    private static SlideResult Finish(List<SlideRun>? runs, Cell runStart, Cell runEnd, SlideResult result)
    {
        runs?.Add(new SlideRun(runStart, runEnd));
        return result;
    }

    internal static bool IsInside(Cell cell, int rows, int columns) =>
        cell.Row >= 0 && cell.Row < rows && cell.Column >= 0 && cell.Column < columns;
}
