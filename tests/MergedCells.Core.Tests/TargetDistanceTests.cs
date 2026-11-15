using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class TargetDistanceTests
{
    // Every count 0, so only the metric a test sets can fall short
    private static readonly DifficultyMetrics Nothing = new(5, 0, 0, 0, 0, 0, 0);

    private static int Distance(DifficultyTarget target, DifficultyMetrics metrics, int score = 50, int? reliance = 0) =>
        TargetDistance.Of(target, metrics, score, reliance, DifficultyConfigs.Caps());

    [Theory]
    [AutoData]
    public void Zero_inside_the_target(DifficultyMetrics metrics, int? reliance)
    {
        Assert.Equal(0, Distance(new DifficultyTarget(20, 30), metrics, 25, reliance));
    }

    [Theory]
    [InlineData(10, 10)]
    [InlineData(45, 15)]
    public void Score_outside_the_range_costs_its_distance(int score, int distance)
    {
        Assert.Equal(distance, Distance(new DifficultyTarget(20, 30), Nothing, score));
    }

    [Fact]
    public void One_decoy_short_at_cap_8_costs_13()
    {
        Assert.Equal(13, Distance(new DifficultyTarget(0, 100, new MetricMinimums(Decoys: 5)), Nothing with { Decoys = 4 }));
    }

    [Fact]
    public void Null_greedy_gap_meets_its_minimum()
    {
        Assert.Equal(0, Distance(new DifficultyTarget(0, 100, new MetricMinimums(GreedyGap: 3)), Nothing with { GreedyGap = null }));
    }

    [Fact]
    public void Null_brute_force_meets_its_minimum()
    {
        var target = new DifficultyTarget(0, 100, new MetricMinimums(BruteForceExtraMoves: 3));

        Assert.Equal(0, Distance(target, Nothing with { BruteForceExtraMoves = null }));
    }

    [Fact]
    public void Each_move_of_missing_reliance_costs_100()
    {
        Assert.Equal(100, Distance(new DifficultyTarget(0, 100, MinTwistReliance: 2), Nothing, reliance: 1));
    }

    [Fact]
    public void Null_reliance_meets_the_minimum()
    {
        Assert.Equal(0, Distance(new DifficultyTarget(0, 100, MinTwistReliance: 2), Nothing, reliance: null));
    }

    [Fact]
    public void Huge_minimum_does_not_overflow()
    {
        var target = new DifficultyTarget(0, 100, new MetricMinimums(NearOptimalRoutes: 1_000_000_000_000_000_000));

        Assert.InRange(Distance(target, Nothing), 1, int.MaxValue);
    }

    public static TheoryData<DifficultyTarget> OutOfRangeTargets => new()
    {
        new DifficultyTarget(-1, 10),
        new DifficultyTarget(101, 101),
        new DifficultyTarget(30, 20),
        new DifficultyTarget(0, 101),
        new DifficultyTarget(0, 100, new MetricMinimums(AwayMoves: -1)),
        new DifficultyTarget(0, 100, new MetricMinimums(GreedyGap: -1)),
        new DifficultyTarget(0, 100, new MetricMinimums(Decoys: -1)),
        new DifficultyTarget(0, 100, new MetricMinimums(NearOptimalRoutes: -1)),
        new DifficultyTarget(0, 100, new MetricMinimums(BruteForceExtraMoves: -1)),
        new DifficultyTarget(0, 100, MinTwistReliance: -1),
    };

    [Theory]
    [MemberData(nameof(OutOfRangeTargets))]
    public void Check_rejects_a_target_out_of_range(DifficultyTarget target)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TargetDistance.Check(target));
    }

    public static TheoryData<DifficultyTarget> EdgeTargets => new()
    {
        new DifficultyTarget(0, 100, new MetricMinimums(0, 0, 0, 0, 0), 0),
        new DifficultyTarget(100, 100),
    };

    [Theory]
    [MemberData(nameof(EdgeTargets))]
    public void Check_accepts_every_range_s_edge(DifficultyTarget target)
    {
        Assert.Null(Record.Exception(() => TargetDistance.Check(target)));
    }
}
