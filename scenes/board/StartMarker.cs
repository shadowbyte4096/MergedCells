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
        if (look == null || GetParent<BoardView>().Gap == 0)
        {
            return;
        }

        var width = look.StartOutlineWidth * BoardView.CellSize;
        var dash = look.StartDashFraction * BoardView.CellSize;
        var half = look.StartSideFraction * BoardView.CellSize / 2;
        Vector2[] corners = [new(-half, -half), new(half, -half), new(half, half), new(-half, half)];
        for (var i = 0; i < corners.Length; i++)
        {
            DrawDashedLine(corners[i], corners[(i + 1) % corners.Length], look.StartOutline, width, dash, antialiased: true);
        }
    }
}
