namespace MergedCells.Core.Tests;

// Every shipped level measured with the shipped difficulty numbers: the rater's par must be the stored par
public class ShippedDifficultyTests
{
    private static readonly DifficultyRater Rater = new(
        GameConfig.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "game_config.json"))).Difficulty);

    public static TheoryData<int> LevelIds => new(ShippedCampaignTests.Campaign().Levels.Select(level => level.Id));

    [Theory(Timeout = 10_000)]
    [MemberData(nameof(LevelIds))]
    public async Task Measured_par_is_the_stored_par(int id)
    {
        var level = ShippedCampaignTests.Campaign().Levels.Single(level => level.Id == id);

        var metrics = await Task.Run(() => Rater.Measure(level.Map), TestContext.Current.CancellationToken);

        Assert.Equal(level.Par, metrics!.Par);
    }
}
