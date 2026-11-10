namespace MergedCells.Core;

/// <summary>
/// The diagonal stripes on a sticky square. A whole number of stripes fits each square, so every square carries the
/// same pattern and stripes run unbroken across squares that join.
/// </summary>
public static class StickyStripes
{
    /// <summary>
    /// How far a point lies inside the nearest stripe, in square units; negative between stripes. The point is measured
    /// from the square's top-left corner. Stripes run from bottom-left to top-right at 45 degrees,
    /// <paramref name="stripes"/> of them crossing each side of a square, each <paramref name="width"/> wide across.
    /// </summary>
    public static float Distance(float x, float y, int stripes, float width)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(stripes, 1);
        var period = 1f / stripes;
        var along = x + y;
        var offset = along - MathF.Floor(along / period) * period;
        return width / 2 - MathF.Abs(offset - period / 2) / MathF.Sqrt(2);
    }
}
