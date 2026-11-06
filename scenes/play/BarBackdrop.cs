using Godot;

namespace MergedCells;

// Backs the HUD's bars in the board's background colour, fading to clear toward the board, so a board scrolled or
// zoomed past the whole map doesn't show through the text and buttons
public partial class BarBackdrop : Control
{
    private Color color;
    private float topEdge;
    private float bottomEdge;

    public Color Color
    {
        set
        {
            color = value;
            QueueRedraw();
        }
    }

    // In this control's units: the top bar's bottom edge and the bottom bar's top edge
    public void SetEdges(float topEdge, float bottomEdge)
    {
        this.topEdge = topEdge;
        this.bottomEdge = bottomEdge;
        QueueRedraw();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationThemeChanged || what == NotificationResized)
        {
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        var fade = GetThemeConstant("bar_fade", "Play");
        var width = Size.X;
        var clear = color with { A = 0 };

        DrawRect(new Rect2(0, 0, width, topEdge), color);
        DrawPolygon(
            [new(0, topEdge), new(width, topEdge), new(width, topEdge + fade), new(0, topEdge + fade)],
            [color, color, clear, clear]);

        DrawRect(new Rect2(0, bottomEdge, width, Size.Y - bottomEdge), color);
        DrawPolygon(
            [new(0, bottomEdge - fade), new(width, bottomEdge - fade), new(width, bottomEdge), new(0, bottomEdge)],
            [clear, clear, color, color]);
    }
}
