using static MergedCells.Core.Tests.TestMaps;

namespace MergedCells.Core.Tests;

public class WallNeighboursTests
{
    private static readonly Cell Centre = new(2, 2);

    // 5x5, every square its own region, with a wall in the middle and on the given squares
    private static Map Around(params (int Row, int Column)[] walls)
    {
        var regions = new int[5, 5];
        for (var row = 0; row < 5; row++)
        {
            for (var column = 0; column < 5; column++)
            {
                regions[row, column] = row * 5 + column;
            }
        }
        return Map.Create(regions, new(0, 0), new(4, 4), Walls(5, 5, [.. walls, (Centre.Row, Centre.Column)]));
    }

    [Fact]
    public void Lone_wall_has_no_wall_neighbours()
    {
        Assert.Equal(WallSides.None, WallNeighbours.Of(Around(), Centre));
    }

    [Theory]
    [InlineData(1, 2, WallSides.Up)]
    [InlineData(2, 3, WallSides.Right)]
    [InlineData(3, 2, WallSides.Down)]
    [InlineData(2, 1, WallSides.Left)]
    public void Wall_beside_the_square_sets_that_side(int row, int column, WallSides expected)
    {
        Assert.Equal(expected, WallNeighbours.Of(Around((row, column)), Centre));
    }

    [Theory]
    [InlineData(1, 1, WallSides.UpLeft)]
    [InlineData(1, 3, WallSides.UpRight)]
    [InlineData(3, 3, WallSides.DownRight)]
    [InlineData(3, 1, WallSides.DownLeft)]
    public void Diagonal_wall_between_two_wall_sides_is_set(int row, int column, WallSides expected)
    {
        var map = Around((row, column), (row, 2), (2, column));

        Assert.True(WallNeighbours.Of(map, Centre).HasFlag(expected));
    }

    [Fact]
    public void Diagonal_wall_alone_is_dropped()
    {
        Assert.Equal(WallSides.None, WallNeighbours.Of(Around((1, 1)), Centre));
    }

    [Fact]
    public void Diagonal_wall_with_one_wall_side_is_dropped()
    {
        Assert.Equal(WallSides.Up, WallNeighbours.Of(Around((1, 1), (1, 2)), Centre));
    }

    [Fact]
    public void Walls_all_round_set_every_side()
    {
        var map = Around((1, 1), (1, 2), (1, 3), (2, 1), (2, 3), (3, 1), (3, 2), (3, 3));

        Assert.Equal((WallSides)255, WallNeighbours.Of(map, Centre));
    }

    [Fact]
    public void Squares_off_the_map_are_not_walls()
    {
        Assert.Equal(WallSides.Down, WallNeighbours.Of(Around((0, 4), (1, 4)), new(0, 4)));
    }
}
