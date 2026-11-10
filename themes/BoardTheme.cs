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

    // A wall's rings step from Wall at its edge to WallMiddle
    [Export] public Color Wall { get; set; } = new("#235e56");

    [Export] public Color WallMiddle { get; set; } = new("#a9c5c0");

    [Export] public int WallRings { get; set; } = 4;

    // A sticky square's diagonal stripes, drawn over StickyTint
    [Export] public Color Sticky { get; set; } = new("#235e56");

    [Export] public Color StickyTint { get; set; } = new("#f2f5f5");

    // In from the grid line on sides that don't join another sticky square
    [Export] public float StickyInsetFraction { get; set; } = 0.14f;

    [Export] public float StickyCornerFraction { get; set; } = 0.12f;

    // Stripes crossing each side of a square: a whole number, so they run unbroken across joined squares
    [Export] public int StickyStripes { get; set; } = 4;

    // Measured across a stripe
    [Export] public float StickyStripeFraction { get; set; } = 0.085f;

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
    [Export] public float MinGapPixels { get; set; } = 3f;
}
