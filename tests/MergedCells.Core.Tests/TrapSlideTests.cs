using static MergedCells.Core.Tests.TestMaps;

namespace MergedCells.Core.Tests;

public class TrapSlideTests
{
    private static readonly Cell Start = new(0, 0);

    private static Map TopLane(Tile[,] tiles) => Map.Create(Grid("AAAAA", "BCDEF"), Start, new(1, 2), tiles);

    private static Map TopLane(params (int Row, int Column)[] traps) => TopLane(Traps(2, 5, traps));

    [Fact]
    public void Slide_over_a_trap_returns_the_start_trapped()
    {
        Assert.Equal(new SlideResult(Start, false, true), TopLane((0, 2)).Slide(new(0, 4), Direction.Left));
    }

    [Fact]
    public void Trap_on_the_first_square_of_a_new_region_catches()
    {
        var map = Map.Create(Grid("AAB", "CCD"), Start, new(1, 2), Traps(2, 3, (0, 2)));

        Assert.Equal(new SlideResult(Start, false, true), map.Slide(Start, Direction.Right));
    }

    [Fact]
    public void Trap_beside_the_player_catches()
    {
        Assert.Equal(new SlideResult(Start, false, true), TopLane((0, 3)).Slide(new(0, 2), Direction.Right));
    }

    [Fact]
    public void Trap_beside_the_start_catches_a_slide_from_the_start()
    {
        Assert.Equal(new SlideResult(Start, false, true), TrapBesideTheStart().Slide(Start, Direction.Right));
    }

    [Fact]
    public void Wall_before_a_trap_stops_the_slide_first()
    {
        var tiles = Traps(2, 5, (0, 3));
        tiles[0, 2] = Tile.Wall;

        Assert.Equal(new SlideResult(new(0, 1), false), TopLane(tiles).Slide(Start, Direction.Right));
    }

    [Fact]
    public void Sticky_square_before_a_trap_stops_the_slide_first()
    {
        var tiles = Traps(2, 5, (0, 3));
        tiles[0, 2] = Tile.Sticky;

        Assert.Equal(new SlideResult(new(0, 2), false), TopLane(tiles).Slide(Start, Direction.Right));
    }

    [Fact]
    public void End_before_a_trap_wins()
    {
        var map = Map.Create(Grid("AAZBB"), Start, new(0, 2), Traps(1, 5, (0, 3)));

        Assert.Equal(new SlideResult(new(0, 2), true), map.Slide(Start, Direction.Right));
    }

    [Fact]
    public void Slide_that_misses_the_traps_is_not_trapped()
    {
        Assert.Equal(new SlideResult(new(1, 0), false), TopLane((0, 3)).Slide(Start, Direction.Down));
    }

    // 5 rows by 6 columns with the trap off row 0, so reading the tiles with rows and columns swapped looks at another square
    [Theory]
    [InlineData(0, 3, Direction.Down)]
    [InlineData(4, 3, Direction.Up)]
    [InlineData(2, 0, Direction.Right)]
    [InlineData(2, 5, Direction.Left)]
    public void Slide_from_any_side_into_a_trap_returns_the_start_on_a_non_square_map(int row, int column, Direction direction)
    {
        var map = Map.Create(Grid("AAAAAA", "AAAAAA", "AAAAAA", "AAAAAA", "AAAAAZ"), Start, new(4, 5), Traps(5, 6, (2, 3)));

        Assert.Equal(new SlideResult(Start, false, true), map.Slide(new(row, column), direction));
    }
}
