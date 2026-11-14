using static MergedCells.Core.Tests.TestMaps;

namespace MergedCells.Core.Tests;

// Each map has a single optimal route, and a move that would count differently if it were measured by the twist it ran
// into rather than where it lands
public class DifficultyTwistTests
{
    private static DifficultyMetrics? Measure(Map map) => new DifficultyRater(DifficultyConfigs.With()).Measure(map);

    // On another thread, so a broken loop guard fails the test at its timeout instead of hanging the run
    private static Task<DifficultyMetrics?> MeasureAsync(Map map, CancellationToken token) => Task.Run(() => Measure(map), token);

    // The route is Right, Right, Down, Down, Left. From [0, 2], Down runs into the trap at [1, 2], 4 from the end and
    // nearer than the route's landing at [0, 4], 5 away; but it lands on the start, 7 away, so it's no decoy
    [Fact]
    public async Task A_trapped_move_is_measured_by_the_start()
    {
        var map = Map.Create(
            Grid("AABBB", "CCCCD", "EFFFD", "EGGGD", "EHHZD"), new(0, 0), new(4, 3), Traps(5, 5, (1, 2), (3, 1), (4, 2)));

        Assert.Equal(0, (await MeasureAsync(map, TestContext.Current.CancellationToken))!.Decoys);
    }

    // The route is Right, Down, Right. From [3, 2], Right enters the teleporter at [3, 3], 1 from the end like the route's
    // landing at [4, 2]; but it comes out of [2, 3] and stops on [2, 4], 3 away, so it's no decoy
    [Fact(Timeout = 10_000)]
    public async Task A_teleported_move_is_measured_where_it_stops()
    {
        var map = Map.Create(
            Grid("AABCD", "EEBCF", "GHBCI", "JHBCK", "JLLZK"),
            new(1, 0),
            new(4, 3),
            teleporters: [Pair(2, 3, 3, 3), Pair(3, 1, 1, 2)]);

        Assert.Equal(0, (await MeasureAsync(map, TestContext.Current.CancellationToken))!.Decoys);
    }

    // The route is Right, Down, Right. From [2, 4], 2 from the end, Down enters the teleporter at [3, 4], 1 away, and comes
    // out of [4, 1] at the map's edge, 3 away: it moves away from the end
    [Fact(Timeout = 10_000)]
    public async Task A_teleported_move_can_be_an_away_move()
    {
        var map = Map.Create(
            Grid("AAAAA", "BBBBB", "CCCCD", "EEEEE", "FFFFZ"),
            new(0, 1),
            new(4, 4),
            teleporters: [Pair(4, 1, 3, 4), Pair(2, 2, 0, 2)]);

        var metrics = (await MeasureAsync(map, TestContext.Current.CancellationToken))!;

        Assert.Equal((1, 1), (metrics.AwayMoves, metrics.Retreat));
    }

    // Right from the start comes back to it through the teleporters. Counted as a move, it would add near-optimal routes
    // such as Right, Down, Right; the real ones are Down, Left, Right and Down, Up, Down, Right
    [Fact(Timeout = 10_000)]
    public async Task A_round_trip_is_no_move()
    {
        Assert.Equal(2, (await MeasureAsync(RoundTrip(), TestContext.Current.CancellationToken))!.NearOptimalRoutes);
    }

    // Left and Right from the start loop; the routes one and two moves over par are Down, Left, Right and Down, Up, Down, Right
    [Fact(Timeout = 10_000)]
    public async Task Looping_slides_count_nowhere()
    {
        var metrics = (await MeasureAsync(LoopingLane(), TestContext.Current.CancellationToken))!;

        Assert.Equal((2, 0, 2L), (metrics.Par, metrics.Decoys, metrics.NearOptimalRoutes));
    }

    // [0, 0] and [0, 2] can only slide to each other, but Down from the start wins at once
    [Fact]
    public async Task A_map_with_dead_ends_is_measured()
    {
        var map = Map.Create(Grid("AAA", "BZC"), new(0, 1), new(1, 1), Walls(2, 3, (1, 0), (1, 2)));

        Assert.Equal(new DifficultyMetrics(1, 0, 0, 0, 0, 0, 0), await MeasureAsync(map, TestContext.Current.CancellationToken));
    }
}
