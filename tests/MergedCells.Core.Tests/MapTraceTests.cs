using static MergedCells.Core.Tests.TestMaps;

namespace MergedCells.Core.Tests;

public class MapTraceTests
{
    // The top lane has a trap at [0, 3]; the end is below [0, 2]
    private static Map TopLane() => Map.Create(Grid("AAAAA", "BCDEF"), new(0, 0), new(1, 2), Traps(2, 5, (0, 3)));

    // Three lanes, with the end at the bottom right
    private static Map Lanes(TeleporterPair[] pairs, Tile[,]? tiles = null) =>
        Map.Create(Grid("AAAAAA", "BBBBBB", "CCCCCZ"), new(0, 0), new(2, 5), tiles, pairs);

    private static SlideRun Run(int fromRow, int fromColumn, int toRow, int toColumn) => new(new(fromRow, fromColumn), new(toRow, toColumn));

    // From, direction, then the one straight run: into a trap, to a plain stop, to the end, and a no-op
    public static TheoryData<int, int, Direction, int, int> Slides => new()
    {
        { 0, 0, Direction.Right, 0, 3 },
        { 0, 0, Direction.Down, 1, 0 },
        { 0, 2, Direction.Down, 1, 2 },
        { 0, 0, Direction.Up, 0, 0 },
    };

    [Theory]
    [MemberData(nameof(Slides))]
    public void Straight_slide_is_one_run_to_the_square_it_reached(int row, int column, Direction direction, int reachedRow, int reachedColumn)
    {
        Assert.Equal([Run(row, column, reachedRow, reachedColumn)], TopLane().Trace(new(row, column), direction).Runs);
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

    // A sticky square at [0, 2] and a wall at [0, 4] in the top lane: PlayerSlide.Trapped compares the last run's end with
    // the stop, so a wall or sticky stop must end its run on the stop, or it would play the trap's animation
    private static Map WallAndStickyLane()
    {
        var tiles = Sticky(2, 5, (0, 2));
        tiles[0, 4] = Tile.Wall;
        return Map.Create(Grid("AAAAA", "BCDEF"), new(0, 0), new(1, 2), tiles);
    }

    [Theory]
    [InlineData(0, 0, 0, 2)]
    [InlineData(0, 2, 0, 3)]
    public void Trace_of_a_sticky_or_wall_stop_ends_on_the_stop(int row, int column, int stopRow, int stopColumn)
    {
        var trace = WallAndStickyLane().Trace(new(row, column), Direction.Right);

        Assert.Equal((new Cell(stopRow, stopColumn), new Cell(stopRow, stopColumn)), (trace.Runs[^1].To, trace.Result.Stop));
    }

    [Fact]
    public void One_jump_gives_two_runs()
    {
        Assert.Equal([Run(0, 0, 0, 2), Run(1, 4, 1, 5)], TeleporterLane().Trace(new(0, 0), Direction.Right).Runs);
    }

    [Fact]
    public void Exit_next_to_the_edge_ends_with_a_run_on_the_exit()
    {
        Assert.Equal([Run(0, 0, 0, 2), Run(1, 5, 1, 5)], Lanes([Pair(0, 2, 1, 5)]).Trace(new(0, 0), Direction.Right).Runs);
    }

    [Fact]
    public void Trap_after_the_exit_ends_the_last_run_on_the_trap()
    {
        var tiles = Traps(3, 6, (1, 4));

        Assert.Equal([Run(0, 0, 0, 2), Run(1, 2, 1, 4)], Lanes([Pair(0, 2, 1, 2)], tiles).Trace(new(0, 0), Direction.Right).Runs);
    }

    [Fact]
    public void Chain_gives_a_run_per_jump()
    {
        var map = Lanes([Pair(0, 2, 1, 0), Pair(1, 3, 0, 4)]);

        Assert.Equal([Run(0, 0, 0, 2), Run(1, 0, 1, 3), Run(0, 4, 0, 5)], map.Trace(new(0, 0), Direction.Right).Runs);
    }

    [Fact]
    public void Round_trip_ends_its_last_run_where_it_began()
    {
        Assert.Equal([Run(0, 2, 0, 4), Run(0, 1, 0, 2)], RoundTrip().Trace(new(0, 2), Direction.Right).Runs);
    }

    [Fact(Timeout = 10_000)]
    public async Task Loop_traces_as_a_no_op()
    {
        var trace = await Task.Run(() => LoopingLane().Trace(new(0, 2), Direction.Right), TestContext.Current.CancellationToken);

        Assert.Equal([Run(0, 2, 0, 2)], trace.Runs);
    }

    // Right from each map's start: one jump, an exit next to the edge, a trap after the exit, a chain, a round trip and a loop
    public static TheoryData<string> TeleporterMaps =>
        new() { nameof(TeleporterLane), "ExitAtTheEdge", "TrapAfterTheExit", "Chain", nameof(RoundTrip), nameof(LoopingLane) };

    private static Map TeleporterMap(string name) => name switch
    {
        nameof(TeleporterLane) => TeleporterLane(),
        "ExitAtTheEdge" => Lanes([Pair(0, 2, 1, 5)]),
        "TrapAfterTheExit" => Lanes([Pair(0, 2, 1, 2)], Traps(3, 6, (1, 4))),
        "Chain" => Lanes([Pair(0, 2, 1, 0), Pair(1, 3, 0, 4)]),
        nameof(RoundTrip) => RoundTrip(),
        _ => LoopingLane(),
    };

    [Theory(Timeout = 10_000)]
    [MemberData(nameof(TeleporterMaps))]
    public async Task Trace_result_matches_Slide_through_teleporters(string name)
    {
        var map = TeleporterMap(name);

        var trace = await Task.Run(() => map.Trace(map.Start, Direction.Right), TestContext.Current.CancellationToken);

        Assert.Equal(map.Slide(map.Start, Direction.Right), trace.Result);
    }
}
