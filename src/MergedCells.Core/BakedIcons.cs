namespace MergedCells.Core;

/// <summary>
/// The menu theme's three icons with colours baked in (the switch on and off, and the slider's knob), as SVG text in a
/// palette's colours, so a bought theme remakes them. Every other icon is white and takes its colour from the theme.
/// With Teal's palette each is exactly its shipped file, which a test checks, so a restyled file must change here too.
/// </summary>
public static class BakedIcons
{
    private const string SwitchOn =
        """<svg xmlns="http://www.w3.org/2000/svg" width="88" height="52" viewBox="0 0 88 52"><rect x="3" y="3" width="82" height="46" rx="23" fill="{ink}" stroke="{ink}" stroke-width="5"/><circle cx="62" cy="26" r="15" fill="{card}"/></svg>""";

    private const string SwitchOff =
        """<svg xmlns="http://www.w3.org/2000/svg" width="88" height="52" viewBox="0 0 88 52"><rect x="3" y="3" width="82" height="46" rx="23" fill="{placeholder}" stroke="{disabled}" stroke-width="5"/><circle cx="26" cy="26" r="15" fill="{disabled}"/></svg>""";

    private const string SliderKnob =
        """<svg xmlns="http://www.w3.org/2000/svg" width="52" height="52" viewBox="0 0 52 52"><circle cx="26" cy="26" r="21" fill="{card}" stroke="{ink}" stroke-width="6"/></svg>""";

    /// <summary>The icon's SVG text in the palette's colours, by its file name, or null for an icon that isn't baked.</summary>
    public static string? For(string fileName, Palette palette)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(palette);
        var svg = fileName.StartsWith("switch-on-", StringComparison.Ordinal) ? SwitchOn
            : fileName.StartsWith("switch-off-", StringComparison.Ordinal) ? SwitchOff
            : fileName.StartsWith("slider-knob-", StringComparison.Ordinal) ? SliderKnob
            : null;
        return svg?
            .Replace("{ink}", palette.Ink.ToString(), StringComparison.Ordinal)
            .Replace("{card}", palette.Card.ToString(), StringComparison.Ordinal)
            .Replace("{placeholder}", palette.Placeholder.ToString(), StringComparison.Ordinal)
            .Replace("{disabled}", palette.Disabled.ToString(), StringComparison.Ordinal);
    }
}
