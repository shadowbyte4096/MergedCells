using Godot;
using MergedCells.Core;

namespace MergedCells;

public partial class PlayerView : Node2D
{
    private PlayerLook look;
    private StyleBoxFlat outline;
    private Cell cell;

    [Export]
    public PlayerLook Look
    {
        get => look;
        set
        {
            look = value;
            if (look != null)
            {
                outline = new StyleBoxFlat { DrawCenter = false, BorderColor = look.Color, AntiAliasing = true };
                outline.SetBorderWidthAll(Mathf.RoundToInt(look.StrokeFraction * BoardView.CellSize));
                outline.SetCornerRadiusAll(Mathf.RoundToInt(look.CornerFraction * BoardView.CellSize));
            }
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

        var side = look.SideFraction * BoardView.CellSize;
        var square = new Rect2(-side / 2, -side / 2, side, side);
        // Zoomed out, a hollow square would vanish
        if (GetParent<BoardView>().Gap > 0)
        {
            DrawStyleBox(outline, square);
        }
        else
        {
            DrawRect(square, look.Color);
        }
    }
}
