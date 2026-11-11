namespace MergedCells.Core.Tests;

public class PlayerSlideTests
{
    // Right from [0, 1] into a trap on [0, 3], then back to the start
    private static readonly PlayerSlide Caught = new(Direction.Right, new(0, 1), new(0, 3), new(0, 0), false);

    [Theory]
    [InlineData(Direction.Down, 0, 0, 1, 0, 1)]
    [InlineData(Direction.Right, 3, 0, 3, 3, 3)]
    [InlineData(Direction.Up, 5, 2, 1, 2, 4)]
    public void Squares_is_the_distance_travelled(Direction direction, int fromRow, int fromColumn, int toRow, int toColumn, int expected)
    {
        var slide = new PlayerSlide(direction, new(fromRow, fromColumn), new(toRow, toColumn), new(toRow, toColumn), false);

        Assert.Equal(expected, slide.Squares);
    }

    [Fact]
    public void Slide_that_ends_away_from_the_square_it_reached_is_trapped()
    {
        Assert.True(Caught.Trapped);
    }

    [Fact]
    public void Squares_of_a_trapped_slide_count_the_slide_into_the_trap()
    {
        Assert.Equal(2, Caught.Squares);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Slide_that_ends_where_it_reached_is_not_trapped(bool wins)
    {
        Assert.False(new PlayerSlide(Direction.Down, new(0, 0), new(3, 0), new(3, 0), wins).Trapped);
    }
}
