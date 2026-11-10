using static MergedCells.Core.Tests.TestMaps;

namespace MergedCells.Core.Tests;

public class MapTilesTests
{
    private static Map ExampleWithWall() => Map.Create(ExampleRegions(), ExampleStart, ExampleEnd, Walls(4, 4, (1, 1)));

    [Theory]
    [InlineData(1, 1, Tile.Wall)]
    [InlineData(1, 2, Tile.None)]
    public void TileAt_returns_the_tile(int row, int column, Tile expected)
    {
        Assert.Equal(expected, ExampleWithWall().TileAt(new(row, column)));
    }

    [Fact]
    public void TileAt_is_none_on_a_map_without_tiles()
    {
        Assert.Equal(Tile.None, Example().TileAt(new(1, 1)));
    }

    [Fact]
    public void TileAt_outside_the_map_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ExampleWithWall().TileAt(new(4, 0)));
    }

    [Fact]
    public void HasTiles_is_false_without_tiles()
    {
        Assert.False(Example().HasTiles);
    }

    [Fact]
    public void HasTiles_is_false_when_every_tile_is_none()
    {
        Assert.False(Map.Create(ExampleRegions(), ExampleStart, ExampleEnd, Walls(4, 4)).HasTiles);
    }

    [Fact]
    public void HasTiles_is_true_with_a_wall()
    {
        Assert.True(ExampleWithWall().HasTiles);
    }

    [Fact]
    public void Map_keeps_its_own_copy_of_the_tiles()
    {
        var tiles = Walls(4, 4, (1, 1));
        var map = Map.Create(ExampleRegions(), ExampleStart, ExampleEnd, tiles);

        tiles[1, 1] = Tile.None;

        Assert.Equal(Tile.Wall, map.TileAt(new(1, 1)));
    }
}
