namespace MergedCells.Core.Tests;

// The shipped levels measured with the shipped difficulty numbers
public class ShippedDifficultyTests
{
    private static readonly DifficultyRater Rater = new(
        GameConfig.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "game_config.json"))).Difficulty);

    private static Level Level(int id) => ShippedCampaignTests.Campaign().Levels.Single(level => level.Id == id);

    public static TheoryData<int> LevelIds => new(ShippedCampaignTests.Campaign().Levels.Select(level => level.Id));

    // Chunk 12's "Greedy par" and "Brute" columns in levels-and-campaign.md, minus par. The values are the point: they pin
    // these definitions to the measures chapter 1 was built with
    [Theory]
    [InlineData(11, 0, 0)]
    [InlineData(12, 0, 0)]
    [InlineData(13, 0, 0)]
    [InlineData(1, 0, 0)]
    [InlineData(2, 0, 0)]
    [InlineData(18, 0, 3)]
    [InlineData(19, 0, 3)]
    [InlineData(20, 2, 2)]
    [InlineData(21, 2, 3)]
    [InlineData(7, 2, 2)]
    [InlineData(23, 2, 3)]
    [InlineData(24, 2, 3)]
    [InlineData(25, 2, 3)]
    [InlineData(22, 2, 3)]
    [InlineData(8, 1, 4)]
    [InlineData(9, 1, 3)]
    [InlineData(10, null, null)]
    public void Metrics_match_chunk_12_s_greedy_par_and_brute_columns(int id, int? greedyGap, int? bruteForce)
    {
        var metrics = Rater.Measure(Level(id).Map)!;

        Assert.Equal((greedyGap, bruteForce), (metrics.GreedyGap, metrics.BruteForceExtraMoves));
    }

    [Theory(Timeout = 10_000)]
    [MemberData(nameof(LevelIds))]
    public async Task Measured_par_is_the_stored_par(int id)
    {
        var level = Level(id);

        var metrics = await Task.Run(() => Rater.Measure(level.Map), TestContext.Current.CancellationToken);

        Assert.Equal(level.Par, metrics!.Par);
    }

    public static TheoryData<DifficultyMode> Modes => new(Enum.GetValues<DifficultyMode>());

    // The bands in levels-and-campaign.md's table: tutorials and easy, then medium and hard
    [Theory]
    [MemberData(nameof(Modes))]
    public void Chapter_1_s_medium_and_hard_levels_outscore_its_tutorials_and_easy_levels(DifficultyMode mode)
    {
        int Score(int id) => Rater.Score(Rater.Measure(Level(id).Map)!, mode);

        var easiestHard = new[] { 18, 19, 20, 21, 7, 23, 24, 25, 22, 8 }.Min(Score);
        var hardestEasy = new[] { 11, 12, 13, 1, 2 }.Max(Score);

        Assert.True(easiestHard > hardestEasy, $"A medium or hard level scores {easiestHard}, an easy level {hardestEasy}");
    }
}
