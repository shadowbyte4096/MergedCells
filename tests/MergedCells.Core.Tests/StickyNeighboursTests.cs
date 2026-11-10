using static MergedCells.Core.Tests.TestMaps;

namespace MergedCells.Core.Tests;

public class StickyNeighboursTests
{
    private static readonly Cell Middle = new(1, 1);

    // One 3x3 region with the end tucked on in a fourth column, so every neighbour of the middle square shares its region
    private static Map OneRegion(params (int Row, int Column)[] sticky) =>
        Map.Create(Grid("AAAB", "AAAC", "AAAZ"), new(0, 0), new(2, 3), Sticky(3, 4, [(1, 1), .. sticky]));

    [Fact]
    public void Lone_sticky_square_joins_nothing()
    {
        Assert.Equal(WallSides.None, StickyNeighbours.Of(OneRegion(), Middle));
    }

    [Theory]
    [InlineData(0, 1, WallSides.Up)]
    [InlineData(1, 2, WallSides.Right)]
    [InlineData(2, 1, WallSides.Down)]
    [InlineData(1, 0, WallSides.Left)]
    public void Sticky_neighbour_in_the_same_region_is_joined(int row, int column, WallSides expected)
    {
        Assert.Equal(expected, StickyNeighbours.Of(OneRegion((row, column)), Middle));
    }

    [Fact]
    public void Sticky_neighbour_in_another_region_is_not_joined()
    {
        var map = Map.Create(Grid("AAB", "CCZ"), new(0, 0), new(1, 2), Sticky(2, 3, (0, 1), (0, 2), (1, 1)));

        Assert.Equal(WallSides.None, StickyNeighbours.Of(map, new(0, 1)));
    }

    [Fact]
    public void Wall_neighbour_is_not_joined()
    {
        var tiles = Sticky(3, 4, (1, 1));
        tiles[1, 2] = Tile.Wall;
        var map = Map.Create(Grid("AAAB", "AAAC", "AAAZ"), new(0, 0), new(2, 3), tiles);

        Assert.Equal(WallSides.None, StickyNeighbours.Of(map, Middle));
    }

    [Fact]
    public void Squares_off_the_map_are_not_joined()
    {
        var map = Map.Create(Grid("AB"), new(0, 0), new(0, 1), Sticky(1, 2, (0, 0)));

        Assert.Equal(WallSides.None, StickyNeighbours.Of(map, new(0, 0)));
    }

    [Fact]
    public void Diagonal_counts_when_both_sides_next_to_it_are_joined()
    {
        var map = OneRegion((0, 1), (1, 0), (0, 0));

        Assert.Equal(WallSides.Up | WallSides.Left | WallSides.UpLeft, StickyNeighbours.Of(map, Middle));
    }

    [Fact]
    public void Diagonal_is_dropped_unless_both_sides_next_to_it_are_joined()
    {
        var map = OneRegion((0, 1), (0, 0));

        Assert.Equal(WallSides.Up, StickyNeighbours.Of(map, Middle));
    }

    [Fact]
    public void Null_map_throws()
    {
        Assert.Throws<ArgumentNullException>(() => StickyNeighbours.Of(null!, Middle));
    }
}
