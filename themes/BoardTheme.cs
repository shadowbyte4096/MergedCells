using Godot;

namespace MergedCells;

[GlobalClass]
public partial class BoardTheme : Resource
{
    [Export] public Color Background { get; set; } = new("#0b0b0b");

    [Export] public Color Gap { get; set; } = new("#a3a3a3");

    // RectangleColoring needs at least five
    [Export] public Color[] Fills { get; set; } = [new("#141414"), new("#222222"), new("#303030"), new("#3e3e3e"), new("#4c4c4c")];

    [Export] public Color End { get; set; } = new("#22b14c");

    // A fraction of a square's side, so gaps scale with zoom
    [Export] public float GapFraction { get; set; } = 0.13f;

    // In viewport units (the 720x1280 base); narrower gaps aren't drawn
    [Export] public float MinGapPixels { get; set; } = 1.5f;
}
