using static MergedCells.Core.Tests.TestMaps;

namespace MergedCells.Core.Tests;

public class StickySlideTests
{
    // Level 28 on its side: the top lane's sticky square sits right above the end
    private static Map TopLane(params (int Row, int Column)[] sticky) =>
        Map.Create(Grid("AAAAA", "BCDEF"), new(0, 0), new(1, 2), Sticky(2, 5, sticky));

    [Fact]
    public void Slide_stops_on_a_sticky_square_inside_a_region()
    {
        Assert.Equal(new SlideResult(new(0, 2), false), TopLane((0, 2)).Slide(new(0, 0), Direction.Right));
    }

    [Fact]
    public void Slide_off_a_sticky_square_runs_to_the_regions_edge()
    {
        Assert.Equal(new SlideResult(new(0, 4), false), TopLane((0, 2)).Slide(new(0, 2), Direction.Right));
    }

    [Fact]
    public void Second_sticky_square_in_a_row_stops_the_slide_again()
    {
        Assert.Equal(new SlideResult(new(0, 3), false), TopLane((0, 2), (0, 3)).Slide(new(0, 2), Direction.Right));
    }

    [Fact]
    public void Sticky_start_does_not_hold_the_player()
    {
        Assert.Equal(new SlideResult(new(0, 4), false), TopLane((0, 0)).Slide(new(0, 0), Direction.Right));
    }

    [Fact]
    public void Sticky_square_on_the_first_square_of_a_new_region_stops_the_slide_there()
    {
        var map = Map.Create(Grid("AAB", "CCD"), new(0, 0), new(1, 2), Sticky(2, 3, (0, 2)));

        Assert.Equal(new SlideResult(new(0, 2), false), map.Slide(new(0, 0), Direction.Right));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Wall_behind_a_sticky_square_leaves_the_stop_on_the_sticky_square(int fromColumn)
    {
        var tiles = Sticky(2, 5, (0, 2));
        tiles[0, 3] = Tile.Wall;
        var map = Map.Create(Grid("AAAAA", "BCDEF"), new(0, 0), new(1, 2), tiles);

        Assert.Equal(new SlideResult(new(0, 2), false), map.Slide(new(0, fromColumn), Direction.Right));
    }

    // 5 rows by 6 columns with the sticky square off row 0, so reading the tiles with rows and columns swapped looks at another square
    [Theory]
    [InlineData(0, 3, Direction.Down)]
    [InlineData(4, 3, Direction.Up)]
    [InlineData(2, 0, Direction.Right)]
    [InlineData(2, 5, Direction.Left)]
    public void Slide_from_any_side_stops_on_a_sticky_square_on_a_non_square_map(int row, int column, Direction direction)
    {
        var map = Map.Create(Grid("AAAAAA", "AAAAAA", "AAAAAA", "AAAAAA", "AAAAAZ"), new(0, 0), new(4, 5), Sticky(5, 6, (2, 3)));

        Assert.Equal(new SlideResult(new(2, 3), false), map.Slide(new(row, column), direction));
    }
}
