using AutoFixture;
using static MergedCells.Core.Tests.TestMaps;

namespace MergedCells.Core.Tests;

public class SlideTests
{
    private static Map Lanes() => Map.Create(
        Grid(
            "AAAAA",
            "BCDEF",
            "BCDEF",
            "BCDEF",
            "BCDEG"),
        new(0, 0),
        new(4, 4));

    private static Map SingleSquare() => Map.Create(
        Grid(
            "AAA",
            "BCD",
            "EEF"),
        new(0, 0),
        new(2, 2));

    [Fact]
    public void Example_down_down_right_reaches_the_end()
    {
        var map = Example();

        var first = map.Slide(new(0, 0), Direction.Down);
        var second = map.Slide(first.Stop, Direction.Down);
        var third = map.Slide(second.Stop, Direction.Right);

        Assert.Equal(new SlideResult(new(1, 0), false), first);
        Assert.Equal(new SlideResult(new(3, 0), false), second);
        Assert.Equal(new SlideResult(new(3, 3), true), third);
    }

    [Fact]
    public void Generated_region_ids_slide_the_same_way()
    {
        var ids = new Fixture().CreateMany<int>(8).ToList();
        var map = Map.Create(Relabel(ExampleRegions(), ids), ExampleStart, ExampleEnd);

        var first = map.Slide(new(0, 0), Direction.Down);
        var second = map.Slide(first.Stop, Direction.Down);
        var third = map.Slide(second.Stop, Direction.Right);

        Assert.Equal(new SlideResult(new(1, 0), false), first);
        Assert.Equal(new SlideResult(new(3, 0), false), second);
        Assert.Equal(new SlideResult(new(3, 3), true), third);
    }

    [Theory]
    [InlineData(0, 0, Direction.Up)]
    [InlineData(0, 0, Direction.Left)]
    [InlineData(0, 3, Direction.Up)]
    [InlineData(0, 3, Direction.Right)]
    [InlineData(3, 0, Direction.Down)]
    [InlineData(3, 0, Direction.Left)]
    [InlineData(3, 3, Direction.Down)]
    [InlineData(3, 3, Direction.Right)]
    [InlineData(0, 1, Direction.Up)]
    [InlineData(1, 0, Direction.Left)]
    [InlineData(1, 3, Direction.Right)]
    [InlineData(3, 1, Direction.Down)]
    public void Slide_off_the_map_edge_is_a_no_op(int row, int column, Direction direction)
    {
        var from = new Cell(row, column);

        Assert.Equal(new SlideResult(from, false), Example().Slide(from, direction));
    }

    [Theory]
    [InlineData(0, 0, Direction.Right, 0, 4)]
    [InlineData(0, 4, Direction.Left, 0, 0)]
    [InlineData(1, 0, Direction.Down, 4, 0)]
    public void Lane_along_the_direction_carries_to_the_last_square_at_the_map_edge(
        int row, int column, Direction direction, int stopRow, int stopColumn)
    {
        Assert.Equal(new SlideResult(new(stopRow, stopColumn), false), Lanes().Slide(new(row, column), direction));
    }

    [Theory]
    [InlineData(4, 1, Direction.Up, 0, 1)]
    [InlineData(0, 2, Direction.Down, 1, 2)]
    public void Slide_stops_on_the_first_square_inside_the_next_rectangle(
        int row, int column, Direction direction, int stopRow, int stopColumn)
    {
        Assert.Equal(new SlideResult(new(stopRow, stopColumn), false), Lanes().Slide(new(row, column), direction));
    }

    [Theory]
    [InlineData(2, 1, Direction.Right, 2, 2)]
    [InlineData(2, 3, Direction.Left, 2, 2)]
    public void Strip_crossed_at_a_right_angle_stops_after_one_step(
        int row, int column, Direction direction, int stopRow, int stopColumn)
    {
        Assert.Equal(new SlideResult(new(stopRow, stopColumn), false), Lanes().Slide(new(row, column), direction));
    }

