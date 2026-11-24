namespace MergedCells.Core.Tests;

public class CoinsConfigTests
{
    private static CoinsConfig Config(int perStar = 10, int daily = 20, int perPar = 1, int percentPerScore = 5) =>
        new() { PerStar = perStar, Daily = daily, PerPar = perPar, PercentPerScore = percentPerScore };

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 10)]
    [InlineData(2, 20)]
    [InlineData(3, 30)]
    public void For_stars_pays_per_new_star(int newStars, long expected)
    {
        Assert.Equal(expected, Config().ForStars(newStars));
    }

    [Fact]
    public void For_stars_throws_for_a_negative_count()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Config().ForStars(-1));
    }

    [Theory]
    [InlineData(6, 8, 63, 14)] // the spec's worked example
    [InlineData(6, 6, 63, 18)]
    [InlineData(2, 2, 5, 0)] // a tiny easy map rounds down to nothing
    [InlineData(6, 6, 0, 0)]
    [InlineData(10, 10, 100, 50)]
    public void For_map_pays_par_times_efficiency_times_difficulty(int par, int moves, int score, long expected)
    {
        Assert.Equal(expected, Config().ForMap(par, moves, score));
    }

    [Fact]
    public void For_map_does_not_overflow_at_a_huge_par()
    {
        Assert.Equal(1_000_000L * 1000 * 1000, Config(perPar: 1000, percentPerScore: 1000).ForMap(1_000_000, 1_000_000, 100));
    }

    [Theory]
    [InlineData(0, 1, 50)]
    [InlineData(6, 5, 50)]
    [InlineData(6, 6, -1)]
    [InlineData(6, 6, 101)]
    public void For_map_throws_out_of_range(int par, int moves, int score)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Config().ForMap(par, moves, score));
    }

    public static TheoryData<string, int> OutOfRangeValues()
    {
        var data = new TheoryData<string, int>();
        foreach (var path in new[] { "coins.perStar", "coins.daily", "coins.perPar", "coins.percentPerScore" })
        {
            data.Add(path, -1);
            data.Add(path, 1001);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(OutOfRangeValues))]
    public void Check_ranges_names_a_value_out_of_range(string path, int value)
    {
        var config = Config(
            perStar: path == "coins.perStar" ? value : 0,
            daily: path == "coins.daily" ? value : 0,
            perPar: path == "coins.perPar" ? value : 0,
            percentPerScore: path == "coins.percentPerScore" ? value : 0);

        Assert.Equal(path, Assert.Throws<ArgumentOutOfRangeException>(config.CheckRanges).ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    public void Check_ranges_accepts_the_ends_of_the_range(int value)
    {
        var config = Config(perStar: value, daily: value, perPar: value, percentPerScore: value);

        config.CheckRanges();
    }
}
