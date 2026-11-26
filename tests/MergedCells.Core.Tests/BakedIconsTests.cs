namespace MergedCells.Core.Tests;

public class BakedIconsTests
{
    public static TheoryData<string, bool> ShippedIcons => new()
    {
        { "switch-on-light.svg", false },
        { "switch-off-light.svg", false },
        { "slider-knob-light.svg", false },
        { "switch-on-dark.svg", true },
        { "switch-off-dark.svg", true },
        { "slider-knob-dark.svg", true },
    };

    // A restyled icon file must change its copy here too, or bought themes would keep the old shape
    [Theory]
    [MemberData(nameof(ShippedIcons))]
    public void Teal_gives_the_shipped_icon(string file, bool dark)
    {
        var shipped = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "themes", "icons", file)).TrimEnd();

        Assert.Equal(shipped, BakedIcons.For(file, dark ? Palettes.TealDark : Palettes.TealLight));
    }

    [Theory]
    [InlineData("undo.svg")]
    [InlineData("settings.svg")]
    public void A_white_icon_is_not_baked(string file)
    {
        Assert.Null(BakedIcons.For(file, Palettes.TealLight));
    }

    [Fact]
    public void A_baked_icon_takes_the_palette_s_colours()
    {
        var palette = Palettes.TealLight with { Ink = Rgb.Parse("#123456"), Card = Rgb.Parse("#abcdef") };

        Assert.Equal(
            (true, true, false),
            (BakedIcons.For("switch-on-light.svg", palette)!.Contains("#123456", StringComparison.Ordinal),
                BakedIcons.For("switch-on-light.svg", palette)!.Contains("#abcdef", StringComparison.Ordinal),
                BakedIcons.For("switch-on-light.svg", palette)!.Contains("#235e56", StringComparison.Ordinal)));
    }
}
