namespace MergedCells.Core.Tests;

public class PlayerSlideTests
{
    // Right from [0, 1] into a trap on [0, 3], then back to the start
    private static readonly PlayerSlide Caught = new(Direction.Right, [new(new(0, 1), new(0, 3))], new(0, 0), false);

    // Right from the start into a teleporter on [0, 2], out of [1, 4] and on to [1, 5]
    private static readonly SlideRun[] Jump = [new(new(0, 0), new(0, 2)), new(new(1, 4), new(1, 5))];

    private static PlayerSlide Straight(Direction direction, Cell from, Cell to, bool wins = false) => new(direction, [new(from, to)], to, wins);

    [Theory]
    [InlineData(Direction.Down, 0, 0, 1, 0, 1)]
    [InlineData(Direction.Right, 3, 0, 3, 3, 3)]
    [InlineData(Direction.Up, 5, 2, 1, 2, 4)]
    public void Squares_is_the_distance_travelled(Direction direction, int fromRow, int fromColumn, int toRow, int toColumn, int expected)
    {
        Assert.Equal(expected, Straight(direction, new(fromRow, fromColumn), new(toRow, toColumn)).Squares);
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
        Assert.False(Straight(Direction.Down, new(0, 0), new(3, 0), wins).Trapped);
    }

    [Fact]
    public void From_and_Reached_are_the_first_and_last_runs_ends()
    {
        var slide = new PlayerSlide(Direction.Right, Jump, new(1, 5), false);

        Assert.Equal((new Cell(0, 0), new Cell(1, 5)), (slide.From, slide.Reached));
    }

    [Fact]
    public void Squares_add_up_every_run()
    {
        Assert.Equal(3, new PlayerSlide(Direction.Right, Jump, new(1, 5), false).Squares);
    }

    [Fact]
    public void Trap_after_a_jump_is_trapped()
    {
        Assert.True(new PlayerSlide(Direction.Right, [new(new(0, 0), new(0, 2)), new(new(1, 2), new(1, 4))], new(0, 0), false).Trapped);
    }

    [Fact]
    public void Round_trip_is_not_trapped()
    {
        Assert.False(new PlayerSlide(Direction.Right, [new(new(0, 2), new(0, 4)), new(new(0, 1), new(0, 2))], new(0, 2), false).Trapped);
    }

    [Fact]
    public void Slides_with_equal_runs_in_different_lists_are_equal()
    {
        var first = new PlayerSlide(Direction.Right, Jump.ToList(), new(1, 5), false);
        var second = new PlayerSlide(Direction.Right, Jump.ToArray(), new(1, 5), false);

        Assert.Equal((first, first.GetHashCode()), (second, second.GetHashCode()));
    }

    public static TheoryData<SlideRun[]> OtherRuns => new()
    {
        new SlideRun[] { new(new(0, 0), new(0, 2)), new(new(1, 3), new(1, 5)) },
        new SlideRun[] { new(new(0, 0), new(0, 2)), new(new(1, 4), new(1, 5)), new(new(1, 5), new(1, 5)) },
    };

    [Theory]
    [MemberData(nameof(OtherRuns))]
    public void Slides_with_different_runs_are_not_equal(SlideRun[] runs)
    {
        Assert.NotEqual(new PlayerSlide(Direction.Right, Jump, new(1, 5), false), new PlayerSlide(Direction.Right, runs, new(1, 5), false));
    }

    [Fact]
    public void Null_runs_throw()
    {
        Assert.Throws<ArgumentNullException>(() => new PlayerSlide(Direction.Right, null!, new(1, 5), false));
    }

    [Fact]
    public void Empty_runs_throw()
    {
        Assert.Throws<ArgumentException>(() => new PlayerSlide(Direction.Right, [], new(1, 5), false));
    }
}
