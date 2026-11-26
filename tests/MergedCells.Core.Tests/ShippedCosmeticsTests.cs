using System.Globalization;
using System.Text.RegularExpressions;

namespace MergedCells.Core.Tests;

// The shipped catalogue is valid and agrees with the default theme files, never what its colours are: tuning a palette
// breaks no test unless it makes the theme harder to read
public partial class ShippedCosmeticsTests
{
    private static readonly Cosmetics Shipped = Cosmetics.Parse(Read("cosmetics", "cosmetics.json"));

    private static string Read(params string[] path) => File.ReadAllText(Path.Combine([AppContext.BaseDirectory, .. path]));

    private static string ThemeFile(string name) => Read("themes", name);

    private static string Mode(bool dark) => dark ? "dark" : "light";

    [GeneratedRegex(@"Color\(([0-9.e-]+), ([0-9.e-]+), ([0-9.e-]+), ([0-9.e-]+)\)")]
    private static partial Regex ColourPattern();

    private static Rgb ToRgb(Match match) => new(Channel(match.Groups[1].Value), Channel(match.Groups[2].Value), Channel(match.Groups[3].Value));

    private static byte Channel(string value) => (byte)Math.Round(double.Parse(value, CultureInfo.InvariantCulture) * 255);

    // A property line's value, such as "Outline = Color(...)"
    private static string Property(string text, string name) =>
        text.Split('\n').Single(line => line.StartsWith(name + " = ", StringComparison.Ordinal))[(name.Length + 3)..].Trim();

    private static Rgb ColourOf(string text, string name) => ToRgb(ColourPattern().Match(Property(text, name)));

    public static TheoryData<string, bool> EveryPalette()
    {
        var data = new TheoryData<string, bool>();
        foreach (var theme in Shipped.Themes)
        {
            data.Add(theme.Id, false);
            data.Add(theme.Id, true);
        }
        return data;
    }

    public static TheoryData<bool> BothModes => new() { false, true };

    [Fact]
    public void Shipped_catalogue_has_themes_and_looks()
    {
        Assert.NotEmpty(Shipped.Themes.Skip(1));
        Assert.NotEmpty(Shipped.Looks.Skip(1));
    }

    [Theory]
    [MemberData(nameof(EveryPalette))]
    public void Every_palette_passes_the_contrast_rules(string id, bool dark)
    {
        Assert.Empty(PaletteContrast.Problems(Shipped.ThemeFor(id).For(dark)));
    }

    [Theory]
    [MemberData(nameof(BothModes))]
    public void Every_ui_theme_colour_is_a_teal_role(bool dark)
    {
        var teal = Shipped.DefaultTheme.For(dark);
        var roles = UiRecolour.Map(teal, teal);
        var colours = ColourPattern().Matches(ThemeFile($"{Mode(dark)}_ui_theme.tres")).Select(ToRgb).Distinct();

        Assert.Empty(colours.Where(colour => !roles.ContainsKey(colour)).Select(colour => colour.ToString()));
    }

    // CosmeticLooks recolours colours, StyleBoxFlat and three baked icons; any other kind of resource in a UI theme file
    // would keep Teal's colours in every bought theme, so it has to be taught to CosmeticLooks first
    [Theory]
    [MemberData(nameof(BothModes))]
    public void Ui_theme_files_hold_only_resources_a_theme_recolours(bool dark)
    {
        var text = ThemeFile($"{Mode(dark)}_ui_theme.tres");
        var subResources = SubResourcePattern().Matches(text).Select(match => match.Groups[1].Value).Distinct();
        var textures = TexturePattern().Matches(text).Select(match => match.Groups[1].Value.Split('/')[^1]);
        string[] baked = [$"switch-on-{Mode(dark)}.svg", $"switch-off-{Mode(dark)}.svg", $"slider-knob-{Mode(dark)}.svg"];

        Assert.Equal(
            (string.Empty, string.Empty),
            (string.Join(" ", subResources.Except(["StyleBoxFlat", "StyleBoxEmpty"])), string.Join(" ", textures.Except(baked))));
    }

