using Godot;

namespace MergedCells;

public partial class BoardCamera : Camera2D
{
    private Rect2 area;

    // In viewport units, on every side
    [Export] public float Margin { get; set; } = 24f;

    public override void _Ready()
    {
        GetViewport().SizeChanged += Refit;
    }

    // Centres an area in board space and zooms so it fills the viewport, keeping it fitted when the window resizes
    public void Fit(Rect2 area)
    {
        this.area = area;
        Refit();
    }

    private void Refit()
    {
        if (area.Size.X <= 0 || area.Size.Y <= 0)
        {
            return;
        }
        var size = GetViewportRect().Size;
        var zoom = Mathf.Min((size.X - 2 * Margin) / area.Size.X, (size.Y - 2 * Margin) / area.Size.Y);
        Position = area.GetCenter();
        Zoom = new Vector2(zoom, zoom);
    }
}
