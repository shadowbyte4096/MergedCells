namespace MergedCells.Core;

/// <summary>
/// How a bought theme recolours the default menu theme: each colour in Teal's file is a palette role, found by its
/// value, and becomes the target palette's colour for that role, alpha kept. Where two roles share a colour in Teal
/// (light's chart is its muted, and the board pieces are its ink) the first role in <see cref="Map"/>'s order wins, so
/// the items that are really the chart's are named in <see cref="ChartItems"/>.
/// </summary>
public static class UiRecolour
{
    /// <summary>The menu theme items that take the palette's <see cref="Palette.Chart"/>: text on the placeholder panel.</summary>
    public static IReadOnlyList<string> ChartItems { get; } =
        ["BestMovesChart/colors/muted", "StatKey/colors/font_color", "StatNote/colors/font_color"];

    public static IReadOnlyDictionary<Rgb, Rgb> Map(Palette from, Palette to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        var map = new Dictionary<Rgb, Rgb>();
        foreach (var (source, target) in Roles(from).Zip(Roles(to)))
        {
            map.TryAdd(source, target);
        }
        return map;
    }

    private static IEnumerable<Rgb> Roles(Palette palette) =>
    [
        palette.Background, palette.Ink, palette.Card, palette.Muted, palette.Chart, palette.Placeholder, palette.Disabled,
        palette.Pressed, palette.New, palette.Outline, palette.End, palette.Wall, palette.WallMiddle, palette.Trap,
        palette.Teleporter, palette.Player, .. palette.Fills,
    ];
}
