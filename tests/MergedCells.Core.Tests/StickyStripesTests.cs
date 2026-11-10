namespace MergedCells.Core.Tests;

public class StickyStripesTests
{
    private const int Stripes = 4;
    private const float Width = 0.085f;
    private const float Tolerance = 1e-4f;

    // With four stripes, a stripe's centre line crosses the top edge an eighth of a square in
    [Fact]
    public void Stripes_centre_line_is_half_a_width_inside()
    {
        Assert.Equal(Width / 2, StickyStripes.Distance(0.125f, 0, Stripes, Width), Tolerance);
    }

    [Fact]
    public void Centre_line_runs_from_bottom_left_to_top_right()
    {
        Assert.Equal(Width / 2, StickyStripes.Distance(0.025f, 0.1f, Stripes, Width), Tolerance);
    }

    // A quarter of a square along the top edge is 0.125 / sqrt(2) from the centre lines either side
    [Fact]
    public void Midway_between_stripes_is_outside()
    {
        Assert.Equal(Width / 2 - 0.125f / MathF.Sqrt(2), StickyStripes.Distance(0.25f, 0, Stripes, Width), Tolerance);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    [InlineData(1, -1)]
    [InlineData(-1, 0)]
    public void Pattern_repeats_from_square_to_square(int squaresRight, int squaresDown)
    {
        const float x = 0.3f;
        const float y = 0.6f;

        Assert.Equal(
            StickyStripes.Distance(x, y, Stripes, Width),
            StickyStripes.Distance(x + squaresRight, y + squaresDown, Stripes, Width),
            Tolerance);
    }

    [Theory]
    [InlineData(1, 0.5f)]
    [InlineData(2, 0.25f)]
    [InlineData(5, 0.1f)]
    public void Stripe_count_sets_where_the_first_centre_line_crosses_the_top_edge(int stripes, float x)
    {
        Assert.Equal(Width / 2, StickyStripes.Distance(x, 0, stripes, Width), Tolerance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Fewer_than_one_stripe_throws(int stripes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StickyStripes.Distance(0, 0, stripes, Width));
    }
}
