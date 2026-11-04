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

        var tile = BoardView.CellSize - GetParent<BoardView>().Gap - 2 * look.Inset * BoardView.CellSize;
        var outer = tile / 2;
        var inner = outer - look.Bevel * tile;
        DrawColoredPolygon(new Vector2[] { new(-outer, -outer), new(outer, -outer), new(inner, -inner), new(-inner, -inner) }, look.Top);
        DrawColoredPolygon(new Vector2[] { new(outer, -outer), new(outer, outer), new(inner, inner), new(inner, -inner) }, look.Right);
        DrawColoredPolygon(new Vector2[] { new(outer, outer), new(-outer, outer), new(-inner, inner), new(inner, inner) }, look.Bottom);
        DrawColoredPolygon(new Vector2[] { new(-outer, outer), new(-outer, -outer), new(-inner, -inner), new(-inner, inner) }, look.Left);
        DrawRect(new Rect2(-inner, -inner, 2 * inner, 2 * inner), look.Face);
    }
}
