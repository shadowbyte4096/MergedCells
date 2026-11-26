using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class RgbTests
{
    [Theory, AutoData]
    public void Parse_round_trips(byte r, byte g, byte b)
    {
        var colour = new Rgb(r, g, b);

        Assert.Equal(colour, Rgb.Parse(colour.ToString()));
    }

    [Fact]
    public void To_string_is_lowercase_hash_and_six_digits()
    {
        Assert.Equal("#235e56", new Rgb(0x23, 0x5e, 0x56).ToString());
    }

    [Theory]
    [InlineData("235e56")]
    [InlineData("#235E56")]
    [InlineData("#235e5")]
    [InlineData("#235e5600")]
    [InlineData("#23 e56")]
    [InlineData("")]
    public void Parse_rejects(string text)
    {
        Assert.Throws<FormatException>(() => Rgb.Parse(text));
    }

    [Fact]
    public void Try_parse_refuses_null()
    {
        Assert.False(Rgb.TryParse(null, out _));
    }
}
