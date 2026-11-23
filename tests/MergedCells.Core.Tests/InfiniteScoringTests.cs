namespace MergedCells.Core.Tests;

public class InfiniteScoringTests
{
    private static readonly InfiniteScoring Scoring = InfiniteConfigs.Scoring();

    [Theory]
    [InlineData(6, 8, 7500)]
    [InlineData(99, 100, 9900)]
    [InlineData(6, 6, 10000)]
    [InlineData(2, 3, 6666)]
    public void Efficiency_is_in_hundredths_rounded_down(int par, int moves, int expected)
    {
        Assert.Equal(expected, Scoring.Efficiency(par, moves));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 30)]
    [InlineData(10, 100)]
    [InlineData(25, 100)]
    public void Bonus_grows_per_step_and_stops_at_the_cap(int streakBefore, int expected)
    {
        Assert.Equal(expected, Scoring.BonusPercent(streakBefore));
    }

    [Fact]
    public void Huge_streak_does_not_overflow_the_bonus()
    {
        var scoring = InfiniteConfigs.Scoring(streakBonusPercent: 1000, streakBonusCapPercent: 1000);

        Assert.Equal(1000, scoring.BonusPercent(int.MaxValue));
    }

    // Par × 10 × efficiency × (1 + bonus), rounded down once: 6 × 10 × 6/8 × 1.3 = 58.5
    [Theory]
    [InlineData(6, 8, 3, 58)]
    [InlineData(6, 6, 0, 60)]
    [InlineData(2, 50, 0, 0)]
    [InlineData(6, 6, 12, 120)]
    public void Points_follow_the_formula(int par, int moves, int streakBefore, long expected)
    {
        Assert.Equal(expected, Scoring.Points(par, moves, streakBefore));
    }

    [Fact]
    public void Efficiency_below_par_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Scoring.Efficiency(6, 5));
    }

    [Fact]
    public void Points_below_par_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Scoring.Points(6, 5, 0));
    }

    [Fact]
    public void Par_below_one_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Scoring.Efficiency(0, 3));
    }

    // par² × 1000 × 1100 overflows a long long before the answer does
    [Fact]
    public void Points_for_the_largest_par_are_exact()
    {
        var scoring = InfiniteConfigs.Scoring(pointsPerPar: 1000, streakBonusPercent: 1000, streakBonusCapPercent: 1000);

        Assert.Equal(int.MaxValue * 11_000L, scoring.Points(int.MaxValue, int.MaxValue, 1));
    }

    [Fact]
    public void Default_test_scoring_passes_the_range_check()
    {
        Scoring.CheckRanges();
    }
}
