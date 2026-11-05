using System.Numerics;
using static MergedCells.Core.CornerKind;
using static MergedCells.Core.Tests.TestMaps;

namespace MergedCells.Core.Tests;

public class RegionOutlineTests
{
    private static readonly float Root2 = MathF.Sqrt(2);

    private static Map SingleSquare() => Map.Create(Grid("AB"), new(0, 0), new(0, 1));

    private static Map LShape() => Map.Create(Grid("AB", "AA"), new(0, 0), new(0, 1));

    // Corners as text, so a loop's corners and IsHole fit in one tuple assert (arrays compare by reference)
    private static string Corners(params (int Row, int Column, CornerKind Kind)[] corners) =>
        Describe(corners.Select(c => new OutlineCorner(c.Row, c.Column, c.Kind)));

    private static string Describe(IEnumerable<OutlineCorner> corners) => string.Join(" ", corners);

    private static void AssertPoints(IEnumerable<Vector2> expected, IEnumerable<Vector2> actual)
    {
        Assert.All(
            expected.Zip(actual),
            pair => Assert.True(Vector2.Distance(pair.First, pair.Second) < 1e-5f, $"Expected {pair.First}, got {pair.Second}"));
    }

    [Fact]
    public void Single_square_is_one_loop_of_four_outer_corners()
    {
        var loop = Assert.Single(RegionOutline.Trace(SingleSquare(), 'A'));

        Assert.Equal(
            (Corners((0, 0, Outer), (0, 1, Outer), (1, 1, Outer), (1, 0, Outer)), false),
            (Describe(loop.Corners), loop.IsHole));
    }

    [Fact]
    public void L_shape_has_one_inner_corner()
    {
        var loop = Assert.Single(RegionOutline.Trace(LShape(), 'A'));

        Assert.Equal(
            Corners((0, 0, Outer), (0, 1, Outer), (1, 1, Inner), (1, 2, Outer), (2, 2, Outer), (2, 0, Outer)),
            Describe(loop.Corners));
    }

    [Fact]
    public void Ring_has_an_outer_loop_and_a_hole()
    {
        var map = Map.Create(Grid("AAA", "ABA", "AAA"), new(0, 0), new(1, 1));

        var loops = RegionOutline.Trace(map, 'A');

        Assert.Collection(
            loops,
            outer => Assert.Equal(
                (Corners((0, 0, Outer), (0, 3, Outer), (3, 3, Outer), (3, 0, Outer)), false),
                (Describe(outer.Corners), outer.IsHole)),
            hole => Assert.Equal(
                (Corners((1, 1, Inner), (2, 1, Inner), (2, 2, Inner), (1, 2, Inner)), true),
                (Describe(hole.Corners), hole.IsHole)));
    }

    [Fact]
    public void Squares_touching_at_a_corner_keep_separate_corners()
    {
        var map = Map.Create(Grid("AAA", "ABA", "CAA"), new(0, 0), new(1, 1));

        var loop = Assert.Single(RegionOutline.Trace(map, 'A'));

        Assert.Equal(
            (Corners(
                (0, 0, Outer), (0, 3, Outer), (3, 3, Outer), (3, 1, Outer), (2, 1, Outer),
                (2, 2, Inner), (1, 2, Inner), (1, 1, Inner), (2, 1, Outer), (2, 0, Outer)), false),
            (Describe(loop.Corners), loop.IsHole));
    }

    [Fact]
    public void Trace_throws_for_an_id_not_on_the_map()
    {
        Assert.Throws<ArgumentException>(() => RegionOutline.Trace(SingleSquare(), 'Z'));
    }

    [Fact]
    public void Outer_corner_arc_runs_between_its_tangent_points()
    {
        var loop = RegionOutline.Trace(SingleSquare(), 'A')[0];

        var points = RegionOutline.Inset(loop, 0.1f, 0.2f, 2);

        Assert.Equal(12, points.Count);
        AssertPoints(
            [new(0.1f, 0.3f), new(0.3f - 0.2f / Root2, 0.3f - 0.2f / Root2), new(0.3f, 0.1f)],
            points.Take(3));
    }

    [Fact]
    public void Inner_corner_arc_is_centred_on_the_grid_point()
    {
        var loop = RegionOutline.Trace(LShape(), 'A')[0];

        var points = RegionOutline.Inset(loop, 0.1f, 0.2f, 2);

        AssertPoints(
            [new(0.9f, 1f), new(1f - 0.1f / Root2, 1f + 0.1f / Root2), new(1f, 1.1f)],
            points.Skip(6).Take(3));
    }

    [Theory]
    [InlineData(0.1f, 0.2f, 0)]
    [InlineData(-0.1f, 0.2f, 2)]
    [InlineData(0.1f, -0.2f, 2)]
    [InlineData(0.2f, 0.31f, 2)]
    public void Inset_rejects_bad_arguments(float inset, float convexRadius, int arcSegments)
    {
        var loop = RegionOutline.Trace(SingleSquare(), 'A')[0];

        Assert.Throws<ArgumentOutOfRangeException>(() => RegionOutline.Inset(loop, inset, convexRadius, arcSegments));
    }
}
