namespace MergedCells.Core.Tests;

public class LevelEditsEraseTests
{
    private static readonly Cell Start = new(0, 0);

    private static readonly Cell End = new(2, 3);

    // A 3×4 board of single squares with the given twists
    private static Map Singles(Tile[,]? tiles = null, params TeleporterPair[] pairs) =>
        Map.Create(TestMaps.Grid("ABCD", "EFGH", "IJKL"), Start, End, tiles, pairs);

    // A row-wide block holding a wall at (0, 1), a trap at (0, 2) and one end of a pair at (0, 3)
    private static Map MixedRow()
    {
        var tiles = new Tile[3, 4];
        tiles[0, 1] = Tile.Wall;
        tiles[0, 2] = Tile.Trap;
        return Map.Create(TestMaps.Grid("AAAA", "EFGH", "IJKL"), Start, End, tiles, [TestMaps.Pair(0, 3, 1, 1)]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Stroke_from_a_twist_leaves_the_blocks(int column)
    {
        var result = LevelEdits.Erase(MixedRow(), [new(0, column)]);

        Assert.Equal("AAAA", TestMaps.Layout(result.Map)[0]);
    }

    [Theory]
    [InlineData(1, false, true, true)]
    [InlineData(2, true, false, true)]
    [InlineData(3, true, true, false)]
    public void Stroke_removes_only_its_first_square_s_kind_of_twist(int first, bool wallLeft, bool trapLeft, bool pairLeft)
    {
        Cell[] stroke = [new(0, first), .. new[] { 1, 2, 3 }.Where(column => column != first).Select(column => new Cell(0, column))];

        var result = LevelEdits.Erase(MixedRow(), stroke);

        Assert.Equal(
            (wallLeft, trapLeft, pairLeft),
            (result.Map.TileAt(new(0, 1)) == Tile.Wall, result.Map.TileAt(new(0, 2)) == Tile.Trap, result.Map.Teleporters.Count == 1));
    }

    [Fact]
    public void Teleporter_tap_takes_its_pair_and_later_letters_move_up()
    {
        var second = TestMaps.Pair(1, 0, 1, 1);
        var third = TestMaps.Pair(1, 2, 2, 0);

        var result = LevelEdits.Erase(Singles(null, TestMaps.Pair(0, 1, 0, 2), second, third), [new(0, 2)]);

        Assert.Equal([second, third], result.Map.Teleporters);
    }

    [Fact]
    public void Stroke_across_both_squares_of_a_pair_takes_only_that_pair()
    {
        var other = TestMaps.Pair(1, 0, 1, 1);

        var result = LevelEdits.Erase(Singles(null, TestMaps.Pair(0, 1, 0, 2), other), [new(0, 1), new(0, 2)]);

        Assert.Equal([other], result.Map.Teleporters);
    }

    [Fact]
    public void Stroke_from_a_plain_square_makes_each_square_single()
    {
        var map = Map.Create(TestMaps.Grid("AAAA", "EFGH", "IJKL"), Start, End);

        var result = LevelEdits.Erase(map, [new(0, 1), new(0, 2)]);

        Assert.Equal(["ABCD", "EFGH", "IJKL"], TestMaps.Layout(result.Map));
    }

    [Fact]
    public void Stroke_from_a_plain_square_keeps_the_twists()
    {
        var map = Map.Create(TestMaps.Grid("AAAA", "EFGH", "IJKL"), Start, End, TestMaps.Walls(3, 4, (0, 2)), [TestMaps.Pair(0, 3, 1, 1)]);

        var result = LevelEdits.Erase(map, [new(0, 1), new(0, 2), new(0, 3)]);

        Assert.Equal((Tile.Wall, 1), (result.Map.TileAt(new(0, 2)), result.Map.Teleporters.Count));
    }

    [Theory]
    [InlineData(0, 0, 0, 1)]
    [InlineData(2, 3, 2, 2)]
    public void Stroke_from_the_start_or_end_keeps_the_twists(int row, int column, int nextRow, int nextColumn)
    {
        var map = Singles(TestMaps.Traps(3, 4, (nextRow, nextColumn)));

        var result = LevelEdits.Erase(map, [new(row, column), new(nextRow, nextColumn)]);

        Assert.Equal(Tile.Trap, result.Map.TileAt(new(nextRow, nextColumn)));
    }

    [Fact]
    public void Erase_of_a_plain_single_square_changes_nothing()
    {
        Assert.False(LevelEdits.Erase(Singles(), [new(1, 1)]).Changed);
    }

    [Fact]
    public void Empty_stroke_throws()
    {
        Assert.Throws<ArgumentException>(() => LevelEdits.Erase(Singles(), []));
    }

    [Fact]
    public void Targets_of_a_twist_stroke_are_its_twist_squares()
    {
        var tiles = TestMaps.Walls(3, 4, (0, 1), (0, 3));
        tiles[0, 2] = Tile.Trap;

        var targets = LevelEdits.EraseTargets(Singles(tiles), [new(0, 1), new(0, 2), new(0, 3)]);

        Assert.Equal([new(0, 1), new(0, 3)], targets);
    }

    [Fact]
    public void Targets_of_a_teleporter_stroke_include_the_partners()
    {
        var targets = LevelEdits.EraseTargets(Singles(null, TestMaps.Pair(0, 1, 2, 0)), [new(0, 1), new(0, 2)]);

        Assert.Equal([new(0, 1), new(2, 0)], targets);
    }

    [Fact]
    public void Targets_of_a_plain_stroke_are_every_square_once()
    {
        var targets = LevelEdits.EraseTargets(Singles(TestMaps.Walls(3, 4, (0, 2))), [new(0, 1), new(0, 2), new(0, 1)]);

        Assert.Equal([new(0, 1), new(0, 2)], targets);
    }
}