    [GeneratedRegex(@"\[sub_resource type=""(\w+)""")]
    private static partial Regex SubResourcePattern();

    [GeneratedRegex(@"\[ext_resource type=""Texture2D"" path=""([^""]+)""")]
    private static partial Regex TexturePattern();

    // A colour set in a scene would show in every theme, as no theme reaches it
    [Fact]
    public void No_scene_sets_a_colour()
    {
        var scenes = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "scenes"), "*.tscn", SearchOption.AllDirectories);

        Assert.NotEmpty(scenes);
        Assert.Empty(scenes.Where(scene => ColourPattern().IsMatch(File.ReadAllText(scene))).Select(Path.GetFileName));
    }

    // Light's chart is its muted, so only dark tells the chart's items apart: an item left out of ChartItems would get a
    // bought theme's muted, which has no contrast rule on the placeholder panel
    [Fact]
    public void Only_chart_items_hold_teal_dark_s_chart()
    {
        var chart = Shipped.DefaultTheme.Dark.Chart;
        var holders = ThemeFile("dark_ui_theme.tres").Split('\n')
            .Where(line => ColourPattern().Match(line) is { Success: true } match && ToRgb(match) == chart)
            .Select(line => line.Split(" = ")[0].Trim());

        Assert.Equal(UiRecolour.ChartItems.Order(StringComparer.Ordinal), holders.Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(BothModes))]
    public void Chart_items_hold_teal_chart(bool dark)
    {
        var text = ThemeFile($"{Mode(dark)}_ui_theme.tres");

        Assert.All(UiRecolour.ChartItems, item => Assert.Equal(Shipped.DefaultTheme.For(dark).Chart, ColourOf(text, item)));
    }

    [Theory]
    [MemberData(nameof(BothModes))]
    public void Board_theme_files_match_teal(bool dark)
    {
        var text = ThemeFile($"{Mode(dark)}_board_theme.tres");
        var teal = Shipped.DefaultTheme.For(dark);
        var fills = SplitFills(Property(text, "Fills"));

        Assert.Equal(
            (teal.Background, teal.Outline, teal.End, teal.Wall, teal.WallMiddle, teal.Trap, teal.Teleporter, string.Join(" ", teal.Fills)),
            (ColourOf(text, "Background"), ColourOf(text, "Outline"), ColourOf(text, "End"), ColourOf(text, "Wall"), ColourOf(text, "WallMiddle"),
                ColourOf(text, "Trap"), ColourOf(text, "Teleporter"), string.Join(" ", fills)));
    }

    // PackedColorArray(r, g, b, a, r, g, b, a, ...)
    private static Rgb[] SplitFills(string value)
    {
        var numbers = value["PackedColorArray(".Length..^1].Split(',').Select(number => number.Trim()).ToArray();
        return Enumerable.Range(0, numbers.Length / 4).Select(i => new Rgb(Channel(numbers[i * 4]), Channel(numbers[i * 4 + 1]), Channel(numbers[i * 4 + 2]))).ToArray();
    }

    [Theory]
    [MemberData(nameof(BothModes))]
    public void Player_look_files_match_teal_and_square(bool dark)
    {
        var text = ThemeFile($"{Mode(dark)}_player_look.tres");
        var square = Shipped.DefaultLook;
        double Number(string name) => double.Parse(Property(text, name), CultureInfo.InvariantCulture);

        Assert.Equal(
            (Shipped.DefaultTheme.For(dark).Player, Shipped.DefaultTheme.For(dark).Player, square.Side, square.Stroke, square.Corner),
            (ColourOf(text, "Color"), ColourOf(text, "StartOutline"), Number("SideFraction"), Number("StrokeFraction"), Number("CornerFraction")));
    }

    [Fact]
    public void Shipped_prices_cover_the_catalogue()
    {
        Shipped.CheckPrices(GameConfig.Parse(Read("game_config.json")).Shop);
    }
}
