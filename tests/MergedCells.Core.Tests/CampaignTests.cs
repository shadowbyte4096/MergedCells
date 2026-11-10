using System.Text.Json;
using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class CampaignTests
{
    private static Level LevelWith(int id, int chapter) => new(id, $"Level {id}", chapter, TestMaps.Example(), 3);

    private static string ChapterText(params (string Name, int[] Ids)[] chapters) =>
        $$"""{"chapters":[{{string.Join(",", chapters.Select(c => $$"""{"name":{{JsonSerializer.Serialize(c.Name)}},"levels":[{{string.Join(",", c.Ids)}}]}"""))}}]}""";

    // Level 1 in chapter 1 and level 2 in chapter 2; each invalid case below breaks one thing
    private static readonly Level[] TwoLevels = [LevelWith(1, 1), LevelWith(2, 2)];

    private const string Valid = """{"chapters":[{"name":"A","levels":[1]},{"name":"B","levels":[2]}]}""";

    private static Campaign ThreeLevels(string first = "First", string second = "Second") =>
        Campaign.Parse(ChapterText((first, [3, 1]), (second, [2])), [LevelWith(1, 1), LevelWith(2, 2), LevelWith(3, 1)]);

    [Theory]
    [AutoData]
    public void Parse_reads_names_and_order(string first, string second)
    {
        var campaign = ThreeLevels(first, second);

        Assert.Equal(
            (first, second, "3,1", "2"),
            (campaign.Chapters[0].Name, campaign.Chapters[1].Name,
                string.Join(",", campaign.Chapters[0].Levels.Select(level => level.Id)),
                string.Join(",", campaign.Chapters[1].Levels.Select(level => level.Id))));
    }

    [Fact]
    public void Chapters_are_numbered_by_position()
    {
        Assert.Equal([1, 2], ThreeLevels().Chapters.Select(chapter => chapter.Number));
    }

    [Fact]
    public void Levels_run_in_play_order_across_chapters()
    {
        Assert.Equal([3, 1, 2], ThreeLevels().Levels.Select(level => level.Id));
    }

    [Theory]
    [InlineData(3, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 1)]
    public void NumberInChapter_counts_from_one_within_each_chapter(int id, int expected)
    {
        var campaign = ThreeLevels();

        Assert.Equal(expected, campaign.NumberInChapter(campaign.Levels.Single(level => level.Id == id)));
    }

    [Fact]
    public void ChapterOf_finds_the_listing_chapter()
    {
        var campaign = ThreeLevels();

        Assert.Equal(2, campaign.ChapterOf(campaign.Levels.Single(level => level.Id == 2)).Number);
    }

    [Fact]
    public void ChapterOf_throws_for_a_level_not_in_the_campaign()
    {
        Assert.Throws<ArgumentException>(() => ThreeLevels().ChapterOf(LevelWith(9, 1)));
    }

    [Fact]
    public void NumberInChapter_throws_for_a_level_not_in_the_campaign()
    {
        Assert.Throws<ArgumentException>(() => ThreeLevels().NumberInChapter(LevelWith(9, 1)));
    }

    [Theory]
    [AutoData]
    public void Parse_accepts_a_leading_byte_order_mark(string name)
    {
        var campaign = Campaign.Parse((char)0xFEFF + ChapterText((name, [1])), [LevelWith(1, 1)]);

        Assert.Equal(name, campaign.Chapters[0].Name);
    }

    [Theory]
    [AutoData]
    public void Parse_reads_a_chapter_s_intro(string first, string second)
    {
        var text = Valid.Replace("\"levels\":[2]", $"\"levels\":[2],\"intro\":{JsonSerializer.Serialize(new[] { first, second })}");

        Assert.Equal([first, second], Campaign.Parse(text, TwoLevels).Chapters[1].Intro);
    }

    public static TheoryData<string> IntroNullOrLeftOut => new() { Valid, Valid.Replace("\"levels\":[2]", "\"levels\":[2],\"intro\":null") };

    [Theory]
    [MemberData(nameof(IntroNullOrLeftOut))]
    public void A_chapter_without_an_intro_has_no_lines(string text)
    {
        Assert.Empty(Campaign.Parse(text, TwoLevels).Chapters[1].Intro);
    }

    [Fact]
    public void Valid_text_used_by_the_invalid_cases_parses()
    {
        Assert.Null(Record.Exception(() => Campaign.Parse(Valid, TwoLevels)));
    }

    // The loaders catch JsonException, so two files claiming one ID must not surface as ToDictionary's ArgumentException
    [Fact]
    public void Two_level_files_with_one_id_throw_JsonException()
    {
        Assert.Throws<JsonException>(() => Campaign.Parse(Valid, [.. TwoLevels, LevelWith(1, 1)]));
    }

    public static TheoryData<string> InvalidTexts => new()
    {
        "{}",
        """{"chapters":null}""",
        """{"chapters":[]}""",
        """{"chapters":[null,{"name":"B","levels":[2]}]}""",
        Valid.Replace("\"name\":\"A\"", "\"name\":null"),
        Valid.Replace("\"name\":\"A\"", "\"name\":\"\""),
        Valid.Replace("\"name\":\"A\"", "\"name\":\"  \""),
        Valid.Replace("\"name\":\"A\",", ""),
        Valid.Replace("\"levels\":[1]", "\"levels\":null"),
        Valid.Replace(",\"levels\":[1]", ""),
        Valid.Replace("\"levels\":[1]", "\"levels\":[]"),
        Valid.Replace("\"levels\":[1]", "\"levels\":[1],\"extra\":1"),
        Valid[..^1] + ",\"extra\":1}",
        Valid.Replace("\"levels\":[1]", "\"levels\":[1,1]"),
        Valid.Replace("\"levels\":[2]", "\"levels\":[2,1]"),
        Valid.Replace("\"levels\":[2]", "\"levels\":[2,7]"),
        """{"chapters":[{"name":"A","levels":[1]}]}""",
        """{"chapters":[{"name":"A","levels":[1,2]}]}""",
        Valid.Replace("\"levels\":[1]", "\"levels\":[\"1\"]"),
        Valid.Replace("\"levels\":[2]", "\"levels\":[2],\"intro\":[]"),
        Valid.Replace("\"levels\":[2]", "\"levels\":[2],\"intro\":[\"a\",\"b\",\"c\"]"),
        Valid.Replace("\"levels\":[2]", "\"levels\":[2],\"intro\":[null]"),
        Valid.Replace("\"levels\":[2]", "\"levels\":[2],\"intro\":[\" \"]"),
        "null",
        Valid.Replace("\"name\":\"A\"", "\"name\":\"A\",\"name\":\"C\""),
        Valid.Replace("\"levels\":[2]", "\"levels\":[7],\"levels\":[2]"),
    };

    [Theory]
    [MemberData(nameof(InvalidTexts))]
    public void Invalid_chapter_file_throws_JsonException(string text)
    {
        Assert.Throws<JsonException>(() => Campaign.Parse(text, TwoLevels));
    }

    // A case that broke the JSON itself would throw without testing the rule it was written for
    [Theory]
    [MemberData(nameof(InvalidTexts))]
    public void Every_invalid_case_is_still_JSON(string text)
    {
        Assert.Null(Record.Exception(() => JsonDocument.Parse(text).Dispose()));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("")]
    public void Text_that_is_not_JSON_throws_JsonException(string text)
    {
        Assert.Throws<JsonException>(() => Campaign.Parse(text, TwoLevels));
    }

    [Fact]
    public void A_repeated_key_is_named_in_the_error()
    {
        var text = Valid.Replace("\"name\":\"A\"", "\"name\":\"A\",\"name\":\"C\"");

        Assert.Contains("\"name\"", Assert.Throws<JsonException>(() => Campaign.Parse(text, TwoLevels)).Message, StringComparison.Ordinal);
    }
}
