namespace MergedCells.Core;

/// <summary>
/// The stepped rings inside a wall. Edge-joined walls make one shape, and each ring's contour is a smaller copy of its
/// outline, so a square's rings depend only on which of its neighbours are walls.
/// </summary>
public static class WallRings
{
    /// <summary>
    /// How far a point lies inside the contour of ring <paramref name="ring"/> (0 is the wall's edge), in square units;
    /// negative outside it, and positive infinity when no side of the square bounds the ring. The point is measured from
    /// the square's top-left corner. The wall's edge sits <paramref name="inset"/> in from the grid lines on sides
    /// without a wall neighbour, with outer corners of radius <paramref name="corner"/>. The rings share the depth from
    /// the edge to a lone wall's centre equally, and each ring's corners shrink in step.
    /// </summary>
    public static float Distance(WallSides walls, float x, float y, float inset, float corner, int ring, int rings)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rings, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(ring);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(ring, rings);

        var edge = inset + ring * (0.5f - inset) / rings;
        var radius = corner * (1 - (float)ring / rings);
        var up = (walls & WallSides.Up) == 0;
        var right = (walls & WallSides.Right) == 0;
        var down = (walls & WallSides.Down) == 0;
        var left = (walls & WallSides.Left) == 0;

        // Each open side as a distance from its grid line, so the four corners read alike
        var fromTop = y;
        var fromRight = 1 - x;
        var fromBottom = 1 - y;
        var fromLeft = x;

        var distance = float.PositiveInfinity;
        if (up)
        {
            distance = MathF.Min(distance, fromTop - edge);
        }
        if (right)
        {
            distance = MathF.Min(distance, fromRight - edge);
        }
        if (down)
        {
            distance = MathF.Min(distance, fromBottom - edge);
        }
        if (left)
        {
            distance = MathF.Min(distance, fromLeft - edge);
        }

        distance = MathF.Min(distance, Corner(up, left, walls, WallSides.UpLeft, fromTop, fromLeft, edge, radius));
        distance = MathF.Min(distance, Corner(up, right, walls, WallSides.UpRight, fromTop, fromRight, edge, radius));
        distance = MathF.Min(distance, Corner(down, right, walls, WallSides.DownRight, fromBottom, fromRight, edge, radius));
        distance = MathF.Min(distance, Corner(down, left, walls, WallSides.DownLeft, fromBottom, fromLeft, edge, radius));
        return distance;
    }

    // Two open sides meet in a rounded outer corner. Two wall sides with no wall between them diagonally leave a
    // notch: the contour curves round the grid point, as an irregular region's inner corner does.
    private static float Corner(
        bool openA, bool openB, WallSides walls, WallSides diagonal, float fromA, float fromB, float edge, float radius)
    {
        if (openA && openB)
        {
            var centre = edge + radius;
            return radius > 0 && fromA < centre && fromB < centre
                ? radius - MathF.Sqrt((fromA - centre) * (fromA - centre) + (fromB - centre) * (fromB - centre))
                : float.PositiveInfinity;
        }
        if (!openA && !openB && (walls & diagonal) == 0)
        {
            return MathF.Sqrt(fromA * fromA + fromB * fromB) - edge;
        }
        return float.PositiveInfinity;
    }
}
