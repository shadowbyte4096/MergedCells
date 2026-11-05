using System.Globalization;
using System.Text.Json;
using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class GameConfigTests
{
    // Every group with a value of 1, so each invalid case below breaks exactly one thing
    private const string Valid =
        """{"input":{"swipeThreshold":1},"slide":{"squaresPerSecond":1,"maxSeconds":1},"stars":{"twoStarMinExtraMoves":1,"twoStarExtraParPercent":1,"oneStarMinExtraMoves":1,"oneStarExtraParPercent":1}}""";

    private static string ConfigText(
        float threshold = 1,
        double squaresPerSecond = 1,
        double maxSeconds = 1,
        int twoStarMin = 1,
        int twoStarPercent = 1,
        int oneStarMin = 1,
        int oneStarPercent = 1) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $$$"""{"input":{"swipeThreshold":{{{threshold}}}},"slide":{"squaresPerSecond":{{{squaresPerSecond}}},"maxSeconds":{{{maxSeconds}}}},"stars":{"twoStarMinExtraMoves":{{{twoStarMin}}},"twoStarExtraParPercent":{{{twoStarPercent}}},"oneStarMinExtraMoves":{{{oneStarMin}}},"oneStarExtraParPercent":{{{oneStarPercent}}}}}""");

    private static GameConfig Shipped() =>
        GameConfig.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "game_config.json")));

    public static TheoryData<string> InvalidTexts => new()
    {
        "{}",
        Valid.Replace("""{"input":{"swipeThreshold":1},""", "{"),
        Valid.Replace(""","slide":{"squaresPerSecond":1,"maxSeconds":1}""", ""),
        Valid.Replace(""","stars":{"twoStarMinExtraMoves":1,"twoStarExtraParPercent":1,"oneStarMinExtraMoves":1,"oneStarExtraParPercent":1}""", ""),
        Valid.Replace("""{"swipeThreshold":1}""", "null"),
        Valid.Replace("""{"squaresPerSecond":1,"maxSeconds":1}""", "null"),
        Valid.Replace("""{"twoStarMinExtraMoves":1,"twoStarExtraParPercent":1,"oneStarMinExtraMoves":1,"oneStarExtraParPercent":1}""", "null"),
        Valid.Replace("""{"swipeThreshold":1}""", "{}"),
        Valid.Replace("""{"squaresPerSecond":1,""", "{"),
        Valid.Replace(""","maxSeconds":1""", ""),
        Valid.Replace("""{"twoStarMinExtraMoves":1,""", "{"),
        Valid.Replace(""","twoStarExtraParPercent":1""", ""),
        Valid.Replace(""","oneStarMinExtraMoves":1""", ""),
        Valid.Replace(""","oneStarExtraParPercent":1""", ""),
        Valid[..^1] + ",\"extra\":1}",
        Valid.Replace("\"swipeThreshold\":1", "\"swipeThreshold\":1,\"extra\":1"),
        Valid.Replace("\"maxSeconds\":1", "\"maxSeconds\":1,\"extra\":1"),
        Valid.Replace("\"twoStarExtraParPercent\":1", "\"twoStarExtraParPercent\":1,\"extra\":1"),
        Valid.Replace("\"swipeThreshold\":1", "\"swipeThreshold\":\"1\""),
        Valid.Replace("\"maxSeconds\":1", "\"maxSeconds\":\"1\""),
        Valid.Replace("\"twoStarMinExtraMoves\":1", "\"twoStarMinExtraMoves\":2.5"),
        "null",
        "not json",
        "",
    };

    [Theory]
    [AutoData]
    public void Parse_reads_the_swipe_threshold(float threshold)
    {
        Assert.Equal(threshold, GameConfig.Parse(ConfigText(threshold: threshold)).Input.SwipeThreshold);
    }

    [Theory]
    [AutoData]
    public void Parse_reads_the_slide_group(double squaresPerSecond, double maxSeconds)
    {
        var slide = GameConfig.Parse(ConfigText(squaresPerSecond: squaresPerSecond, maxSeconds: maxSeconds)).Slide;

        Assert.Equal((squaresPerSecond, maxSeconds), (slide.SquaresPerSecond, slide.MaxSeconds));
    }

    [Theory]
    [AutoData]
    public void Parse_reads_the_stars_group(int twoStarMin, int twoStarPercent, int oneStarMin, int oneStarPercent)
    {
        var stars = GameConfig.Parse(ConfigText(
            twoStarMin: twoStarMin, twoStarPercent: twoStarPercent, oneStarMin: oneStarMin, oneStarPercent: oneStarPercent)).Stars;

        Assert.Equal(
            (twoStarMin, twoStarPercent, oneStarMin, oneStarPercent),
            (stars.TwoStarMinExtraMoves, stars.TwoStarExtraParPercent, stars.OneStarMinExtraMoves, stars.OneStarExtraParPercent));
    }

    [Fact]
    public void Shipped_config_parses_and_its_threshold_is_accepted()
    {
        var config = Shipped();

        Assert.Null(Record.Exception(() => new SwipeDetector(config.Input.SwipeThreshold)));
    }

    [Fact]
    public void Shipped_star_thresholds_are_accepted()
    {
        var config = Shipped();

        // Earned checks all four threshold values
        Assert.Null(Record.Exception(() => Stars.Earned(1, 1, config.Stars)));
    }

    [Fact]
    public void Shipped_slide_timing_is_accepted()
    {
        var config = Shipped();

        Assert.Null(Record.Exception(() => SlideTiming.Seconds(1, config.Slide)));
    }

    [Theory]
    [AutoData]
    public void Parse_accepts_a_leading_byte_order_mark(float threshold)
    {
        Assert.Equal(threshold, GameConfig.Parse((char)0xFEFF + ConfigText(threshold: threshold)).Input.SwipeThreshold);
    }

    [Fact]
    public void Valid_text_used_by_the_invalid_cases_parses()
    {
        Assert.Null(Record.Exception(() => GameConfig.Parse(Valid)));
    }

    [Theory]
    [MemberData(nameof(InvalidTexts))]
    public void Invalid_config_throws_JsonException(string text)
    {
        Assert.Throws<JsonException>(() => GameConfig.Parse(text));
    }
}
