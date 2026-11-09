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

    [Fact]
    public void Credits_file_parses_and_credits_the_developer_and_Jake()
    {
        var credits = Credits.Parse(File.ReadAllText(Shipped("credits", "credits.json")));

        Assert.Equal(
            ["Made by: DefinitelyNotZac", "Original idea: Jake"],
            credits.Select(entry => $"{entry.Role}: {string.Join(", ", entry.Names)}"));
    }

    [Fact]
    public void Every_chapterStars_key_is_a_shipped_chapter()
    {
        var config = GameConfig.Parse(File.ReadAllText(Shipped("game_config.json")));

        Assert.All(config.Campaign.ChapterStars.Keys, chapter => Assert.InRange(chapter, 1, Campaign().Chapters.Count));
    }
}
