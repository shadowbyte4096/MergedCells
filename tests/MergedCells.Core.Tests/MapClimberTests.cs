using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class MapClimberTests
{
    // The hard brute-force bar on a plain 8x8, which random maps almost never meet
    private static readonly ClimbRequest HardBar =
        new(new MapRequest(8, 4, 12), new DifficultyTarget(40, 100, new MetricMinimums(AwayMoves: 1, BruteForceExtraMoves: 3)));

    private static readonly ClimbRequest Middling =
        new(new MapRequest(10, 1, 100, Bends: true, Twists: GeneratedMaps.EveryTwist), new DifficultyTarget(30, 60));

    private static MapClimber Climber(ClimbConfig? climb = null) =>
        new(GeneratorConfigs.With(), DifficultyConfigs.With(), climb ?? ClimbConfigs.With());

    private static Task<ClimbedMap?> ClimbAsync(MapClimber climber, ClimbRequest request, ulong seed, CancellationToken token) =>
        Task.Run(() => climber.Climb(request, seed), token);

    [Theory(Timeout = 30_000)]
    [AutoData]
    public async Task One_seed_gives_one_map(ulong seed)
    {
        var climber = Climber(ClimbConfigs.With(maxSteps: 300, stallSteps: 100));

        var first = (await ClimbAsync(climber, HardBar, seed, TestContext.Current.CancellationToken))!;
        var second = (await ClimbAsync(climber, HardBar, seed, TestContext.Current.CancellationToken))!;

        Assert.Equal((LevelFingerprint.Of(first.Map, first.Solution.Par!.Value), first.Distance), (LevelFingerprint.Of(second.Map, second.Solution.Par!.Value), second.Distance));
    }

    [Fact(Timeout = 60_000)]
    public async Task Climbs_up_to_the_hard_bar()
    {
        var climbed = await ClimbAsync(Climber(ClimbConfigs.With(maxSteps: 20_000)), HardBar, 1, TestContext.Current.CancellationToken);

        Assert.Equal(0, climbed!.Distance);
    }

    [Fact(Timeout = 60_000)]
    public async Task Climbs_down_to_an_easy_score()
    {
        var request = new ClimbRequest(new MapRequest(30, 1, 200, Bends: true, Twists: GeneratedMaps.EveryTwist), new DifficultyTarget(0, 10));

        var climbed = await ClimbAsync(Climber(), request, 1, TestContext.Current.CancellationToken);

        Assert.Equal(0, climbed!.Distance);
    }

    [Theory(Timeout = 30_000)]
    [AutoData]
    public async Task Result_passes_the_generator_s_filter(ulong seed)
    {
        var climbed = (await ClimbAsync(Climber(ClimbConfigs.With(maxSteps: 500)), Middling, seed, TestContext.Current.CancellationToken))!;

        Assert.True(MapGenerator.Keeps(Solver.Solve(climbed.Map), Middling.Map));
    }

    [Theory(Timeout = 30_000)]
    [AutoData]
    public async Task Result_fits_the_generator_s_shapes(ulong seed)
    {
        var climbed = (await ClimbAsync(Climber(ClimbConfigs.With(maxSteps: 500)), Middling, seed, TestContext.Current.CancellationToken))!;

        Assert.True(MapShapes.Fits(climbed.Map.RegionGrid(), climbed.Map.End, GeneratorConfigs.With(), Middling.Map.Bends));
    }

    [Theory(Timeout = 30_000)]
    [AutoData]
    public async Task Unreachable_target_returns_the_nearest_map(ulong seed)
    {
        var request = new ClimbRequest(new MapRequest(5, 2, 3), new DifficultyTarget(100, 100));
        var rater = new DifficultyRater(DifficultyConfigs.With());

        var climbed = (await ClimbAsync(Climber(ClimbConfigs.With(maxSteps: 200, stallSteps: 50)), request, seed, TestContext.Current.CancellationToken))!;

        var metrics = rater.Measure(climbed.Map)!;
        var distance = TargetDistance.Of(
            request.Target, metrics, rater.Score(metrics, DifficultyMode.Campaign), TwistReliance.Of(climbed.Map, rater), DifficultyConfigs.Caps());
        Assert.True(climbed.Distance > 0);
        Assert.Equal(distance, climbed.Distance);
    }

    [Theory(Timeout = 30_000)]
    [AutoData]
    public async Task Null_when_the_generator_never_gives_a_starting_map(ulong seed)
    {
        var request = new ClimbRequest(new MapRequest(5, 1000, 1000), new DifficultyTarget(0, 100));

        Assert.Null(await ClimbAsync(Climber(ClimbConfigs.With(maxSteps: 10, stallSteps: 5)), request, seed, TestContext.Current.CancellationToken));
    }

    [Theory]
    [AutoData]
    public void Twist_moves_alone_on_a_plain_request_throw(ulong seed)
    {
        var climber = Climber(ClimbConfigs.With(splitWeight: 0, mergeWeight: 0, moveSquareWeight: 0, moveStartWeight: 0, moveTwistWeight: 1));

        Assert.Throws<ArgumentException>(() => climber.Climb(HardBar, seed));
    }

    [Theory]
    [AutoData]
    public void Out_of_range_target_throws(ulong seed)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Climber().Climb(HardBar with { Target = new DifficultyTarget(101, 101) }, seed));
    }

    [Theory]
    [AutoData]
    public void Undefined_mode_throws(ulong seed)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Climber().Climb(HardBar with { Mode = (DifficultyMode)99 }, seed));
    }

    public static TheoryData<ClimbConfig> OutOfRangeConfigs => new()
    {
        ClimbConfigs.With(maxSteps: 0),
        ClimbConfigs.With(maxSteps: 10_000_001),
        ClimbConfigs.With(stallSteps: 0),
        ClimbConfigs.With(maxSteps: 1, stallSteps: 2),
        ClimbConfigs.With(splitWeight: -1),
        ClimbConfigs.With(mergeWeight: -1),
        ClimbConfigs.With(moveSquareWeight: -1),
        ClimbConfigs.With(moveStartWeight: -1),
        ClimbConfigs.With(moveTwistWeight: -1),
        ClimbConfigs.With(splitWeight: 0, mergeWeight: 0, moveSquareWeight: 0, moveStartWeight: 0, moveTwistWeight: 0),
        ClimbConfigs.With(splitWeight: int.MaxValue),
    };

    [Theory]
    [MemberData(nameof(OutOfRangeConfigs))]
    public void Rejects_a_climb_number_out_of_range(ClimbConfig climb)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Climber(climb));
    }

    public static TheoryData<ClimbConfig> EdgeConfigs => new()
    {
        ClimbConfigs.With(maxSteps: 1, stallSteps: 1),
        ClimbConfigs.With(maxSteps: 10_000_000, stallSteps: 1),
    };

    [Theory]
    [MemberData(nameof(EdgeConfigs))]
    public void Accepts_every_range_s_edge(ClimbConfig climb)
    {
        Assert.Null(Record.Exception(() => Climber(climb)));
    }
}
