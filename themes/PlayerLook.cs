using Godot;

namespace MergedCells;

[GlobalClass]
public partial class PlayerLook : Resource
{
    [Export] public Color Face { get; set; } = new("#00a2e8");

    [Export] public Color Top { get; set; } = new("#4dbeef");

    [Export] public Color Right { get; set; } = new("#26b0eb");

    [Export] public Color Left { get; set; } = new("#0085be");

    [Export] public Color Bottom { get; set; } = new("#005c84");

    // From the cell's fill edge, as a fraction of a square's side
    [Export] public float Inset { get; set; } = 0.14f;

    // As a fraction of the tile's side
    [Export] public float Bevel { get; set; } = 0.26f;

    [Export] public Color StartOutline { get; set; } = new("#00a2e8");

    // As a fraction of a square's side
    [Export] public float StartOutlineWidth { get; set; } = 0.09f;
}
