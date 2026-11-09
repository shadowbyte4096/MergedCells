namespace MergedCells.Core.Tests;

public class NoticeTextTests
{
    public static TheoryData<string, string[]> Splits => new()
    {
        { "a\nb\n\nc", ["a\nb", "c"] },
        { "a\r\n\r\nb", ["a", "b"] },
        { "a\n  \t\nb", ["a", "b"] },
        { "\n\na\n\n", ["a"] },
        { "a\n\n\n\nb", ["a", "b"] },
        { "  a\n  b", ["  a\n  b"] },
        { "a  \nb", ["a\nb"] },
        { "", [] },
    };

    [Theory]
    [MemberData(nameof(Splits))]
    public void Paragraphs_split_on_blank_lines(string text, string[] expected)
    {
        Assert.Equal(expected, NoticeText.Paragraphs(text));
    }

    [Fact]
    public void Component_lists_each_part_s_copyright_then_licence()
    {
        var text = NoticeText.Component(
            "FreeType",
            [new ComponentPart(["2006-2024, David Turner"], "FTL"), new ComponentPart(["2010, Someone", "2011, Other"], "MIT")]);

        Assert.Equal(
            "FreeType\nCopyright 2006-2024, David Turner\nLicence: FTL\nCopyright 2010, Someone\nCopyright 2011, Other\nLicence: MIT",
            text);
    }
}
