using static MergedCells.Core.Tests.TestMaps;

namespace MergedCells.Core.Tests;

public class MapTraceTests
{
    // The top lane has a trap at [0, 3]; the end is below [0, 2]
    private static Map TopLane() => Map.Create(Grid("AAAAA", "BCDEF"), new(0, 0), new(1, 2), Traps(2, 5, (0, 3)));

    // From, direction, then the square the straight slide reached: a trap, a plain stop, the end, and a no-op
    public static TheoryData<int, int, Direction, int, int> Slides => new()
    {
        { 0, 0, Direction.Right, 0, 3 },
        { 0, 0, Direction.Down, 1, 0 },
        { 0, 2, Direction.Down, 1, 2 },
        { 0, 0, Direction.Up, 0, 0 },
    };

    [Theory]
    [MemberData(nameof(Slides))]
    public void Trace_reports_the_square_the_slide_reached(int row, int column, Direction direction, int reachedRow, int reachedColumn)
    {
        Assert.Equal(new Cell(reachedRow, reachedColumn), TopLane().Trace(new(row, column), direction).Reached);
    }

    public static TheoryData<int, int, Direction> Moves => new()
    {
        { 0, 0, Direction.Right },
        { 0, 0, Direction.Down },
        { 0, 2, Direction.Down },
        { 0, 0, Direction.Up },
    };

    [Theory]
    [MemberData(nameof(Moves))]
    public void Trace_result_matches_Slide(int row, int column, Direction direction)
    {
        var map = TopLane();

        Assert.Equal(map.Slide(new(row, column), direction), map.Trace(new(row, column), direction).Result);
    }

    // A sticky square at [0, 2] and a wall at [0, 4] in the top lane: PlayerSlide.Trapped compares Reached with the stop,
    // so a wall or sticky stop must report the stop, or it would play the trap's animation
    private static Map WallAndStickyLane()
    {
        var tiles = Sticky(2, 5, (0, 2));
        tiles[0, 4] = Tile.Wall;
        return Map.Create(Grid("AAAAA", "BCDEF"), new(0, 0), new(1, 2), tiles);
    }

    [Theory]
    [InlineData(0, 0, 0, 2)]
    [InlineData(0, 2, 0, 3)]
    public void Trace_of_a_sticky_or_wall_stop_reports_the_stop(int row, int column, int stopRow, int stopColumn)
    {
        var trace = WallAndStickyLane().Trace(new(row, column), Direction.Right);

        Assert.Equal((new Cell(stopRow, stopColumn), new Cell(stopRow, stopColumn)), (trace.Reached, trace.Result.Stop));
    }
}
