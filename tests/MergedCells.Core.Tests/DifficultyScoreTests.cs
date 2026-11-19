using AutoFixture;

namespace MergedCells.Core.Tests;

// Every cap is 10 unless a test says otherwise; par and retreat are never scored
public class DifficultyScoreTests
{
    private static readonly DifficultyCaps CapsOf10 = DifficultyConfigs.Caps(10, 10, 10, 10, 10);

    private static DifficultyMetrics Metrics(int away = 0, int? greedyGap = 0, int decoys = 0, long nearOptimal = 0, int? bruteForce = 0)
    {
        var fixture = new Fixture();
        return new DifficultyMetrics(fixture.Create<int>(), away, fixture.Create<int>(), greedyGap, decoys, nearOptimal, bruteForce);
    }

    private static int Score(
        DifficultyMetrics metrics,
        DifficultyWeights? weights = null,
        DifficultyCaps? caps = null,
        DifficultyMode mode = DifficultyMode.Campaign,
        IReadOnlyDictionary<string, DifficultyWeights>? modeWeights = null) =>
        new DifficultyRater(DifficultyConfigs.With(caps: caps ?? CapsOf10, weights: weights, modeWeights: modeWeights)).Score(metrics, mode);

    [Fact]
    public void No_tricks_score_0()
    {
        Assert.Equal(0, Score(Metrics()));
    }

    [Fact]
    public void Every_count_at_its_cap_scores_100()
    {
        Assert.Equal(100, Score(Metrics(10, 10, 10, 10, 10)));
    }

    [Fact]
    public void A_count_above_its_cap_counts_as_the_cap()
    {
        Assert.Equal(100, Score(Metrics(20, 20, 20, 20, 20)));
    }

    public static TheoryData<DifficultyMetrics> OneMissing => new() { Metrics(greedyGap: null), Metrics(bruteForce: null) };

    // No greedy route, or no lookahead player finishing, is as hard as that metric gets: a fifth of the score
    [Theory]
    [MemberData(nameof(OneMissing))]
    public void A_missing_route_or_finish_counts_as_the_full_share(DifficultyMetrics metrics)
    {
        Assert.Equal(20, Score(metrics));
    }

    [Fact]
    public void A_weight_of_0_leaves_its_metric_out()
    {
        Assert.Equal(0, Score(Metrics(decoys: 10), DifficultyConfigs.Weights(decoys: 0)));
    }

    // Two thirds of 100 is 66.7
    [Fact]
    public void The_score_rounds_down()
    {
        var awayOnly = DifficultyConfigs.Weights(100, 0, 0, 0, 0);

        Assert.Equal(66, Score(Metrics(away: 2), awayOnly, DifficultyConfigs.Caps(3, 10, 10, 10, 10)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(10_000)]
    public void Only_the_ratio_of_weights_matters(int weight)
    {
        Assert.Equal(10, Score(Metrics(away: 5), DifficultyConfigs.Weights(weight, weight, weight, weight, weight)));
    }

    public static TheoryData<DifficultyMetrics> OneNegative => new()
    {
        Metrics(away: -1), Metrics(greedyGap: -1), Metrics(decoys: -1), Metrics(nearOptimal: -1), Metrics(bruteForce: -1),
    };

    // Stored metrics read back from a file (chunk 22's playtest records) could hold anything
    [Theory]
    [MemberData(nameof(OneNegative))]
    public void A_negative_count_is_refused(DifficultyMetrics metrics)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Score(metrics));
    }

    [Fact]
    public void A_mode_that_isn_t_defined_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Score(Metrics(), mode: (DifficultyMode)(-1)));
    }

    private static readonly IReadOnlyDictionary<string, DifficultyWeights> DailyAwayOnly =
        new Dictionary<string, DifficultyWeights> { ["daily"] = DifficultyConfigs.Weights(100, 0, 0, 0, 0) };

    [Fact]
    public void A_mode_with_an_override_is_scored_with_it()
    {
        Assert.Equal(100, Score(Metrics(away: 10), mode: DifficultyMode.Daily, modeWeights: DailyAwayOnly));
    }

    [Fact]
    public void Custom_levels_have_a_mode_of_their_own()
    {
        var customAwayOnly = new Dictionary<string, DifficultyWeights> { ["custom"] = DifficultyConfigs.Weights(100, 0, 0, 0, 0) };

        Assert.Equal(100, Score(Metrics(away: 10), mode: DifficultyMode.Custom, modeWeights: customAwayOnly));
    }

    [Fact]
    public void A_mode_without_an_override_uses_the_shared_weights()
    {
        Assert.Equal(20, Score(Metrics(away: 10), mode: DifficultyMode.Campaign, modeWeights: DailyAwayOnly));
    }
}
