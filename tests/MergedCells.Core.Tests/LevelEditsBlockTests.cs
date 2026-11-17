namespace MergedCells.Core.Tests;

public class LevelEditsBlockTests
{
    private static readonly Cell TopLeft = new(0, 0);

    private static readonly Cell BottomRight = new(2, 3);

    // Every square its own block, the start top-left and the end bottom-right
    private static Map Blank() => LevelEdits.Blank(3, 4);

    private static Map MapOf(params string[] rows) => Map.Create(TestMaps.Grid(rows), TopLeft, BottomRight);

    [Fact]
    public void Blank_is_single_squares_with_start_top_left_and_end_bottom_right()
    {
        var map = LevelEdits.Blank(2, 3);

        Assert.Equal(["ABC", "DEF"], TestMaps.Layout(map));
        Assert.Equal((new Cell(0, 0), new Cell(1, 2)), (map.Start, map.End));
        Assert.False(map.HasTiles);
        Assert.Empty(map.Teleporters);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(0, 5)]
    public void Blank_of_fewer_than_two_squares_throws(int rows, int columns)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LevelEdits.Blank(rows, columns));
    }

    [Fact]
    public void Paint_joins_a_strip_to_the_starting_block()
    {
        var result = LevelEdits.Paint(Blank(), [new(0, 1), new(0, 2), new(0, 3)]);

        Assert.True(result.Changed);
        Assert.Equal(["ABBB", "CDEF", "GHIJ"], TestMaps.Layout(result.Map));
    }

    [Fact]
    public void Paint_through_a_turn_makes_a_bend()
    {
        var result = LevelEdits.Paint(Blank(), [new(1, 0), new(1, 1), new(1, 2), new(2, 2)]);

        Assert.Equal(["ABCD", "EEEF", "GHEI"], TestMaps.Layout(result.Map));
    }

    [Fact]
    public void Paint_that_cuts_a_block_in_two_leaves_two_blocks()
    {
        var result = LevelEdits.Paint(MapOf("AAAA", "BCDE", "FGHZ"), [new(1, 1), new(0, 1)]);

        Assert.Equal(["ABCC", "DBEF", "GHIJ"], TestMaps.Layout(result.Map));
    }

    [Fact]
    public void Paint_stops_at_the_end()
    {
        var map = Map.Create(TestMaps.Grid("ABCD", "EFGH", "IJKL"), TopLeft, new(1, 2));

        var result = LevelEdits.Paint(map, [new(1, 0), new(1, 1), new(1, 2), new(1, 3)]);

        Assert.Equal(["ABCD", "EEFG", "HIJK"], TestMaps.Layout(result.Map));
    }

    [Fact]
    public void Paint_from_the_end_changes_nothing()
    {
        var map = Blank();

        var result = LevelEdits.Paint(map, [BottomRight, new(2, 2)]);

        Assert.False(result.Changed);
        Assert.Same(map, result.Map);
    }

    [Fact]
    public void Paint_over_squares_already_in_the_block_changes_nothing()
    {
        var result = LevelEdits.Paint(MapOf("AAAB", "CDEF", "GHIZ"), [new(0, 0), new(0, 1), new(0, 2)]);

        Assert.False(result.Changed);
    }

    [Fact]
    public void Paint_keeps_twists()
    {
        var map = Map.Create(
            TestMaps.Grid("ABCD", "EFGH", "IJKL"), TopLeft, BottomRight, TestMaps.Walls(3, 4, (1, 1)), [TestMaps.Pair(1, 2, 2, 0)]);

        var result = LevelEdits.Paint(map, [new(1, 0), new(1, 1), new(1, 2)]);

        Assert.Equal(Tile.Wall, result.Map.TileAt(new(1, 1)));
        Assert.Equal(map.Teleporters, result.Map.Teleporters);
    }

    [Fact]
    public void Paint_with_a_gap_in_the_path_throws()
    {
        Assert.Throws<ArgumentException>(() => LevelEdits.Paint(Blank(), [new(0, 0), new(0, 2)]));
    }

    [Fact]
    public void Split_makes_a_square_its_own_block_and_splits_the_rest_into_pieces()
    {
        var result = LevelEdits.Split(MapOf("AAAA", "BCDE", "FGHZ"), new(0, 1));

        Assert.True(result.Changed);
        Assert.Equal(["ABCC", "DEFG", "HIJK"], TestMaps.Layout(result.Map));
    }

    [Fact]
    public void Split_of_a_lone_square_changes_nothing()
    {
        Assert.False(LevelEdits.Split(Blank(), new(1, 1)).Changed);
    }

    [Fact]
    public void Results_are_relabelled_in_reading_order()
    {
        var result = LevelEdits.Paint(LevelEdits.Blank(2, 3), [new(0, 1), new(0, 2)]);

        Assert.Equal(new[,] { { 1, 2, 2 }, { 3, 4, 5 } }, result.Map.RegionGrid());
    }
}
