using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

// The climber builds one move graph per step and reads the solution and the metrics from it, so both must match what the
// public methods give for the map
public class SharedMoveGraphTests
{
    private static Map Twisted(ulong seed) => GeneratedMaps.Make(new MapRequest(12, 1, 100, Twists: GeneratedMaps.EveryTwist), seed);

    [Theory(Timeout = 10_000)]
    [AutoData]
    public async Task SolveGraph_matches_Solve_of_the_map(ulong seed)
    {
        var (fromGraph, fromMap) = await Task.Run(
            () =>
            {
                var map = Twisted(seed);
                return (Solver.SolveGraph(MoveGraph.Build(map.Start, map.Slide)), Solver.Solve(map));
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(
            (fromMap.Par, fromMap.ReachableStops, fromMap.DeadEnds, fromMap.OptimalRoutes, fromMap.LoopingSlides),
            (fromGraph.Par, fromGraph.ReachableStops, fromGraph.DeadEnds, fromGraph.OptimalRoutes, fromGraph.LoopingSlides));
        Assert.Equal(fromMap.Path, fromGraph.Path);
    }

    [Theory(Timeout = 10_000)]
    [AutoData]
    public async Task Measure_of_a_built_graph_matches_Measure_of_the_map(ulong seed)
    {
        var rater = new DifficultyRater(DifficultyConfigs.With());

        var (fromGraph, fromMap) = await Task.Run(
            () =>
            {
                var map = Twisted(seed);
                return (rater.Measure(MoveGraph.Build(map.Start, map.Slide), map.End), rater.Measure(map));
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(fromMap, fromGraph);
    }
}
