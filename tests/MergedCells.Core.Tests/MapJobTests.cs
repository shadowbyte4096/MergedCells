using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class MapJobTests
{
    private static readonly InfiniteMaps Maps =
        new(InfiniteConfigs.With(), GeneratorConfigs.With(), DifficultyConfigs.With(), ClimbConfigs.With());

    private static readonly InfiniteMap Made = new(
        TestMaps.Example(), 3, new DifficultyRater(DifficultyConfigs.With()).Measure(TestMaps.Example())!, 0, true);

    private static InfiniteChoice Plain(int difficulty, int side) => new(difficulty, side, false, false, false, false);

    [Theory(Timeout = 30_000)]
    [AutoData]
    public async Task Job_makes_the_map_next_makes(ulong seed)
    {
        var job = MapJob.Start(Maps, Plain(5, 8), seed);

        await job.Finished.WaitAsync(TestContext.Current.CancellationToken);

        var expected = Maps.Next(Plain(5, 8), seed, TestContext.Current.CancellationToken)!;
        Assert.Equal(LevelFingerprint.Of(expected.Map, expected.Par), LevelFingerprint.Of(job.Map!.Map, job.Map.Par));
    }

    // A plain 4×4 map can't reach difficulty 10's band, and a million steps take minutes, so only the job's token ends it soon
    private static MapJob LongClimb(ulong seed) => MapJob.Start(
        new InfiniteMaps(InfiniteConfigs.With(climbSteps: 1_000_000, stallSteps: 1000), GeneratorConfigs.With(), DifficultyConfigs.With(), ClimbConfigs.With()),
        Plain(10, 4),
        seed);

    [Theory(Timeout = 30_000)]
    [AutoData]
    public async Task Start_passes_the_step_counter_on(ulong seed)
    {
        var job = LongClimb(seed);

        var counted = SpinWait.SpinUntil(() => job.StepsTaken > 0, TimeSpan.FromSeconds(10));
        job.Cancel();
        await job.Finished.WaitAsync(TestContext.Current.CancellationToken);

        Assert.True(counted);
    }

    [Theory(Timeout = 30_000)]
    [AutoData]
    public async Task Start_passes_the_token_on(ulong seed)
    {
        var job = LongClimb(seed);

        job.Cancel();
        await job.Finished.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.True(job.IsCancelled);
    }

    [Fact(Timeout = 10_000)]
    public async Task Progress_is_steps_over_the_budget()
    {
        using var gate = new ManualResetEventSlim();
        var job = new MapJob((_, count) =>
        {
            count(30);
            gate.Wait();
            return null;
        }, 120);
        SpinWait.SpinUntil(() => job.StepsTaken == 30, TimeSpan.FromSeconds(5));

        var progress = job.Progress;
        gate.Set();
        await job.Finished.WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0.25, progress);
    }

    [Fact(Timeout = 10_000)]
    public async Task Progress_never_passes_one()
    {
        var job = new MapJob((_, count) =>
        {
            count(500);
            return null;
        }, 100);

        await job.Finished.WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1.0, job.Progress);
    }

    [Fact(Timeout = 10_000)]
    public async Task Not_done_while_the_work_runs()
    {
        using var gate = new ManualResetEventSlim();
        var job = new MapJob((_, _) =>
        {
            gate.Wait();
            return Made;
        }, 1);

        var doneWhileWorking = job.IsDone;
        gate.Set();
        await job.Finished.WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal((false, true), (doneWhileWorking, job.IsDone));
    }

    [Fact(Timeout = 10_000)]
    public async Task Cancel_stops_work_that_watches_its_token()
    {
        var job = new MapJob((token, _) =>
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                Thread.Sleep(1);
            }
        }, 1);

        job.Cancel();
        await job.Finished.WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal((true, true, (InfiniteMap?)null, (Exception?)null), (job.IsDone, job.IsCancelled, job.Map, job.Failure));
    }

    [Fact(Timeout = 10_000)]
    public async Task Cancel_after_the_work_finished_changes_nothing()
    {
        var job = new MapJob((_, _) => Made, 1);
        await job.Finished.WaitAsync(TestContext.Current.CancellationToken);

        job.Cancel();

        Assert.Equal((Made, false), (job.Map, job.IsCancelled));
    }

    [Theory(Timeout = 10_000)]
    [AutoData]
    public async Task Work_that_throws_is_kept_as_its_failure(string message)
    {
        var thrown = new InvalidOperationException(message);
        var job = new MapJob((_, _) => throw thrown, 1);

        await job.Finished.WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal((thrown, (InfiniteMap?)null, false), (job.Failure, job.Map, job.IsCancelled));
    }

    [Fact(Timeout = 10_000)]
    public async Task Work_with_no_map_finishes_empty()
    {
        var job = new MapJob((_, _) => null, 1);

        await job.Finished.WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(((InfiniteMap?)null, (Exception?)null, false), (job.Map, job.Failure, job.IsCancelled));
    }

    [Fact]
    public void Start_refuses_a_missing_choice()
    {
        Assert.Throws<ArgumentNullException>(() => MapJob.Start(Maps, null!, 1));
    }
}
