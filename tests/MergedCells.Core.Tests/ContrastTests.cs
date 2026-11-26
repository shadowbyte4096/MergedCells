using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class ContrastTests
{
    [Fact]
    public void Black_on_white_is_21()
    {
        Assert.Equal(21, Contrast.Ratio(new Rgb(0, 0, 0), new Rgb(255, 255, 255)), 0.01);
    }

    [Theory, AutoData]
    public void Colour_on_itself_is_1(byte r, byte g, byte b)
    {
        Assert.Equal(1, Contrast.Ratio(new Rgb(r, g, b), new Rgb(r, g, b)), 1e-9);
    }

    [Theory, AutoData]
    public void Order_does_not_matter(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2)
    {
        var a = new Rgb(r1, g1, b1);
        var b = new Rgb(r2, g2, b2);

        Assert.Equal(Contrast.Ratio(a, b), Contrast.Ratio(b, a), 1e-12);
    }

    [Fact]
    public void Teal_ink_on_its_card_matches_the_mockup()
    {
        // The mockup page's figure for the default light theme, worked out independently in JavaScript
        Assert.Equal(7.5, Contrast.Ratio(Rgb.Parse("#235e56"), Rgb.Parse("#ffffff")), 0.05);
    }
}
