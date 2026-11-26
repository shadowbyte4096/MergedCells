namespace MergedCells.Core;

/// <summary>
/// One mode (light or dark) of a theme: the menu roles, each one colour wherever the default menu theme uses it, and
/// one colour per board piece. <see cref="Chart"/> is text on the placeholder panel; <see cref="Pressed"/> is a pressed
/// or hovered button's fill; <see cref="New"/> is the new-star colour, the results card's coins line and coin.
/// </summary>
public sealed record Palette(
    Rgb Background,
    Rgb Ink,
    Rgb Card,
    Rgb Muted,
    Rgb Chart,
    Rgb Placeholder,
    Rgb Disabled,
    Rgb Pressed,
    Rgb New,
    IReadOnlyList<Rgb> Fills,
    Rgb Outline,
    Rgb End,
    Rgb Wall,
    Rgb WallMiddle,
    Rgb Trap,
    Rgb Teleporter,
    Rgb Player)
{
    public const int FillCount = 5;
}
