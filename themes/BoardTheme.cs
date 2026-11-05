using Godot;

namespace MergedCells;

[GlobalClass]
public partial class BoardTheme : Resource
{
    [Export] public Color Background { get; set; } = new("#e3f1ed");

    [Export] public Color Outline { get; set; } = new("#235e56");

    // RectangleColoring needs at least five
    [Export] public Color[] Fills { get; set; } = [new("#f2f5f5"), new("#e0e8e7"), new("#cfdcda"), new("#bdcfcc"), new("#abc2bf")];

    // The end's dots, and the whole end square when zoomed out
    [Export] public Color End { get; set; } = new("#235e56");

    // Fractions of a square's side, so everything scales with zoom
    [Export] public float GapFraction { get; set; } = 0.16f;

    [Export] public float OutlineFraction { get; set; } = 0.07f;

    [Export] public float CornerFraction { get; set; } = 0.2f;

    [Export] public float EndDotFraction { get; set; } = 0.13f;

    // Between dot centres
    [Export] public float EndDotSpacing { get; set; } = 0.2f;

    // A fraction of a dot's side, not a square's
    [Export] public float EndDotCornerFraction { get; set; } = 0.3f;

    // In viewport units (the 720x1280 base). Narrower gaps aren't drawn, and the board switches to flat fills
    [Export] public float MinGapPixels { get; set; } = 1.5f;
}
