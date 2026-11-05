using Godot;

namespace MergedCells;

public partial class BoardCamera : Camera2D
{
    private Rect2 area;
    private float topInset;
    private float bottomInset;

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

    // Viewport units kept free at the top and bottom, such as the HUD's bars
    public void SetInsets(float top, float bottom)
    {
        topInset = top;
        bottomInset = bottom;
        Refit();
    }

    private void Refit()
    {
        if (area.Size.X <= 0 || area.Size.Y <= 0)
        {
            return;
        }
        var size = GetViewportRect().Size;
        var zoom = Mathf.Min(
            (size.X - 2 * Margin) / area.Size.X,
            (size.Y - 2 * Margin - topInset - bottomInset) / area.Size.Y);
        if (zoom <= 0)
        {
            return;
        }
        // The camera centres on the viewport, so shift it to centre the area in the band between the insets
        Position = area.GetCenter() - new Vector2(0, (topInset - bottomInset) / 2 / zoom);
        Zoom = new Vector2(zoom, zoom);
    }
}
