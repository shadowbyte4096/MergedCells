using Godot;

namespace MergedCells;

[GlobalClass]
public partial class PlayerLook : Resource
{
    [Export] public Color Color { get; set; } = new("#235e56");

    // Fractions of a square's side
    [Export] public float SideFraction { get; set; } = 0.56f;

    [Export] public float StrokeFraction { get; set; } = 0.1f;

    [Export] public float CornerFraction { get; set; } = 0.1f;

    // A bought look's shape (chunk 28): solid rather than an outline, and turned clockwise, as a diamond is
    [Export] public bool Filled { get; set; }

    [Export] public float TurnDegrees { get; set; }

    [Export] public float TrailAlpha { get; set; } = 0.22f;

    [Export] public float TrailFadeSeconds { get; set; } = 0.3f;

    [Export] public Color StartOutline { get; set; } = new(0.13725491f, 0.36862746f, 0.3372549f, 0.55f);

    // Fractions of a square's side
    [Export] public float StartSideFraction { get; set; } = 0.42f;

    [Export] public float StartOutlineWidth { get; set; } = 0.05f;

    [Export] public float StartDashFraction { get; set; } = 0.07f;
}
