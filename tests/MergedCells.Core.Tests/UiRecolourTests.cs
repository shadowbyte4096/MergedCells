namespace MergedCells.Core.Tests;

public class UiRecolourTests
{
    // Every colour differs from Teal light's, so each role's target is distinguishable
    private static readonly Palette Target = Palettes.TealLight with
    {
        Background = Rgb.Parse("#000001"),
        Ink = Rgb.Parse("#000002"),
        Card = Rgb.Parse("#000003"),
        Muted = Rgb.Parse("#000004"),
        Chart = Rgb.Parse("#000005"),
        Placeholder = Rgb.Parse("#000006"),
        Disabled = Rgb.Parse("#000007"),
        Pressed = Rgb.Parse("#000008"),
        New = Rgb.Parse("#000009"),
        Fills = [Rgb.Parse("#00000a"), Rgb.Parse("#00000b"), Rgb.Parse("#00000c"), Rgb.Parse("#00000d"), Rgb.Parse("#00000e")],
        Outline = Rgb.Parse("#00000f"),
        WallMiddle = Rgb.Parse("#000010"),
    };

    public static TheoryData<string, string> Roles => new()
    {
        { "#e3f1ed", "#000001" },
        { "#235e56", "#000002" },
        { "#ffffff", "#000003" },
        { "#3a7068", "#000004" },
        { "#e5eceb", "#000006" },
        { "#8dafa9", "#000007" },
        { "#497b74", "#000008" },
        { "#349f8b", "#000009" },
        { "#f2f5f5", "#00000a" },
        { "#abc2bf", "#00000e" },
        { "#a9c5c0", "#000010" },
    };

    [Theory]
    [MemberData(nameof(Roles))]
    public void Map_sends_each_role_to_the_target_s(string from, string to)
    {
        Assert.Equal(Rgb.Parse(to), UiRecolour.Map(Palettes.TealLight, Target)[Rgb.Parse(from)]);
    }

    [Fact]
    public void Shared_source_colour_takes_the_first_role()
    {
        // Teal light's chart is its muted, so by colour alone the chart's items get the target's muted
        Assert.Equal(Target.Muted, UiRecolour.Map(Palettes.TealLight, Target)[Palettes.TealLight.Chart]);
    }

    [Fact]
    public void Ink_shared_with_the_board_pieces_stays_the_ink()
    {
        // Outline, end, wall, trap, teleporter and player are all the ink in Teal; ink comes first
        Assert.Equal(Target.Ink, UiRecolour.Map(Palettes.TealLight, Target)[Palettes.TealLight.Outline]);
    }

    [Fact]
    public void Unknown_colour_is_not_in_the_map()
    {
        Assert.False(UiRecolour.Map(Palettes.TealLight, Target).ContainsKey(Rgb.Parse("#cccccc")));
    }
}
