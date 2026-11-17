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

    // A trap's cross: its centre lines span the size on each axis, with round ends. Zoomed out it's bolder
    [Export] public Color Trap { get; set; } = new("#235e56");

    [Export] public float TrapSizeFraction { get; set; } = 0.4f;

    [Export] public float TrapStrokeFraction { get; set; } = 0.11f;

    [Export] public float TrapFlatSizeFraction { get; set; } = 0.68f;

    [Export] public float TrapFlatStrokeFraction { get; set; } = 0.22f;

    // A teleporter square shows its pair's letter, centred by its ink. Zoomed out it nearly fills the square
    [Export] public Color Teleporter { get; set; } = new("#235e56");

    [Export] public Font TeleporterFont { get; set; } = GD.Load<Font>("res://themes/fonts/Inter-ExtraBold.ttf");

    // The letter's font size, as a fraction of a square's side
    [Export] public float TeleporterLetterFraction { get; set; } = 0.66f;

    [Export] public float TeleporterFlatLetterFraction { get; set; } = 0.95f;

    // The level editor's highlight over the squares a stroke has crossed: the ink, see-through
    [Export] public Color EditHighlight { get; set; } = new(0.1372549f, 0.36862745f, 0.3372549f, 0.3f);

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
