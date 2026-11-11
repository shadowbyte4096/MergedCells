using System.Text.Json;

namespace MergedCells.Core.Tests;

public class LevelFileTilesTests
{
    private const string NoWalls = "[[0,0,0,0],[0,0,0,0],[0,0,0,0],[0,0,0,0]]";

    private static string WithTiles(string tiles) =>
        """{"id":3,"name":"Walled","chapter":1,"regions":[[1,1,2,3],[4,5,2,3],[4,5,6,6],[7,7,7,8]],"tiles":TILES,"teleporters":null,"start":[0,0],"end":[3,3],"par":3}"""
            .Replace("TILES", tiles);

    [Theory]
    [InlineData(1, 1, Tile.Wall)]
    [InlineData(1, 2, Tile.None)]
    public void Tiles_are_read_into_the_map(int row, int column, Tile expected)
    {
        var map = LevelFile.Parse(WithTiles("[[0,0,0,0],[0,1,0,0],[0,0,0,0],[0,0,0,0]]")).Map;

        Assert.Equal(expected, map.TileAt(new(row, column)));
    }

    [Theory]
    [InlineData("null")]
    [InlineData(NoWalls)]
    public void Tiles_without_walls_mean_a_plain_map(string tiles)
    {
        Assert.False(LevelFile.Parse(WithTiles(tiles)).Map.HasTiles);
    }

    [Theory]
    [InlineData("[[0,0,0,0],[0,0,0],[0,0,0,0],[0,0,0,0]]", "Row 1")]
    [InlineData("[[0,0,0,0],[0,0,0,0],null,[0,0,0,0]]", "Row 2")]
    public void Tiles_row_that_is_null_or_the_wrong_length_is_rejected(string tiles, string row)
    {
        var ex = Assert.Throws<JsonException>(() => LevelFile.Parse(WithTiles(tiles)));

        Assert.Contains(row + " of \"tiles\"", ex.Message);
    }

    [Fact]
    public void Tiles_with_a_different_row_count_are_rejected()
    {
        var ex = Assert.Throws<InvalidMapException>(() => LevelFile.Parse(WithTiles("[[0,0,0,0],[0,0,0,0],[0,0,0,0]]")));

        Assert.Equal(MapProblemKind.TilesSizeMismatch, Assert.Single(ex.Problems).Kind);
    }

    [Fact]
    public void Tile_code_2_is_read_as_a_sticky_square()
    {
        var map = LevelFile.Parse(WithTiles("[[0,0,0,0],[0,2,0,0],[0,0,0,0],[0,0,0,0]]")).Map;

        Assert.Equal(Tile.Sticky, map.TileAt(new(1, 1)));
    }

    [Fact]
    public void Tile_code_3_is_read_as_a_trap()
    {
        var map = LevelFile.Parse(WithTiles("[[0,0,0,0],[0,3,0,0],[0,0,0,0],[0,0,0,0]]")).Map;

        Assert.Equal(Tile.Trap, map.TileAt(new(1, 1)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(256)]
    public void Unknown_tile_code_is_rejected(int code)
    {
        var ex = Assert.Throws<JsonException>(() => LevelFile.Parse(WithTiles(NoWalls.Replace("[0,0,0,0]]", $"[0,{code},0,0]]"))));

        Assert.Contains($"{code} at [3, 1]", ex.Message);
    }

    [Fact]
    public void Wall_on_the_start_is_rejected()
    {
        var ex = Assert.Throws<InvalidMapException>(() => LevelFile.Parse(WithTiles("[[1,0,0,0],[0,0,0,0],[0,0,0,0],[0,0,0,0]]")));

        Assert.Equal(MapProblemKind.WallOnStart, Assert.Single(ex.Problems).Kind);
    }
}
