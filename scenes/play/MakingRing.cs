using System;
using Godot;

namespace MergedCells;

// New map's ring while the next map is made: an arc round the button's edge, growing clockwise from the top. Its colour and
// width are the UI theme's MakingRing entries
public partial class MakingRing : Control
{
    private const string Type = "MakingRing";
    // Points on a whole circle: a drawing detail, not a style
    private const int CirclePoints = 96;

    private double progress;

    public double Progress
    {
        set
        {
            progress = Math.Clamp(value, 0, 1);
            QueueRedraw();
        }
    }

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Notification(int what)
    {
        if (what == NotificationThemeChanged)
        {
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        if (progress <= 0)
        {
            return;
        }
        var width = GetThemeConstant("width", Type);
        var radius = Mathf.Min(Size.X, Size.Y) / 2 - width / 2f;
        var start = -Mathf.Pi / 2;
        var points = Math.Max(2, (int)Math.Ceiling(CirclePoints * progress));
        DrawArc(Size / 2, radius, start, start + (float)(Mathf.Tau * progress), points, GetThemeColor("ring", Type), width, true);
    }
}
