namespace MergedCells.Core.Tests;

// The campaign's data files, linked into the test output as the level files are
public class ShippedCampaignTests
{
    private static string Shipped(params string[] path) => Path.Combine([AppContext.BaseDirectory, .. path]);

    internal static Campaign Campaign() =>
        MergedCells.Core.Campaign.Parse(
            File.ReadAllText(Shipped("campaign", "chapters.json")),
            Directory.GetFiles(Shipped("levels"), "*.json").Select(path => LevelFile.Parse(File.ReadAllText(path))).ToArray());

    [Fact]
    public void Chapter_file_parses_against_the_shipped_levels()
    {
        Assert.NotEmpty(Campaign().Chapters);
    }

    // The credits show dotnet-<major>.txt for the runtime a build ships, which is its target framework's
    public static TheoryData<int> ShippedDotnetMajors => new(
        System.Text.RegularExpressions.Regex.Matches(
                File.ReadAllText(Shipped("MergedCells.csproj")), @"<TargetFramework[^>]*>net(\d+)\.0</TargetFramework>")
            .Select(match => int.Parse(match.Groups[1].Value)));

    [Theory]
    [MemberData(nameof(ShippedDotnetMajors))]
    public void Every_target_framework_has_its_dotnet_notice(int major)
    {
        Assert.True(File.Exists(Shipped("credits", "licences", $"dotnet-{major}.txt")), $"credits/licences/dotnet-{major}.txt is missing");
    }

    // The other notices the credits screen reads by path; a missing one only shows as a line on the licences page
    [Theory]
    [InlineData("credits", "licences", "merged-cells.txt")]
    [InlineData("credits", "licences", "android.txt")]
    [InlineData("themes", "fonts", "OFL.txt")]
    public void Every_notice_the_credits_read_is_there(params string[] path)
    {
        Assert.True(File.Exists(Shipped(path)), $"{string.Join('/', path)} is missing");
    }

    [Fact]
    public void Credits_file_parses_and_credits_the_developer_and_Jake()
    {
        var credits = Credits.Parse(File.ReadAllText(Shipped("credits", "credits.json")));

        Assert.Equal(
            ["Made by: DefinitelyNotZac", "Original idea: Jake"],
            credits.Select(entry => $"{entry.Role}: {string.Join(", ", entry.Names)}"));
    }

    // The other half of Main's start-up check (GameConfigTests covers CheckRanges): it throws on a gate percentage out of range
    [Fact]
    public void Shipped_campaign_and_config_give_progress_on_a_fresh_save()
    {
        var config = GameConfig.Parse(File.ReadAllText(Shipped("game_config.json")));

        Assert.Null(Record.Exception(() => CampaignProgress.Of(Campaign(), new SaveGame(), config)));
    }

    // Chapter 2 opens at 30 stars, chapter 3 at 34, chapter 4 at 38 and chapter 5 at 42 ("Stars and gates" in levels-and-campaign.md). Adding a level to an
    // early chapter or retuning the percentage moves them, and players part-way through would find a chapter shut again
    [Fact]
    public void Shipped_chapter_gates_are_pinned()
    {
        var campaign = Campaign();
        var progress = CampaignProgress.Of(campaign, new SaveGame(), GameConfig.Parse(File.ReadAllText(Shipped("game_config.json"))));

        Assert.Equal([0, 30, 34, 38, 42], campaign.Chapters.Select(progress.Gate));
    }

    [Fact]
    public void Every_chapterStars_key_is_a_shipped_chapter()
    {
        var config = GameConfig.Parse(File.ReadAllText(Shipped("game_config.json")));

        Assert.All(config.Campaign.ChapterStars.Keys, chapter => Assert.InRange(chapter, 1, Campaign().Chapters.Count));
    }

    [Fact]
    public void Every_chapterGatePercent_key_is_a_shipped_chapter()
    {
        var config = GameConfig.Parse(File.ReadAllText(Shipped("game_config.json")));

        Assert.All(config.Campaign.ChapterGatePercent.Keys, chapter => Assert.InRange(chapter, 1, Campaign().Chapters.Count));
    }

    [Fact]
    public void Every_levelStars_key_is_a_shipped_level()
    {
        var config = GameConfig.Parse(File.ReadAllText(Shipped("game_config.json")));
        var ids = Campaign().Levels.Select(level => level.Id).ToHashSet();

        Assert.All(config.Campaign.LevelStars.Keys, id => Assert.Contains(id, ids));
    }
}
