using static MergedCells.Core.Tests.TestMaps;

namespace MergedCells.Core.Tests;

public class TeleporterSlideTests
{
    private static readonly Cell Start = new(0, 0);

    // Three lanes, with the end at the bottom right
    private static Map Lanes(TeleporterPair[] pairs, Tile[,]? tiles = null) =>
        Map.Create(Grid("AAAAAA", "BBBBBB", "CCCCCZ"), Start, new(2, 5), tiles, pairs);

    private static Tile[,] Lane(Tile tile, int row, int column)
    {
        var tiles = new Tile[3, 6];
        tiles[row, column] = tile;
        return tiles;
    }

    [Fact]
    public void Slide_into_a_teleporter_comes_out_of_its_partner_and_carries_on()
    {
        Assert.Equal(new SlideResult(new(1, 5), false, Teleported: true), TeleporterLane().Slide(Start, Direction.Right));
    }

    [Fact]
    public void Teleporter_on_a_new_regions_first_square_teleports()
    {
        var map = Map.Create(Grid("AABBBB", "CCCCCC", "DDDDDZ"), Start, new(2, 5), teleporters: [Pair(0, 2, 1, 1)]);

        Assert.Equal(new SlideResult(new(1, 5), false, Teleported: true), map.Slide(Start, Direction.Right));
    }

    [Fact]
    public void Exit_next_to_the_map_edge_stops_on_the_exit()
    {
        Assert.Equal(new SlideResult(new(1, 5), false, Teleported: true), Lanes([Pair(0, 2, 1, 5)]).Slide(Start, Direction.Right));
    }

    [Fact]
    public void Exit_next_to_a_wall_stops_on_the_exit()
    {
        var map = Lanes([Pair(0, 2, 1, 4)], Lane(Tile.Wall, 1, 5));

        Assert.Equal(new SlideResult(new(1, 4), false, Teleported: true), map.Slide(Start, Direction.Right));
    }

    [Fact]
    public void New_region_after_the_exit_stops_on_its_first_square()
    {
        var map = Map.Create(Grid("AAAAAA", "BBBCCC", "CCCCCZ"), Start, new(2, 5), teleporters: [Pair(0, 2, 1, 0)]);

        Assert.Equal(new SlideResult(new(1, 3), false, Teleported: true), map.Slide(Start, Direction.Right));
    }

    [Fact]
    public void Sticky_square_after_the_exit_stops_on_it()
    {
        var map = Lanes([Pair(0, 2, 1, 2)], Lane(Tile.Sticky, 1, 4));

        Assert.Equal(new SlideResult(new(1, 4), false, Teleported: true), map.Slide(Start, Direction.Right));
    }

    [Fact]
    public void Trap_after_the_exit_sends_the_player_to_the_start()
    {
        var map = Lanes([Pair(0, 2, 1, 2)], Lane(Tile.Trap, 1, 4));

        Assert.Equal(new SlideResult(Start, false, Trapped: true, Teleported: true), map.Slide(Start, Direction.Right));
    }

    [Fact]
    public void End_after_the_exit_wins()
    {
        Assert.Equal(new SlideResult(new(2, 5), true, Teleported: true), Lanes([Pair(0, 2, 2, 3)]).Slide(Start, Direction.Right));
    }

    [Fact]
    public void Chained_teleporters_carry_on_through_both_pairs()
    {
        var map = Lanes([Pair(0, 2, 1, 0), Pair(1, 3, 0, 4)]);

        Assert.Equal(new SlideResult(new(0, 5), false, Teleported: true), map.Slide(Start, Direction.Right));
    }

    [Theory]
    [InlineData(0, 0, Direction.Right, 1, 5)]
    [InlineData(1, 5, Direction.Left, 0, 0)]
    public void Either_square_sends_the_player_to_the_other(int row, int column, Direction direction, int stopRow, int stopColumn)
    {
        Assert.Equal(
            new SlideResult(new(stopRow, stopColumn), false, Teleported: true),
            TeleporterLane().Slide(new(row, column), direction));
    }

    public static TheoryData<Tile, SlideResult> TilesBeforeATeleporter => new()
    {
        { Tile.Wall, new SlideResult(new(0, 1), false) },
        { Tile.Sticky, new SlideResult(new(0, 2), false) },
        { Tile.Trap, new SlideResult(Start, false, Trapped: true) },
    };

    [Theory]
    [MemberData(nameof(TilesBeforeATeleporter))]
    public void Tile_before_a_teleporter_acts_first(Tile tile, SlideResult expected)
    {
        Assert.Equal(expected, Lanes([Pair(0, 3, 1, 4)], Lane(tile, 0, 2)).Slide(Start, Direction.Right));
    }

    [Theory]
    [InlineData(Direction.Right, 1, 5)]
    [InlineData(Direction.Down, 2, 4)]
    public void Player_on_a_teleporter_slides_off_it_as_normal(Direction direction, int stopRow, int stopColumn)
    {
        Assert.Equal(new SlideResult(new(stopRow, stopColumn), false), TeleporterLane().Slide(new(1, 4), direction));
    }

    [Fact]
    public void Slide_that_misses_the_teleporters_is_not_teleported()
    {
        Assert.Equal(new SlideResult(new(1, 0), false), TeleporterLane().Slide(Start, Direction.Down));
    }

    // 5 rows by 6 columns with the teleporter off the diagonal, so reading rows and columns swapped looks at other squares
    public static TheoryData<Cell, Direction, SlideResult> FromEverySide => new()
    {
        { new(0, 3), Direction.Down, new SlideResult(new(4, 0), false, Teleported: true) },
        { new(4, 3), Direction.Up, new SlideResult(new(0, 0), false, Teleported: true) },
        { new(2, 0), Direction.Right, new SlideResult(new(4, 5), true, Teleported: true) },
        { new(2, 5), Direction.Left, new SlideResult(new(4, 0), false, Teleported: true) },
    };

    [Theory]
    [MemberData(nameof(FromEverySide))]
    public void Slide_from_any_side_into_a_teleporter_on_a_non_square_map(Cell from, Direction direction, SlideResult expected)
    {
        var map = Map.Create(Grid("AAAAAA", "AAAAAA", "AAAAAA", "AAAAAA", "AAAAAZ"), Start, new(4, 5), teleporters: [Pair(2, 3, 4, 0)]);

        Assert.Equal(expected, map.Slide(from, direction));
    }

    [Fact]
    public void Round_trip_ends_on_its_start_teleported()
    {
        Assert.Equal(new SlideResult(new(0, 2), false, Teleported: true), RoundTrip().Slide(new(0, 2), Direction.Right));
    }

    [Theory]
    [InlineData(Direction.Up)]
    [InlineData(Direction.Down)]
    [InlineData(Direction.Left)]
    [InlineData(Direction.Right)]
    public void Map_given_no_pairs_slides_as_the_plain_map(Direction direction)
    {
        var plain = Map.Create(Grid("AAAAAA", "BBBBBB", "CCCCCZ"), Start, new(2, 5));

        Assert.Equal(plain.Slide(new(1, 2), direction), Lanes([]).Slide(new(1, 2), direction));
    }
}
