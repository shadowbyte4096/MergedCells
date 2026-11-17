namespace MergedCells.Core.Tests;

public class LevelEditsTwistTests
{
    private static readonly Cell Start = new(0, 0);

    private static readonly Cell End = new(2, 3);

    // A 3×4 board of single squares with the given twists
    private static Map Singles(Tile[,]? tiles = null, params TeleporterPair[] pairs) =>
        Map.Create(TestMaps.Grid("ABCD", "EFGH", "IJKL"), Start, End, tiles, pairs);

    [Fact]
    public void Erase_removes_a_wall_and_makes_the_square_single()
    {
        var map = Map.Create(TestMaps.Grid("AAAA", "EFGH", "IJKL"), Start, End, TestMaps.Walls(3, 4, (0, 1)));

        var result = LevelEdits.Erase(map, [new(0, 1)]);

        Assert.Equal(Tile.None, result.Map.TileAt(new(0, 1)));
        Assert.Equal(["ABCC", "DEFG", "HIJK"], TestMaps.Layout(result.Map));
    }

    [Fact]
    public void Erase_removes_a_trap()
    {
        var result = LevelEdits.Erase(Singles(TestMaps.Traps(3, 4, (1, 1))), [new(1, 1)]);

        Assert.False(result.Map.HasTiles);
    }

    [Fact]
    public void Erase_of_a_teleporter_takes_its_pair_and_later_letters_move_up()
    {
        var second = TestMaps.Pair(1, 0, 1, 1);
        var third = TestMaps.Pair(1, 2, 2, 0);

        var result = LevelEdits.Erase(Singles(null, TestMaps.Pair(0, 1, 0, 2), second, third), [new(0, 2)]);

        Assert.Equal([second, third], result.Map.Teleporters);
    }

    [Fact]
    public void Erase_leaves_the_partner_square_in_its_block()
    {
        var map = Map.Create(TestMaps.Grid("AAAA", "EFGH", "IJKL"), Start, End, teleporters: [TestMaps.Pair(0, 1, 1, 1)]);

        var result = LevelEdits.Erase(map, [new(1, 1)]);

        Assert.Equal("AAAA", TestMaps.Layout(result.Map)[0]);
    }

    [Fact]
    public void Erase_of_a_plain_single_square_changes_nothing()
    {
        Assert.False(LevelEdits.Erase(Singles(), [new(1, 1)]).Changed);
    }

    [Theory]
    [InlineData(Tile.Wall)]
    [InlineData(Tile.Trap)]
    public void Tool_marks_squares(Tile tile)
    {
        var result = LevelEdits.PlaceTile(Singles(), [new(1, 1), new(1, 2)], tile);

        Assert.Equal((tile, tile), (result.Map.TileAt(new(1, 1)), result.Map.TileAt(new(1, 2))));
    }

    [Theory]
    [InlineData(Tile.Wall, Tile.Trap)]
    [InlineData(Tile.Trap, Tile.Wall)]
    public void One_tile_replaces_the_other(Tile under, Tile placed)
    {
        var tiles = new Tile[3, 4];
        tiles[1, 1] = under;

        var result = LevelEdits.PlaceTile(Singles(tiles), [new(1, 1)], placed);

        Assert.Equal(placed, result.Map.TileAt(new(1, 1)));
    }

    [Fact]
    public void Tiles_skip_the_start_end_and_teleporters()
    {
        var result = LevelEdits.PlaceTile(Singles(null, TestMaps.Pair(1, 1, 1, 2)), [Start, new(0, 1), End, new(1, 1)], Tile.Wall);

        Assert.Equal(
            (Tile.None, Tile.Wall, Tile.None, Tile.None),
            (result.Map.TileAt(Start), result.Map.TileAt(new(0, 1)), result.Map.TileAt(End), result.Map.TileAt(new(1, 1))));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 3)]
    [InlineData(1, 1)]
    public void Tap_on_a_skipped_square_is_refused(int row, int column)
    {
        var map = Singles(null, TestMaps.Pair(1, 1, 1, 2));

        var result = LevelEdits.PlaceTile(map, [new(row, column)], Tile.Trap);

        Assert.Equal((false, true), (result.Changed, result.Refusal is not null));
        Assert.Same(map, result.Map);
    }

    [Theory]
    [InlineData(Tile.Sticky)]
    [InlineData(Tile.None)]
    public void Placing_sticky_or_none_throws(Tile tile)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LevelEdits.PlaceTile(Singles(), [new(1, 1)], tile));
    }

    [Fact]
    public void Pair_is_added_last()
    {
        var first = TestMaps.Pair(0, 1, 0, 2);

        var result = LevelEdits.AddTeleporters(Singles(null, first), new(1, 0), new(1, 1));

        Assert.Equal([first, TestMaps.Pair(1, 0, 1, 1)], result.Map.Teleporters);
    }

    [Fact]
    public void Pair_clears_walls_and_traps_under_it()
    {
        var tiles = new Tile[3, 4];
        tiles[1, 0] = Tile.Wall;
        tiles[1, 1] = Tile.Trap;

        var result = LevelEdits.AddTeleporters(Singles(tiles), new(1, 0), new(1, 1));

        Assert.False(result.Map.HasTiles);
    }

    [Theory]
    [InlineData(1, 1, 1, 1)]
    [InlineData(0, 0, 1, 1)]
    [InlineData(1, 1, 2, 3)]
    [InlineData(0, 1, 1, 1)]
    [InlineData(1, 1, 0, 2)]
    public void Pair_on_a_square_that_can_t_hold_one_is_refused(int firstRow, int firstColumn, int secondRow, int secondColumn)
    {
        var map = Singles(null, TestMaps.Pair(0, 1, 0, 2));

        var result = LevelEdits.AddTeleporters(map, new(firstRow, firstColumn), new(secondRow, secondColumn));

        Assert.Equal((false, true), (result.Changed, result.Refusal is not null));
        Assert.Same(map, result.Map);
    }

    // Squares 1 to 90 of a 10×10 board, in pairs, clear of the start (square 0) and the end (square 99)
    private static Map FullOfTeleporters() => Map.Create(
        LevelEdits.Blank(10, 10).RegionGrid(),
        new(0, 0),
        new(9, 9),
        teleporters: Enumerable.Range(0, TeleporterLetters.Count)
            .Select(pair => new TeleporterPair(new((pair * 2 + 1) / 10, (pair * 2 + 1) % 10), new((pair * 2 + 2) / 10, (pair * 2 + 2) % 10)))
            .ToArray());

    [Fact]
    public void Forty_sixth_pair_is_refused()
    {
        var result = LevelEdits.AddTeleporters(FullOfTeleporters(), new(9, 1), new(9, 2));

        Assert.NotNull(result.Refusal);
    }

    [Fact]
    public void Forty_sixth_pair_s_first_square_is_refused()
    {
        Assert.NotNull(LevelEdits.CanHoldTeleporter(FullOfTeleporters(), new(9, 1)));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 3)]
    [InlineData(0, 1)]
    public void Square_that_can_t_hold_a_teleporter_is_refused(int row, int column)
    {
        Assert.NotNull(LevelEdits.CanHoldTeleporter(Singles(null, TestMaps.Pair(0, 1, 0, 2)), new(row, column)));
    }

    [Fact]
    public void Plain_square_can_hold_a_teleporter()
    {
        Assert.Null(LevelEdits.CanHoldTeleporter(Singles(TestMaps.Walls(3, 4, (1, 1))), new(1, 1)));
    }
}
