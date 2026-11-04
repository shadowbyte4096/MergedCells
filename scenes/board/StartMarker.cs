using Godot;
using MergedCells.Core;

namespace MergedCells;

public partial class StartMarker : Node2D
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

        // The stroke is centred on the square's edge, so the square is shrunk by half a stroke on each side
        var width = look.StartOutlineWidth * BoardView.CellSize;
        var side = BoardView.CellSize - GetParent<BoardView>().Gap - width;
        DrawRect(new Rect2(-side / 2, -side / 2, side, side), look.StartOutline, filled: false, width: width);
    }
}
