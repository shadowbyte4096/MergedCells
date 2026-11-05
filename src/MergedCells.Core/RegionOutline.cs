using System.Numerics;

namespace MergedCells.Core;

public enum CornerKind
{
    Outer,
    Inner,
}

// A corner on the grid lines: Row 0..Rows and Column 0..Columns, so (r, c) is the top-left corner of square (r, c)
public readonly record struct OutlineCorner(int Row, int Column, CornerKind Kind);

public sealed record OutlineLoop(IReadOnlyList<OutlineCorner> Corners, bool IsHole);

public static class RegionOutline
{
    /// <summary>
    /// The closed loops round a region's squares, walked with the region on the right (rows going down), each starting at
    /// its first corner in reading order. A right turn is an <see cref="CornerKind.Outer"/> corner and a left turn an
    /// <see cref="CornerKind.Inner"/> one. Where two of the region's squares touch only at a corner, the loop passes that
    /// point twice, as two outer corners. A loop round a part of the map the region encloses is a hole. Throws
    /// <see cref="ArgumentException"/> for an ID that isn't on the map.
    /// </summary>
    public static IReadOnlyList<OutlineLoop> Trace(Map map, int regionId)
    {
        ArgumentNullException.ThrowIfNull(map);
        var bounds = map.Rectangles.FirstOrDefault(rectangle => rectangle.Id == regionId);
        if (bounds.Rows == 0)
        {
            throw new ArgumentException($"Region {regionId} isn't on the map.", nameof(regionId));
        }

        var edges = Edges(map, regionId, bounds);
        var leaving = edges.GroupBy(edge => edge.From).ToDictionary(group => group.Key, group => group.ToList());
        var used = new HashSet<Edge>();
        var loops = new List<OutlineLoop>();
        foreach (var first in edges.OrderBy(edge => edge.From.Row).ThenBy(edge => edge.From.Column).ThenBy(edge => edge.Heading))
        {
            if (!used.Contains(first))
            {
                loops.Add(Walk(first, leaving, used));
            }
        }
        return loops;
    }

