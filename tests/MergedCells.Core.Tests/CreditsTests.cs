using System.Text.Json;
using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class CreditsTests
{
    private const string Valid = """{"credits":[{"role":"Made by","names":["A","B"]}]}""";

    private static string CreditsText(string role, params string[] names) =>
        $$"""{"credits":[{"role":{{JsonSerializer.Serialize(role)}},"names":{{JsonSerializer.Serialize(names)}}}]}""";

    [Theory]
    [AutoData]
    public void Parse_reads_roles_and_names(string role, string first, string second)
    {
        var entry = Credits.Parse(CreditsText(role, first, second)).Single();

        Assert.Equal((role, $"{first}|{second}"), (entry.Role, string.Join("|", entry.Names)));
    }

    [Theory]
    [AutoData]
    public void Parse_keeps_the_entries_in_order(string first, string second)
    {
        var text = $$"""{"credits":[{"role":{{JsonSerializer.Serialize(first)}},"names":["A"]},{"role":{{JsonSerializer.Serialize(second)}},"names":["B"]}]}""";

        Assert.Equal([first, second], Credits.Parse(text).Select(entry => entry.Role));
    }

    [Theory]
    [AutoData]
    public void Parse_accepts_a_leading_byte_order_mark(string role, string name)
    {
        Assert.Equal(role, Credits.Parse((char)0xFEFF + CreditsText(role, name)).Single().Role);
    }

    [Fact]
    public void Valid_text_used_by_the_invalid_cases_parses()
    {
        Assert.Null(Record.Exception(() => Credits.Parse(Valid)));
    }

    public static TheoryData<string> InvalidTexts => new()
    {
        "{}",
        """{"credits":null}""",
        """{"credits":[]}""",
        """{"credits":[null]}""",
        Valid.Replace("\"role\":\"Made by\"", "\"role\":null"),
        Valid.Replace("\"role\":\"Made by\"", "\"role\":\"\""),
        Valid.Replace("\"role\":\"Made by\"", "\"role\":\"  \""),
        Valid.Replace("\"role\":\"Made by\",", ""),
        Valid.Replace("[\"A\",\"B\"]", "null"),
        Valid.Replace(",\"names\":[\"A\",\"B\"]", ""),
        Valid.Replace("[\"A\",\"B\"]", "[]"),
        Valid.Replace("\"A\"", "null"),
        Valid.Replace("\"A\"", "\"\""),
        Valid.Replace("\"A\"", "\"  \""),
        Valid.Replace("\"names\"", "\"extra\":1,\"names\""),
        Valid[..^1] + ",\"extra\":1}",
        "null",
        "not json",
        "",
    };

    [Theory]
    [MemberData(nameof(InvalidTexts))]
    public void Invalid_credits_throw_JsonException(string text)
    {
        Assert.Throws<JsonException>(() => Credits.Parse(text));
    }
}
