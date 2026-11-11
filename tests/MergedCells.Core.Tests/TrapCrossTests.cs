namespace MergedCells.Core.Tests;

public class TrapCrossTests
{
    // The outlined cross: centre lines from 0.3 to 0.7 of the square, stroke 0.11
    private const float Size = 0.4f;
    private const float Width = 0.11f;
    private const float Tolerance = 1e-5f;

    [Fact]
    public void Centre_is_inside_by_half_the_width()
    {
        Assert.Equal(Width / 2, TrapCross.Distance(0.5f, 0.5f, Size, Width), Tolerance);
    }

    [Theory]
    [InlineData(0.35f, 0.35f)]
    [InlineData(0.65f, 0.35f)]
    [InlineData(0.35f, 0.65f)]
    [InlineData(0.65f, 0.65f)]
    public void Point_on_a_diagonals_centre_line_is_inside_by_half_the_width(float x, float y)
    {
        Assert.Equal(Width / 2, TrapCross.Distance(x, y, Size, Width), Tolerance);
    }

    // [0.5, 0.3] is 0.2 / sqrt(2) from the nearest point of either diagonal
    [Fact]
    public void Midway_between_the_arms_is_outside()
    {
        Assert.Equal(Width / 2 - 0.2f / MathF.Sqrt(2), TrapCross.Distance(0.5f, 0.3f, Size, Width), Tolerance);
    }

    [Fact]
    public void Round_end_reaches_half_a_width_past_the_centre_lines_end()
    {
        var past = Width / 2 / MathF.Sqrt(2);

        Assert.Equal(0, TrapCross.Distance(0.3f - past, 0.3f - past, Size, Width), Tolerance);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-0.4f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void Bad_size_throws(float size)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TrapCross.Distance(0.5f, 0.5f, size, Width));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-0.11f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void Bad_width_throws(float width)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TrapCross.Distance(0.5f, 0.5f, Size, width));
    }
}