    /// <summary>
    /// The points of a loop moved <paramref name="inset"/> into its region, in square units (X is the column, Y the row).
    /// Each corner becomes a 90° arc of <paramref name="arcSegments"/> segments: an outer corner is rounded with
    /// <paramref name="convexRadius"/>, and an inner corner is an arc of radius <paramref name="inset"/> round the grid
    /// point. Throws <see cref="ArgumentOutOfRangeException"/> when <paramref name="arcSegments"/> is below 1, a size is
    /// negative, or the inset and radius together exceed half a square, where arcs could overlap across a one-square arm.
    /// </summary>
    public static IReadOnlyList<Vector2> Inset(OutlineLoop loop, float inset, float convexRadius, int arcSegments)
    {
        ArgumentNullException.ThrowIfNull(loop);
        ArgumentOutOfRangeException.ThrowIfLessThan(arcSegments, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(inset);
        ArgumentOutOfRangeException.ThrowIfNegative(convexRadius);
        if (inset + convexRadius > 0.5f)
        {
            throw new ArgumentOutOfRangeException(nameof(convexRadius), "The inset and the radius together exceed half a square.");
        }

        var corners = loop.Corners;
        var points = new List<Vector2>(corners.Count * (arcSegments + 1));
        for (var i = 0; i < corners.Count; i++)
        {
            var at = Point(corners[i]);
            var incoming = Vector2.Normalize(at - Point(corners[(i + corners.Count - 1) % corners.Count]));
            var outgoing = Vector2.Normalize(Point(corners[(i + 1) % corners.Count]) - at);
            var normalIn = RightNormal(incoming);
            var normalOut = RightNormal(outgoing);
            if (corners[i].Kind == CornerKind.Outer)
            {
                var centre = at + (inset + convexRadius) * (normalIn + normalOut);
                AddArc(points, centre, convexRadius, -normalIn, -normalOut, arcSegments);
            }
            else
            {
                AddArc(points, at, inset, normalIn, normalOut, arcSegments);
            }
        }
        return points;
    }

    // The direction an edge heads, in reading-order priority for choosing where a loop starts
    private enum Heading
    {
        Right,
        Down,
        Left,
        Up,
    }

    private readonly record struct Edge(Cell From, Heading Heading)
    {
        public Cell To => From.Step(ToDirection(Heading));
    }

    // Grid points reuse Cell: (r, c) is the top-left corner of square (r, c)
    private static List<Edge> Edges(Map map, int regionId, Rectangle bounds)
    {
        var edges = new List<Edge>();
        for (var row = bounds.TopLeft.Row; row <= bounds.BottomRight.Row; row++)
        {
            for (var column = bounds.TopLeft.Column; column <= bounds.BottomRight.Column; column++)
            {
                var cell = new Cell(row, column);
                if (map.RegionAt(cell) != regionId)
                {
                    continue;
                }
                if (!InRegion(map, cell.Step(Direction.Up), regionId))
                {
                    edges.Add(new Edge(new Cell(row, column), Heading.Right));
                }
                if (!InRegion(map, cell.Step(Direction.Right), regionId))
                {
                    edges.Add(new Edge(new Cell(row, column + 1), Heading.Down));
                }
                if (!InRegion(map, cell.Step(Direction.Down), regionId))
                {
                    edges.Add(new Edge(new Cell(row + 1, column + 1), Heading.Left));
                }
                if (!InRegion(map, cell.Step(Direction.Left), regionId))
                {
                    edges.Add(new Edge(new Cell(row + 1, column), Heading.Up));
                }
            }
        }
        return edges;
    }

    private static OutlineLoop Walk(Edge first, Dictionary<Cell, List<Edge>> leaving, HashSet<Edge> used)
    {
        var corners = new List<OutlineCorner>();
        var edge = first;
        while (true)
        {
            used.Add(edge);
            var choices = leaving[edge.To];
            // Two edges leave a point where squares touch only at a corner; turning right keeps those squares apart
            var next = choices.Count == 1 ? choices[0] : choices.First(choice => choice.Heading == TurnRight(edge.Heading));
            if (next.Heading != edge.Heading)
            {
                var kind = next.Heading == TurnRight(edge.Heading) ? CornerKind.Outer : CornerKind.Inner;
                corners.Add(new OutlineCorner(edge.To.Row, edge.To.Column, kind));
            }
            if (next == first)
            {
                break;
            }
            edge = next;
        }

        // The corner where the loop closes is its start, so it goes first
        corners.Insert(0, corners[^1]);
        corners.RemoveAt(corners.Count - 1);
        return new OutlineLoop(corners.AsReadOnly(), SignedArea(corners) < 0);
    }

    private static bool InRegion(Map map, Cell cell, int regionId) => map.Contains(cell) && map.RegionAt(cell) == regionId;

    private static Heading TurnRight(Heading heading) => (Heading)(((int)heading + 1) % 4);

    private static Direction ToDirection(Heading heading) => heading switch
    {
        Heading.Right => Direction.Right,
        Heading.Down => Direction.Down,
        Heading.Left => Direction.Left,
        _ => Direction.Up,
    };

    // Positive for a loop that runs clockwise on screen, which an outer loop does with its region on the right
    private static long SignedArea(List<OutlineCorner> corners)
    {
        long sum = 0;
        for (var i = 0; i < corners.Count; i++)
        {
            var a = corners[i];
            var b = corners[(i + 1) % corners.Count];
            sum += (long)a.Column * b.Row - (long)b.Column * a.Row;
        }
        return sum;
    }

    private static Vector2 Point(OutlineCorner corner) => new(corner.Column, corner.Row);

    // The side a region is on, walking in this direction with rows going down
    private static Vector2 RightNormal(Vector2 direction) => new(-direction.Y, direction.X);

    private static void AddArc(List<Vector2> points, Vector2 centre, float radius, Vector2 from, Vector2 to, int segments)
    {
        var start = MathF.Atan2(from.Y, from.X);
        var sweep = MathF.Atan2(to.Y, to.X) - start;
        if (sweep > MathF.PI)
        {
            sweep -= 2 * MathF.PI;
        }
        else if (sweep <= -MathF.PI)
        {
            sweep += 2 * MathF.PI;
        }
        for (var step = 0; step <= segments; step++)
        {
            var angle = start + sweep * step / segments;
            points.Add(centre + radius * new Vector2(MathF.Cos(angle), MathF.Sin(angle)));
        }
    }
}
