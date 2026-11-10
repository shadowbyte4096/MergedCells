using static MergedCells.Core.Tests.TestMaps;

namespace MergedCells.Core.Tests;

public class WallSlideTests
{
    // Level 26: the top lane's wall sits one square past the column above the end
    private static Map TopLaneWall() => Map.Create(Grid("AAAAA", "BCDEF"), new(0, 0), new(1, 2), Walls(2, 5, (0, 3)));

    [Fact]
    public void Slide_stops_on_the_square_before_a_wall()
    {
        Assert.Equal(new SlideResult(new(0, 2), false), TopLaneWall().Slide(new(0, 0), Direction.Right));
    }

    [Fact]
    public void Wall_beside_the_player_makes_the_slide_a_no_op()
    {
        Assert.Equal(new SlideResult(new(0, 2), false), TopLaneWall().Slide(new(0, 2), Direction.Right));
    }

    [Fact]
    public void Wall_on_the_first_square_of_a_new_region_stops_before_it()
    {
        var map = Map.Create(Grid("AAB", "CCD"), new(0, 0), new(1, 2), Walls(2, 3, (0, 2)));

        Assert.Equal(new SlideResult(new(0, 1), false), map.Slide(new(0, 0), Direction.Right));
    }

    [Fact]
    public void Wall_before_the_end_stops_short_of_it()
    {
        var map = Map.Create(Grid("AAAB"), new(0, 0), new(0, 3), Walls(1, 4, (0, 2)));

        Assert.Equal(new SlideResult(new(0, 1), false), map.Slide(new(0, 0), Direction.Right));
    }

    [Fact]
    public void Tiles_with_no_walls_slide_as_a_plain_map()
    {
        var map = Map.Create(ExampleRegions(), ExampleStart, ExampleEnd, Walls(4, 4));

        var first = map.Slide(ExampleStart, Direction.Down);
        var second = map.Slide(first.Stop, Direction.Down);

        Assert.Equal(new SlideResult(ExampleEnd, true), map.Slide(second.Stop, Direction.Right));
    }
}
