namespace MergedCells.Core.Tests;

public class WallRingsTests
{
    // The outlined look: half a gap of 0.16, the blocks' corner, four rings
    private const float Inset = 0.08f;
    private const float Corner = 0.2f;
    private const int Rings = 4;

    private static float Distance(WallSides walls, float x, float y, int ring) =>
        WallRings.Distance(walls, x, y, Inset, Corner, ring, Rings);

    public static TheoryData<int> EveryRing => new(Enumerable.Range(0, Rings));

    [Theory]
    [MemberData(nameof(EveryRing))]
    public void Lone_wall_centre_is_inside_every_ring(int ring)
    {
        Assert.True(Distance(WallSides.None, 0.5f, 0.5f, ring) > 0);
    }

    [Fact]
    public void Point_in_the_half_gap_is_outside_the_wall()
    {
        Assert.True(Distance(WallSides.None, 0.5f, 0.04f, 0) < 0);
    }

    // Each ring is a quarter of the 0.42 from the edge to the centre
    [Theory]
    [MemberData(nameof(EveryRing))]
    public void Ring_contours_are_evenly_spaced(int ring)
    {
        Assert.Equal(0.3f - 0.08f - ring * 0.105f, Distance(WallSides.None, 0.5f, 0.3f, ring), 4);
    }

    [Theory]
    [MemberData(nameof(EveryRing))]
    public void Enclosed_square_is_inside_every_ring_everywhere(int ring)
    {
        Assert.Equal(float.PositiveInfinity, Distance((WallSides)255, 0.01f, 0.01f, ring));
    }

    [Theory]
    [InlineData(0.1f)]
    [InlineData(0.9f)]
    public void Middle_of_a_strip_depends_only_on_its_open_sides(float x)
    {
        Assert.Equal(0.3f - 0.08f, Distance(WallSides.Left | WallSides.Right, x, 0.3f, 0), 4);
    }

    [Fact]
    public void Outer_corner_is_rounded()
    {
        Assert.True(Distance(WallSides.None, 0.09f, 0.09f, 0) < 0);
    }

    [Fact]
    public void Outer_corner_is_square_without_a_corner_radius()
    {
        Assert.True(WallRings.Distance(WallSides.None, 0.09f, 0.09f, Inset, 0, 0, Rings) > 0);
    }

    [Fact]
    public void Inner_corner_has_a_notch()
    {
        Assert.True(Distance(WallSides.Up | WallSides.Left, 0.05f, 0.05f, 0) < 0);
    }

    [Fact]
    public void Inner_corner_filled_by_a_wall_has_no_notch()
    {
        Assert.True(Distance(WallSides.Up | WallSides.Left | WallSides.UpLeft, 0.05f, 0.05f, 0) > 0);
    }

    // Walls touching only at a corner stay separate shapes: the diagonal alone changes nothing
    [Fact]
    public void Diagonal_wall_alone_leaves_the_corner_rounded()
    {
        Assert.True(Distance(WallSides.UpLeft, 0.09f, 0.09f, 0) < 0);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Rings_nest(int ring)
    {
        for (var row = 0; row <= 20; row++)
        {
            for (var column = 0; column <= 20; column++)
            {
                var (x, y) = (column / 20f, row / 20f);
                if (Distance(WallSides.None, x, y, ring) > 0)
                {
                    Assert.True(Distance(WallSides.None, x, y, ring - 1) > 0, $"({x}, {y}) is inside ring {ring} but not ring {ring - 1}");
                }
            }
        }
    }

    [Theory]
    [InlineData(0.2f, 0.3f)]
    [InlineData(0.85f, 0.1f)]
    public void Mirrored_walls_give_mirrored_distances(float x, float y)
    {
        Assert.Equal(Distance(WallSides.Left, x, y, 1), Distance(WallSides.Right, 1 - x, y, 1), 4);
    }

    [Fact]
    public void Flat_form_fills_the_square()
    {
        Assert.True(WallRings.Distance(WallSides.None, 0.01f, 0.01f, 0, 0, 0, Rings) > 0);
    }

    [Theory]
    [InlineData(-1, 4)]
    [InlineData(4, 4)]
    [InlineData(0, 0)]
    public void Ring_out_of_range_throws(int ring, int rings)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WallRings.Distance(WallSides.None, 0.5f, 0.5f, Inset, Corner, ring, rings));
    }
}