    [Theory]
    [InlineData(2, 0, Direction.Up, 0, 0)]
    [InlineData(0, 0, Direction.Right, 0, 3)]
    public void Slide_along_an_arm_of_an_L_shaped_region_stops_on_the_next_region(
        int row, int column, Direction direction, int stopRow, int stopColumn)
    {
        var map = Map.Create(Grid("AAAB", "ACCB", "ADDE"), new(0, 0), new(2, 3));

        Assert.Equal(new SlideResult(new(stopRow, stopColumn), false), map.Slide(new(row, column), direction));
    }

    [Fact]
    public void Ladder_of_side_by_side_strips_moves_one_square_per_slide()
    {
        var map = Lanes();
        var stops = new List<Cell>();
        var at = new Cell(2, 0);
        for (var i = 0; i < 4; i++)
        {
            at = map.Slide(at, Direction.Right).Stop;
            stops.Add(at);
        }

        Assert.Equal(new[] { new Cell(2, 1), new Cell(2, 2), new Cell(2, 3), new Cell(2, 4) }, stops);
    }

    [Theory]
    [InlineData(Direction.Up, 0, 1)]
    [InlineData(Direction.Down, 2, 1)]
    [InlineData(Direction.Left, 1, 0)]
    [InlineData(Direction.Right, 1, 2)]
    public void Single_square_region_is_left_one_step_at_a_time(Direction direction, int stopRow, int stopColumn)
    {
        Assert.Equal(new SlideResult(new(stopRow, stopColumn), false), SingleSquare().Slide(new(1, 1), direction));
    }

    [Fact]
    public void Sliding_into_a_single_square_region_stops_on_it()
    {
        Assert.Equal(new SlideResult(new(1, 1), false), SingleSquare().Slide(new(0, 1), Direction.Down));
    }

    [Theory]
    [InlineData(3, 0, Direction.Right)]
    [InlineData(2, 3, Direction.Down)]
    public void Reaching_the_end_wins_and_stops_on_it(int row, int column, Direction direction)
    {
        Assert.Equal(new SlideResult(new(3, 3), true), Example().Slide(new(row, column), direction));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(4, 0)]
    [InlineData(0, 4)]
    public void Slide_from_outside_the_map_throws(int row, int column)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Example().Slide(new(row, column), Direction.Right));
    }

    [Theory]
    [InlineData(0, 0, Direction.Right, 0, 4, false)]
    [InlineData(0, 0, Direction.Down, 1, 0, false)]
    [InlineData(1, 0, Direction.Right, 1, 4, true)]
    public void Non_square_map_slides_along_rows_and_columns(
        int row, int column, Direction direction, int stopRow, int stopColumn, bool reachedEnd)
    {
        var map = Map.Create(Grid("AAAAB", "CCCCD"), new(0, 0), new(1, 4));

        Assert.Equal(new SlideResult(new(stopRow, stopColumn), reachedEnd), map.Slide(new(row, column), direction));
    }

    [Theory]
    [InlineData(0, 0, Direction.Up)]
    [InlineData(0, 1, Direction.Down)]
    public void Slide_across_a_one_row_map_is_a_no_op(int row, int column, Direction direction)
    {
        var map = Map.Create(Grid("AAB"), new(0, 0), new(0, 2));
        var from = new Cell(row, column);

        Assert.Equal(new SlideResult(from, false), map.Slide(from, direction));
    }

    [Theory]
    [InlineData(0, 0, Direction.Left)]
    [InlineData(1, 0, Direction.Right)]
    public void Slide_across_a_one_column_map_is_a_no_op(int row, int column, Direction direction)
    {
        var map = Map.Create(Grid("A", "A", "B"), new(0, 0), new(2, 0));
        var from = new Cell(row, column);

        Assert.Equal(new SlideResult(from, false), map.Slide(from, direction));
    }

    [Theory]
    [InlineData(0, 0, 0, 99, false)]
    [InlineData(99, 0, 99, 99, true)]
    public void Full_width_slides_work_on_a_100_by_100_map(int row, int column, int stopRow, int stopColumn, bool reachedEnd)
    {
        Assert.Equal(new SlideResult(new(stopRow, stopColumn), reachedEnd), RowStrips100().Slide(new(row, column), Direction.Right));
    }
}
