using Godot;
using MergedCells.Core;

namespace MergedCells;

public partial class PlayerView : Node2D
{
    private PlayerLook look;
    private Cell cell;

    [Export]
    public PlayerLook Look
    {
        get => look;
        set
        {
            look = value;
            QueueRedraw();
        }
    }

    public Cell Cell
    {
        get => cell;
        set
        {
            cell = value;
            Position = GetParent<BoardView>().CellCentre(value);
        }
    }

    public override void _Draw()
    {
        if (look == null)
        {
            return;
        }
        PlayerShape.Draw(this, look, Vector2.Zero, BoardView.CellSize, flat: GetParent<BoardView>().Gap == 0);
    }
}
