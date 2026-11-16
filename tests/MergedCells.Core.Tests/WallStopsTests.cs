using static MergedCells.Core.Tests.TestMaps;

namespace MergedCells.Core.Tests;

public class WallStopsTests
{
    [Fact]
    public void Stop_before_a_wall_counts()
    {
        var map = Map.Create(Grid("AAAAA", "BCDEF"), new(0, 0), new(1, 2), Walls(2, 5, (0, 3)));

        Assert.Equal(1, WallStops.Count(map, [Direction.Right, Direction.Down]));
    }

    // The slide enters B at [0, 2], a boundary stop, and only then faces the wall
    [Fact]
    public void Boundary_stop_facing_a_wall_does_not_count()
    {
        var map = Map.Create(Grid("AABBB", "CDEFG"), new(0, 0), new(1, 4), Walls(2, 5, (0, 3)));

        Assert.Equal(0, WallStops.Count(map, [Direction.Right]));
    }

    [Fact]
    public void Winning_slide_does_not_count()
    {
        var map = Map.Create(Grid("AAABC", "DDDDD"), new(0, 0), new(0, 3), Walls(2, 5, (0, 4)));

        Assert.Equal(0, WallStops.Count(map, [Direction.Right]));
    }

    [Fact]
    public void Stop_at_the_edge_does_not_count()
    {
        var map = Map.Create(Grid("AAAAA", "BBBBC"), new(0, 0), new(1, 4));

        Assert.Equal(0, WallStops.Count(map, [Direction.Right]));
    }

    // Up from [1, 4] falls into the trap at [0, 4] and lands on the start [1, 0], which has a wall above it: the trap
    // stopped the slide, not the wall
    [Fact]
    public void Trapped_slide_does_not_count()
    {
        var tiles = Walls(4, 5, (0, 0));
        tiles[0, 4] = Tile.Trap;
        var map = Map.Create(Grid("AAAAA", "AAAAA", "AAAAA", "BCDEF"), new(1, 0), new(3, 4), tiles);

        Assert.Equal(0, WallStops.Count(map, [Direction.Right, Direction.Up]));
    }
}
