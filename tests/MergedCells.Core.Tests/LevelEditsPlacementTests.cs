namespace MergedCells.Core.Tests;

public class LevelEditsPlacementTests
{
    [Fact]
    public void Start_moves()
    {
        Assert.Equal(new Cell(1, 1), LevelEdits.MoveStart(LevelEdits.Blank(3, 4), new(1, 1)).Map.Start);
    }

    [Fact]
    public void End_moves_and_its_square_becomes_single()
    {
        var map = Map.Create(TestMaps.Grid("AAAA", "BBBB", "CCCZ"), new(0, 0), new(2, 3));

        var result = LevelEdits.MoveEnd(map, new(2, 2));

        Assert.Equal(new Cell(2, 2), result.Map.End);
        Assert.Equal(["AAAA", "BBBB", "CCDE"], TestMaps.Layout(result.Map));
    }

    public static TheoryData<bool, string> TwistMoves => new()
    {
        { true, "wall" },
        { true, "trap" },
        { true, "teleporter" },
        { false, "wall" },
        { false, "trap" },
        { false, "teleporter" },
    };

    [Theory]
    [MemberData(nameof(TwistMoves))]
    public void Moving_onto_a_twist_clears_it(bool start, string twist)
    {
        var tiles = new Tile[3, 4];
        tiles[1, 1] = twist == "wall" ? Tile.Wall : twist == "trap" ? Tile.Trap : Tile.None;
        TeleporterPair[] pairs = twist == "teleporter" ? [TestMaps.Pair(1, 1, 1, 2)] : [];
        var map = Map.Create(LevelEdits.Blank(3, 4).RegionGrid(), new(0, 0), new(2, 3), tiles, pairs);

        var result = start ? LevelEdits.MoveStart(map, new(1, 1)) : LevelEdits.MoveEnd(map, new(1, 1));

        Assert.Equal((Tile.None, 0), (result.Map.TileAt(new(1, 1)), result.Map.Teleporters.Count));
    }

    // A 4×4 board of single squares, the start at [1, 1] and the end at [2, 2]
    private static Map Middle() => Map.Create(LevelEdits.Blank(4, 4).RegionGrid(), new(1, 1), new(2, 2));

    [Theory]
    [InlineData(true, 2, 2)]
    [InlineData(true, 1, 3)]
    [InlineData(true, 3, 0)]
    [InlineData(false, 1, 1)]
    [InlineData(false, 3, 0)]
    [InlineData(false, 0, 3)]
    public void Start_and_end_out_of_order_are_refused(bool start, int row, int column)
    {
        var map = Middle();

        var result = start ? LevelEdits.MoveStart(map, new(row, column)) : LevelEdits.MoveEnd(map, new(row, column));

        Assert.Equal((false, true), (result.Changed, result.Refusal is not null));
        Assert.Same(map, result.Map);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Moving_to_the_same_square_changes_nothing(bool start)
    {
        var map = Middle();

        var result = start ? LevelEdits.MoveStart(map, map.Start) : LevelEdits.MoveEnd(map, map.End);

        Assert.Equal((false, null), (result.Changed, result.Refusal));
    }

    [Fact]
    public void Growing_adds_single_squares_at_the_bottom_and_right()
    {
        var map = Map.Create(TestMaps.Grid("AA", "BZ"), new(0, 0), new(1, 1));

        var result = LevelEdits.Resize(map, 3, 3);

        Assert.Equal(["AAB", "CDE", "FGH"], TestMaps.Layout(result.Map));
    }

    [Fact]
    public void Shrinking_cuts_blocks_into_pieces()
    {
        var map = Map.Create(TestMaps.Grid("ABAC", "ABAD", "AAAE", "FGHZ"), new(0, 0), new(3, 3));

        var result = LevelEdits.Resize(map, 2, 4);

        Assert.Equal(["ABCD", "ABCE"], TestMaps.Layout(result.Map));
    }

    [Fact]
    public void Shrinking_drops_twists_and_pairs_outside()
    {
        var inside = TestMaps.Pair(0, 2, 1, 1);
        var map = Map.Create(
            LevelEdits.Blank(3, 4).RegionGrid(), new(0, 0), new(2, 3), TestMaps.Walls(3, 4, (2, 0)), [TestMaps.Pair(0, 1, 2, 1), inside]);

        var result = LevelEdits.Resize(map, 2, 4);

        Assert.False(result.Map.HasTiles);
        Assert.Equal([inside], result.Map.Teleporters);
    }

    [Fact]
    public void Start_and_end_outside_move_to_the_nearest_square_inside()
    {
        var map = Map.Create(LevelEdits.Blank(4, 4).RegionGrid(), new(3, 1), new(3, 3));

        var result = LevelEdits.Resize(map, 2, 3);

        Assert.Equal((new Cell(1, 1), new Cell(1, 2)), (result.Map.Start, result.Map.End));
    }

    [Fact]
    public void Clamped_end_square_becomes_single_and_plain()
    {
        var map = Map.Create(TestMaps.Grid("AAA", "AAA", "BBZ"), new(0, 0), new(2, 2), TestMaps.Walls(3, 3, (1, 2)));

        var result = LevelEdits.Resize(map, 2, 3);

        Assert.Equal(["AAA", "AAB"], TestMaps.Layout(result.Map));
        Assert.False(result.Map.HasTiles);
    }

    [Fact]
    public void Shrinking_until_start_and_end_meet_is_refused()
    {
        Assert.NotNull(LevelEdits.Resize(LevelEdits.Blank(3, 4), 1, 1).Refusal);
    }

    [Fact]
    public void Same_size_changes_nothing()
    {
        Assert.False(LevelEdits.Resize(LevelEdits.Blank(3, 4), 3, 4).Changed);
    }

    [Fact]
    public void Clear_makes_a_blank_level_the_same_size()
    {
        var map = Map.Create(TestMaps.Grid("AAAA", "BBBB", "CCCZ"), new(1, 1), new(2, 3), TestMaps.Traps(3, 4, (0, 0)));

        var result = LevelEdits.Clear(map);

        Assert.True(RegionGrids.SameLevel(LevelEdits.Blank(3, 4), result.Map));
    }

    [Fact]
    public void Clear_of_a_blank_level_changes_nothing()
    {
        Assert.False(LevelEdits.Clear(LevelEdits.Blank(3, 4)).Changed);
    }
}
