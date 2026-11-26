namespace MergedCells.Core.Tests;

public class PaletteContrastTests
{
    [Fact]
    public void Teal_light_passes()
    {
        Assert.Empty(PaletteContrast.Problems(Palettes.TealLight));
    }

    [Fact]
    public void Teal_dark_passes()
    {
        Assert.Empty(PaletteContrast.Problems(Palettes.TealDark));
    }

    [Fact]
    public void Low_ink_on_background_is_named()
    {
        var palette = Palettes.TealLight with { Ink = Rgb.Parse("#a9c5c0") };

        Assert.Contains(PaletteContrast.Problems(palette), problem => problem.StartsWith("ink on background is ", StringComparison.Ordinal));
    }

    [Fact]
    public void Muted_text_on_the_placeholder_is_not_a_rule()
    {
        // Dark Teal's muted is 3.68:1 on its placeholder, which is why chart exists
        var palette = Palettes.TealDark with { Chart = Palettes.TealDark.Ink };

        Assert.Empty(PaletteContrast.Problems(palette));
    }

    [Fact]
    public void Low_chart_on_the_placeholder_is_named()
    {
        var palette = Palettes.TealDark with { Chart = Palettes.TealDark.Muted };

        Assert.Contains(PaletteContrast.Problems(palette), problem => problem.StartsWith("chart on placeholder is 3.", StringComparison.Ordinal));
    }

    [Fact]
    public void Card_text_on_a_pressed_button_is_a_rule()
    {
        var palette = Palettes.TealLight with { Pressed = Rgb.Parse("#8dafa9") };

        Assert.Contains(PaletteContrast.Problems(palette), problem => problem.StartsWith("card on pressed is ", StringComparison.Ordinal));
    }

    [Fact]
    public void New_is_judged_as_large_text()
    {
        // Teal light's new-star teal is 3.2:1 on white, which passes only at 3:1
        var palette = Palettes.TealLight with { New = Rgb.Parse("#8dafa9") };

        Assert.Contains(PaletteContrast.Problems(palette), problem => problem.StartsWith("new on card is ", StringComparison.Ordinal));
    }

    [Fact]
    public void Player_close_to_one_fill_is_named()
    {
        var palette = Palettes.TealLight with { Player = Rgb.Parse("#9db8b4") };

        Assert.Contains(PaletteContrast.Problems(palette), problem => problem.StartsWith("player on fill ", StringComparison.Ordinal));
    }

    [Fact]
    public void Repeated_fill_is_named()
    {
        var fills = Palettes.TealLight.Fills.ToArray();
        fills[3] = fills[1];

        Assert.Contains("fills 2 and 4 are the same colour", PaletteContrast.Problems(Palettes.TealLight with { Fills = fills }));
    }

    [Fact]
    public void Disabled_has_no_rule()
    {
        var palette = Palettes.TealLight with { Disabled = Palettes.TealLight.Background };

        Assert.Empty(PaletteContrast.Problems(palette));
    }

    [Fact]
    public void Problem_names_the_ratio_and_the_minimum()
    {
        var palette = Palettes.TealLight with { Muted = Rgb.Parse("#8dafa9") };

        Assert.Contains(PaletteContrast.Problems(palette), problem => problem.EndsWith(":1, below 4.5:1", StringComparison.Ordinal));
    }
}
