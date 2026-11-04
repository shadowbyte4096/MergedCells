namespace MergedCells.Core;

public sealed class Map
{
    private readonly int[] regions;

    private Map(int[,] regions, Cell start, Cell end, Rectangle[] rectangles)
    {
        Rows = regions.GetLength(0);
        Columns = regions.GetLength(1);
        Start = start;
        End = end;
        Rectangles = Array.AsReadOnly(rectangles);
        this.regions = new int[Rows * Columns];
        for (var row = 0; row < Rows; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                this.regions[row * Columns + column] = regions[row, column];
            }
        }
    }

    public int Rows { get; }

    public int Columns { get; }

    public Cell Start { get; }

    public Cell End { get; }

    /// <summary>One rectangle per region, in reading order of their top-left squares.</summary>
    public IReadOnlyList<Rectangle> Rectangles { get; }

    /// <summary>
    /// Checks a region grid, start and end against the map rules. Returns every problem found,
    /// or an empty list when the map is valid.
    /// </summary>
    public static IReadOnlyList<MapProblem> Validate(int[,] regions, Cell start, Cell end) =>
        MapValidator.Validate(regions, start, end);

    /// <summary>
    /// Builds a map from a copy of <paramref name="regions"/>, indexed <c>[row, column]</c>.
    /// Throws <see cref="InvalidMapException"/> listing every problem <see cref="Validate"/> finds.
    /// </summary>
    public static Map Create(int[,] regions, Cell start, Cell end)
    {
        ArgumentNullException.ThrowIfNull(regions);
        // Validate a private copy, so a caller changing its array mid-check can't produce an invalid map
        var copy = (int[,])regions.Clone();
        var problems = MapValidator.Validate(copy, start, end);
        if (problems.Count > 0)
        {
            throw new InvalidMapException(problems);
        }
        var bounds = RegionBounds.Measure(copy, out var firstSeenOrder);
        return new Map(copy, start, end, firstSeenOrder.Select(id => bounds[id].ToRectangle(id)).ToArray());
    }

    public bool Contains(Cell cell) => IsInside(cell, Rows, Columns);

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
    /// square wins and stops there. A slide that can't move returns <paramref name="from"/> with
    /// <see cref="SlideResult.ReachedEnd"/> false.
    /// </summary>
    public SlideResult Slide(Cell from, Direction direction)
    {
        if (!Contains(from))
        {
            throw new ArgumentOutOfRangeException(nameof(from), from, "The slide starts outside the map.");
        }

        var current = from;
        while (true)
        {
            var next = current.Step(direction);
            if (!Contains(next))
            {
                return new SlideResult(current, false);
            }
            // Before the boundary check, so a slide passing over an end that shares a rectangle would
            // still win. Validation keeps the end 1x1 today; this is the safety net if that ever changes.
            if (next == End)
            {
                return new SlideResult(End, true);
            }
            if (RegionAt(next) != RegionAt(current))
            {
                return new SlideResult(next, false);
            }
            current = next;
        }
    }

    internal static bool IsInside(Cell cell, int rows, int columns) =>
        cell.Row >= 0 && cell.Row < rows && cell.Column >= 0 && cell.Column < columns;
}
