using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class InfiniteConfigTests
{
    private static readonly SideRange Whole = InfiniteConfigs.Range(4, 30);

    [Fact]
    public void Default_test_config_passes_the_range_check()
    {
        InfiniteConfigs.With().CheckRanges();
    }

    public static TheoryData<string, InfiniteConfig> OutOfRange => new()
    {
        { "infinite.minSide", InfiniteConfigs.With(minSide: 1, difficulties: InfiniteConfigs.Tenths(1, 30)) },
        { "infinite.maxSide", InfiniteConfigs.With(minSide: 4, maxSide: 3, startSide: 4, difficulties: InfiniteConfigs.Tenths(4, 4)) },
        { "infinite.maxSide", InfiniteConfigs.With(maxSide: 1001, difficulties: InfiniteConfigs.Tenths(4, 1001)) },
        { "infinite.difficulties", InfiniteConfigs.With(difficulties: InfiniteConfigs.Tenths().Take(9).ToArray()) },
        { "infinite.difficulties", InfiniteConfigs.With(difficulties: [.. InfiniteConfigs.Tenths(), InfiniteConfigs.Tenths()[0]]) },
        { "infinite.difficulties[3].minScore", InfiniteConfigs.With(difficulties: InfiniteConfigs.TenthsWith(4, InfiniteConfigs.Difficulty(-1, 9, Whole, Whole))) },
        { "infinite.difficulties[3].minScore", InfiniteConfigs.With(difficulties: InfiniteConfigs.TenthsWith(4, InfiniteConfigs.Difficulty(101, 101, Whole, Whole))) },
        { "infinite.difficulties[3].maxScore", InfiniteConfigs.With(difficulties: InfiniteConfigs.TenthsWith(4, InfiniteConfigs.Difficulty(30, 29, Whole, Whole))) },
        { "infinite.difficulties[3].maxScore", InfiniteConfigs.With(difficulties: InfiniteConfigs.TenthsWith(4, InfiniteConfigs.Difficulty(30, 101, Whole, Whole))) },
        { "infinite.difficulties[3].plain.minSide", InfiniteConfigs.With(difficulties: InfiniteConfigs.TenthsWith(4, InfiniteConfigs.Difficulty(30, 39, InfiniteConfigs.Range(3, 30), Whole))) },
        { "infinite.difficulties[3].twists.maxSide", InfiniteConfigs.With(difficulties: InfiniteConfigs.TenthsWith(4, InfiniteConfigs.Difficulty(30, 39, Whole, InfiniteConfigs.Range(4, 31)))) },
        { "infinite.difficulties[3].plain.maxSide", InfiniteConfigs.With(difficulties: InfiniteConfigs.TenthsWith(4, InfiniteConfigs.Difficulty(30, 39, InfiniteConfigs.Range(12, 11), Whole))) },
        { "infinite.startDifficulty", InfiniteConfigs.With(startDifficulty: 0) },
        { "infinite.startDifficulty", InfiniteConfigs.With(startDifficulty: 11) },
        { "infinite.startSide", InfiniteConfigs.With(difficulties: InfiniteConfigs.TenthsWith(3, InfiniteConfigs.Difficulty(20, 29, InfiniteConfigs.Range(12, 30), Whole))) },
        { "infinite.minPar", InfiniteConfigs.With(minPar: 0) },
        { "infinite.maxPar", InfiniteConfigs.With(minPar: 5, maxPar: 4) },
        { "infinite.climbSteps", InfiniteConfigs.With(climbSteps: 0, stallSteps: 0) },
        { "infinite.climbSteps", InfiniteConfigs.With(climbSteps: 10_000_001) },
        { "infinite.stallSteps", InfiniteConfigs.With(stallSteps: 0) },
        { "infinite.stallSteps", InfiniteConfigs.With(climbSteps: 100, stallSteps: 101) },
        { "infinite.wallPercent", InfiniteConfigs.With(wallPercent: -1) },
        { "infinite.wallPercent", InfiniteConfigs.With(wallPercent: 101) },
        { "infinite.trapPercent", InfiniteConfigs.With(trapPercent: -1) },
        { "infinite.trapPercent", InfiniteConfigs.With(trapPercent: 101) },
        { "infinite.teleporterPercent", InfiniteConfigs.With(teleporterPercent: -1) },
        { "infinite.teleporterPercent", InfiniteConfigs.With(teleporterPercent: 101) },
    };

    [Theory]
    [MemberData(nameof(OutOfRange))]
    public void Out_of_range_value_is_named(string name, InfiniteConfig config)
    {
        Assert.Equal(name, Assert.Throws<ArgumentOutOfRangeException>(config.CheckRanges).ParamName);
    }

    [Fact]
    public void Difficulty_is_one_based()
    {
        var config = InfiniteConfigs.With();

        Assert.Same(config.Difficulties[0], config.Difficulty(1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void Difficulty_outside_one_to_ten_throws(int difficulty)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => InfiniteConfigs.With().Difficulty(difficulty));
    }

    [Theory]
    [AutoData]
    public void Climb_takes_infinite_steps_and_shared_weights(int climbSteps, int stallSteps, ClimbConfig shared)
    {
        var climb = InfiniteConfigs.With(climbSteps: climbSteps, stallSteps: stallSteps).Climb(shared);

        Assert.Equal(shared with { MaxSteps = climbSteps, StallSteps = stallSteps }, climb);
    }
}
