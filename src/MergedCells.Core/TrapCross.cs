namespace MergedCells.Core;

// The geometry of a trap's cross, so its look is unit tested without graphics
public static class TrapCross
{
    /// <summary>
    /// How far the point (<paramref name="x"/>, <paramref name="y"/>), in square units from the square's top-left, lies
    /// inside the cross; negative outside. The cross is the square's two diagonals through its centre, each a centre line
    /// spanning <paramref name="size"/> of the square on each axis, with round ends, <paramref name="width"/> wide. Throws
    /// <see cref="ArgumentOutOfRangeException"/> unless both are positive and finite.
    /// </summary>
    public static float Distance(float x, float y, float size, float width)
    {
        RequirePositiveFinite(size, nameof(size));
        RequirePositiveFinite(width, nameof(width));
        var near = 0.5f - size / 2;
        var far = 0.5f + size / 2;
        var nearest = MathF.Min(
            ToSegment(x, y, near, near, far, far),
            ToSegment(x, y, far, near, near, far));
        return width / 2 - nearest;
    }

    private static float ToSegment(float x, float y, float x0, float y0, float x1, float y1)
    {
        var dx = x1 - x0;
        var dy = y1 - y0;
        var along = Math.Clamp(((x - x0) * dx + (y - y0) * dy) / (dx * dx + dy * dy), 0, 1);
        return MathF.Sqrt(MathF.Pow(x - (x0 + along * dx), 2) + MathF.Pow(y - (y0 + along * dy), 2));
    }

    private static void RequirePositiveFinite(float value, string name)
    {
        if (!float.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Must be positive and finite.");
        }
    }
}
