using System.Globalization;
using System.Text.Json;
using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class GameConfigTests
{
    private static string ConfigText(float threshold) =>
        """{"input":{"swipeThreshold":""" + threshold.ToString(CultureInfo.InvariantCulture) + "}}";

    [Theory]
    [AutoData]
    public void Parse_reads_the_swipe_threshold(float threshold)
    {
        Assert.Equal(threshold, GameConfig.Parse(ConfigText(threshold)).Input.SwipeThreshold);
    }

    [Fact]
    public void Shipped_config_parses_and_its_threshold_is_accepted()
    {
        var config = GameConfig.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "game_config.json")));

        Assert.Null(Record.Exception(() => new SwipeDetector(config.Input.SwipeThreshold)));
    }

    [Theory]
    [AutoData]
    public void Parse_accepts_a_leading_byte_order_mark(float threshold)
    {
        Assert.Equal(threshold, GameConfig.Parse((char)0xFEFF + ConfigText(threshold)).Input.SwipeThreshold);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"input":{}}""")]
    [InlineData("""{"input":{"swipeThreshold":1},"extra":1}""")]
    [InlineData("""{"input":{"swipeThreshold":1,"extra":1}}""")]
    [InlineData("""{"input":null}""")]
    [InlineData("null")]
    [InlineData("""{"input":{"swipeThreshold":"1"}}""")]
    [InlineData("not json")]
    [InlineData("")]
    public void Invalid_config_throws_JsonException(string text)
    {
        Assert.Throws<JsonException>(() => GameConfig.Parse(text));
    }
}
