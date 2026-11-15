using System.Text.Json;
using MergedCells.Curation;

namespace MergedCells.Core.Tests;

public class RetiredLevelsTests
{
    [Fact]
    public void Shipped_file_round_trips()
    {
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "curation", "retired-levels.json"));

        Assert.Equal(text, RetiredLevels.Write(RetiredLevels.Parse(text)));
    }

    [Fact]
    public void Write_sorts_the_ids()
    {
        Assert.Equal("{\n  \"retired\": [3, 9, 14]\n}\n", RetiredLevels.Write([14, 3, 9]));
    }

    [Theory]
    [InlineData("""{"retired":null}""")]
    [InlineData("""{"retired":[0]}""")]
    [InlineData("""{"retired":[3],"extra":1}""")]
    [InlineData("""{"retired":[3],"retired":[4]}""")]
    [InlineData("""{}""")]
    [InlineData("null")]
    public void Invalid_file_throws_JsonException(string text)
    {
        Assert.Throws<JsonException>(() => RetiredLevels.Parse(text));
    }

    [Theory]
    [InlineData(new[] { 1, 2, 5 }, new[] { 7 }, 8)]
    [InlineData(new[] { 1, 9 }, new[] { 3 }, 10)]
    [InlineData(new int[0], new int[0], 1)]
    public void Next_id_is_one_past_the_highest(int[] levelIds, int[] retired, int next)
    {
        Assert.Equal(next, RetiredLevels.NextId(levelIds, retired));
    }
}
