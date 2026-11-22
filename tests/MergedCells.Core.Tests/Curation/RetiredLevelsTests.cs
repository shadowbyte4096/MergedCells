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
    [InlineData(2, new[] { 2001, 2002 }, new int[0], 2003)]
    [InlineData(2, new[] { 2001 }, new[] { 2002 }, 2003)]
    [InlineData(2, new[] { 1015, 2001, 3001 }, new int[0], 2002)]
    [InlineData(1, new[] { 11, 93 }, new[] { 3 }, 1001)]
    [InlineData(4, new int[0], new int[0], 4001)]
    public void Next_id_is_one_past_the_highest_in_the_chapter_s_block(int chapter, int[] levelIds, int[] retired, int next)
    {
        Assert.Equal(next, RetiredLevels.NextId(chapter, levelIds, retired));
    }

    [Fact]
    public void Next_id_is_null_once_the_block_s_last_id_is_used()
    {
        Assert.Null(RetiredLevels.NextId(1, [1001], [1999]));
    }
}
