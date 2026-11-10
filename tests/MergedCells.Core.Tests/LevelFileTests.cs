using System.Globalization;
using System.Text.Json;
using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class LevelFileTests
{
    // The example level in section 7 of the design doc; each invalid case below breaks one thing in it
    private const string Valid =
        """{"id":3,"name":"Two Down, One Across","chapter":1,"regions":[[1,1,2,3],[4,5,2,3],[4,5,6,6],[7,7,7,8]],"tiles":null,"teleporters":null,"start":[0,0],"end":[3,3],"par":3}""";

    private const string Regions = "[[1,1,2,3],[4,5,2,3],[4,5,6,6],[7,7,7,8]]";

    private static string LevelText(int id, string name, int chapter, int par) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $$"""{"id":{{id}},"name":{{JsonSerializer.Serialize(name)}},"chapter":{{chapter}},"regions":{{Regions}},"start":[0,0],"end":[3,3],"par":{{par}}}""");

    // AutoFixture ints mapped into the range a level accepts
    private static int Positive(int value) => Math.Abs(value % 1000) + 1;

    private static string WithTutorial(string tutorial) => Valid.Replace("\"par\":3}", "\"par\":3,\"tutorial\":" + tutorial + "}");

    private static string Lines(params string[] lines) => JsonSerializer.Serialize(lines);

    public static TheoryData<string> InvalidTexts => new()
    {
        Valid.Replace("\"id\":3,", ""),
        Valid.Replace("\"name\":\"Two Down, One Across\",", ""),
        Valid.Replace("\"chapter\":1,", ""),
        Valid.Replace($"\"regions\":{Regions},", ""),
        Valid.Replace("\"start\":[0,0],", ""),
        Valid.Replace("\"end\":[3,3],", ""),
        Valid.Replace(",\"par\":3", ""),
        Valid.Replace("\"Two Down, One Across\"", "null"),
        Valid.Replace(Regions, "null"),
        Valid.Replace("\"start\":[0,0]", "\"start\":null"),
        Valid.Replace("\"end\":[3,3]", "\"end\":null"),
        Valid[..^1] + ",\"extra\":1}",
        Valid.Replace("\"id\":3", "\"id\":\"3\""),
        Valid.Replace("[4,5,2,3]", "[4,5,2]"),
        Valid.Replace("[4,5,2,3]", "null"),
        Valid.Replace("\"start\":[0,0]", "\"start\":[0]"),
        Valid.Replace("\"end\":[3,3]", "\"end\":[3,3,3]"),
        Valid.Replace("\"teleporters\":null", "\"teleporters\":[[[0,1],[3,2]]]"),
        Valid.Replace("\"Two Down, One Across\"", "\"\""),
        Valid.Replace("\"Two Down, One Across\"", "\"  \""),
        Valid.Replace("\"id\":3", "\"id\":0"),
        Valid.Replace("\"chapter\":1", "\"chapter\":0"),
        Valid.Replace("\"par\":3", "\"par\":0"),
        WithTutorial("{}"),
        WithTutorial("{\"lines\":null}"),
        WithTutorial("{\"lines\":[]}"),
        WithTutorial("{\"lines\":[\"a\",\"b\",\"c\"]}"),
        WithTutorial("{\"lines\":[null]}"),
        WithTutorial("{\"lines\":[\" \"]}"),
        WithTutorial("{\"lines\":[\"a\"],\"keyboardLines\":[]}"),
        WithTutorial("{\"lines\":[\"a\"],\"keyboardLines\":[\"\"]}"),
        WithTutorial("{\"lines\":[\"a\"],\"swipeHint\":\"Right\"}"),
        WithTutorial("{\"lines\":[\"a\"],\"swipeHint\":\"north\"}"),
        WithTutorial("{\"lines\":[\"a\"],\"text\":\"a\"}"),
        "null",
        "not json",
        "",
    };

    [Theory]
    [AutoData]
    public void Parse_reads_the_level_fields(int id, string name, int chapter, int par)
    {
        var level = LevelFile.Parse(LevelText(Positive(id), name, Positive(chapter), Positive(par)));

        Assert.Equal((Positive(id), name, Positive(chapter), Positive(par)), (level.Id, level.Name, level.Chapter, level.Par));
    }

    [Fact]
    public void Parse_reads_the_map()
    {
        var expected = TestMaps.Example();

        var map = LevelFile.Parse(Valid).Map;

        Assert.Equal((expected.Start, expected.End), (map.Start, map.End));
        Assert.Equal(expected.Rectangles, map.Rectangles);
    }

    public static TheoryData<string> TwistFieldsNullOrLeftOut => new()
    {
        Valid,
        Valid.Replace("\"tiles\":null,\"teleporters\":null,", ""),
    };

    [Theory]
    [MemberData(nameof(TwistFieldsNullOrLeftOut))]
    public void Twist_fields_may_be_null_or_left_out(string text)
    {
        Assert.Null(Record.Exception(() => LevelFile.Parse(text)));
    }

    [Theory]
    [AutoData]
    public void Parse_reads_a_tutorial_s_lines(string first, string second)
    {
        var tutorial = LevelFile.Parse(WithTutorial($"{{\"lines\":{Lines(first, second)}}}")).Tutorial!;

        Assert.Equal([first, second], tutorial.Lines);
    }

    [Theory]
    [AutoData]
    public void Keyboard_lines_default_to_the_lines(string line)
    {
        var tutorial = LevelFile.Parse(WithTutorial($"{{\"lines\":{Lines(line)},\"keyboardLines\":null}}")).Tutorial!;

        Assert.Equal([line], tutorial.KeyboardLines);
    }

    [Theory]
    [AutoData]
    public void Parse_reads_keyboard_lines(string line, string keyboardLine)
    {
        var tutorial = LevelFile.Parse(WithTutorial($"{{\"lines\":{Lines(line)},\"keyboardLines\":{Lines(keyboardLine)}}}")).Tutorial!;

        Assert.Equal([keyboardLine], tutorial.KeyboardLines);
    }

    [Theory]
    [InlineData("up", Direction.Up)]
    [InlineData("down", Direction.Down)]
    [InlineData("left", Direction.Left)]
    [InlineData("right", Direction.Right)]
    public void Parse_reads_the_swipe_hint(string word, Direction expected)
    {
        var tutorial = LevelFile.Parse(WithTutorial($"{{\"lines\":[\"a\"],\"swipeHint\":\"{word}\"}}")).Tutorial!;

        Assert.Equal(expected, tutorial.SwipeHint);
    }

    [Fact]
    public void A_tutorial_without_a_swipe_hint_has_none()
    {
        var tutorial = LevelFile.Parse(WithTutorial("{\"lines\":[\"a\"],\"swipeHint\":null}")).Tutorial!;

        Assert.Null(tutorial.SwipeHint);
    }

    public static TheoryData<string> TutorialNullOrLeftOut => new() { Valid, WithTutorial("null") };

    [Theory]
    [MemberData(nameof(TutorialNullOrLeftOut))]
    public void A_level_without_a_tutorial_has_none(string text)
    {
        Assert.Null(LevelFile.Parse(text).Tutorial);
    }

    [Fact]
    public void Parse_accepts_a_leading_byte_order_mark()
    {
        Assert.Equal(3, LevelFile.Parse((char)0xFEFF + Valid).Id);
    }

    [Theory]
    [MemberData(nameof(InvalidTexts))]
    public void Invalid_level_throws_JsonException(string text)
    {
        Assert.Throws<JsonException>(() => LevelFile.Parse(text));
    }

    [Theory]
    [InlineData("name", "\"Two Down, One Across\"")]
    [InlineData("regions", Regions)]
    [InlineData("start", "[0,0]")]
    [InlineData("end", "[3,3]")]
    public void Null_value_is_named_in_the_error(string key, string value)
    {
        var text = Valid.Replace($"\"{key}\":{value}", $"\"{key}\":null");

        var message = Assert.Throws<JsonException>(() => LevelFile.Parse(text)).Message;

        Assert.Equal(1, new[] { "name", "regions", "start", "end" }.Count(field => message.Contains($"\"{field}\"")));
        Assert.Contains($"\"{key}\"", message);
    }

    [Fact]
    public void Map_rule_breaks_throw_InvalidMapException()
    {
        var text = Valid.Replace("\"start\":[0,0]", "\"start\":[3,0]").Replace("\"end\":[3,3]", "\"end\":[0,3]");

        Assert.Throws<InvalidMapException>(() => LevelFile.Parse(text));
    }
}
