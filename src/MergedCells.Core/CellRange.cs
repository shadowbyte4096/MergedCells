namespace MergedCells.Core;

// Inclusive cell bounds
public readonly record struct CellRange(int Top, int Left, int Bottom, int Right)
{
    /// <summary>
    /// The cells a view rectangle touches, clamped to a map of <paramref name="rows"/> by <paramref name="columns"/>.
    /// The edges are in cell units (x is the column, y is the row), and partly covered cells count.
    /// Null when the view misses the map entirely.
    /// </summary>
    public static CellRange? Covering(double left, double top, double right, double bottom, int rows, int columns)
    {
        var firstColumn = Math.Max(0, (int)Math.Floor(left));
        var lastColumn = Math.Min(columns - 1, (int)Math.Ceiling(right) - 1);
        var firstRow = Math.Max(0, (int)Math.Floor(top));
        var lastRow = Math.Min(rows - 1, (int)Math.Ceiling(bottom) - 1);
        if (firstColumn > lastColumn || firstRow > lastRow)
        {
            return null;
        }
        return new CellRange(firstRow, firstColumn, lastRow, lastColumn);
    }
}
