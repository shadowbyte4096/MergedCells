namespace MergedCells.Core.Tests;

// The exact thresholds are the point here, so these tests use literal values
public class StarsTests
{
    private static readonly StarsConfig Shipped = Config(2, 50, 4, 100);

    private static StarsConfig Config(int twoStarMin, int twoStarPercent, int oneStarMin, int oneStarPercent) => new()
    {
        TwoStarMinExtraMoves = twoStarMin,
        TwoStarExtraParPercent = twoStarPercent,
        OneStarMinExtraMoves = oneStarMin,
        OneStarExtraParPercent = oneStarPercent,
    };

    [Theory]
    [InlineData(1, 1, 3)]
    [InlineData(1, 2, 2)]
    [InlineData(1, 3, 2)]
    [InlineData(1, 4, 1)]
    [InlineData(1, 5, 1)]
    [InlineData(1, 6, 0)]
    [InlineData(2, 6, 1)]
    [InlineData(2, 7, 0)]
    [InlineData(4, 4, 3)]
    [InlineData(4, 5, 2)]
    [InlineData(4, 6, 2)]
    [InlineData(4, 7, 1)]
    [InlineData(4, 8, 1)]
    [InlineData(4, 9, 0)]
    [InlineData(5, 5, 3)]
    [InlineData(5, 8, 2)]
    [InlineData(5, 9, 1)]
    [InlineData(5, 10, 1)]
    [InlineData(5, 11, 0)]
    [InlineData(6, 7, 2)]
    [InlineData(6, 9, 2)]
    [InlineData(6, 10, 1)]
    [InlineData(7, 11, 2)]
    [InlineData(7, 12, 1)]
    [InlineData(7, 14, 1)]
    [InlineData(7, 15, 0)]
    public void Earned_follows_the_section_4_thresholds(int par, int moves, int expected)
    {
        Assert.Equal(expected, Stars.Earned(moves, par, Shipped));
    }

    [Theory]
    [InlineData(0, 100, 5, 10)]
    [InlineData(0, 0, 5, 5)]
    [InlineData(3, 0, 5, 8)]
    [InlineData(0, 34, 3, 5)]
    [InlineData(10, 50, 6, 16)]
    public void TwoStarLimit_uses_the_config_given(int minExtra, int percent, int par, int expected)
    {
        Assert.Equal(expected, Stars.TwoStarLimit(par, Config(minExtra, percent, 0, 0)));
    }

    [Theory]
    [InlineData(0, 100, 5, 10)]
    [InlineData(0, 0, 5, 5)]
    [InlineData(3, 0, 5, 8)]
    [InlineData(0, 34, 3, 5)]
    [InlineData(10, 50, 6, 16)]
    public void OneStarLimit_uses_the_config_given(int minExtra, int percent, int par, int expected)
    {
        Assert.Equal(expected, Stars.OneStarLimit(par, Config(0, 0, minExtra, percent)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TwoStarLimit_with_par_below_one_throws(int par)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Stars.TwoStarLimit(par, Shipped));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void OneStarLimit_with_par_below_one_throws(int par)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Stars.OneStarLimit(par, Shipped));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Earned_with_par_below_one_throws(int par)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Stars.Earned(1, par, Shipped));
    }

    [Fact]
    public void Moves_below_par_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Stars.Earned(4, 5, Shipped));
    }

    public static TheoryData<StarsConfig> ConfigsWithANegativeValue => new()
    {
        Config(-1, 50, 4, 100),
        Config(2, -1, 4, 100),
        Config(2, 50, -1, 100),
        Config(2, 50, 4, -1),
    };

    [Theory]
    [MemberData(nameof(ConfigsWithANegativeValue))]
    public void TwoStarLimit_with_a_negative_threshold_value_throws(StarsConfig config)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Stars.TwoStarLimit(5, config));
    }

    [Theory]
    [MemberData(nameof(ConfigsWithANegativeValue))]
    public void OneStarLimit_with_a_negative_threshold_value_throws(StarsConfig config)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Stars.OneStarLimit(5, config));
    }

    [Theory]
    [MemberData(nameof(ConfigsWithANegativeValue))]
    public void Earned_with_a_negative_threshold_value_throws(StarsConfig config)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Stars.Earned(5, 5, config));
    }

    [Fact]
    public void TwoStarLimit_with_no_config_throws()
    {
        Assert.Throws<ArgumentNullException>(() => Stars.TwoStarLimit(5, null!));
    }

    [Fact]
    public void OneStarLimit_with_no_config_throws()
    {
        Assert.Throws<ArgumentNullException>(() => Stars.OneStarLimit(5, null!));
    }

    [Fact]
    public void Earned_with_no_config_throws()
    {
        Assert.Throws<ArgumentNullException>(() => Stars.Earned(5, 5, null!));
    }
}
