using System;
using System.Collections.Generic;
using Godot;

namespace MergedCells;

// A panel with a dashed, rounded outline (StyleBoxFlat has no dashes), for the next locked chapter's card. Its dash,
// width, corner and colour come from the LockedCard theme entries; its content margins from the theme's panel style.
public partial class DashedPanel : PanelContainer
{
    private const string ThemeType = "LockedCard";
    private const int ArcSegments = 8;

    public override void _Notification(int what)
    {
        if (what == NotificationResized || what == NotificationThemeChanged)
        {
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        var width = GetThemeConstant("width", ThemeType);
        var dash = Math.Max(1, GetThemeConstant("dash", ThemeType));
        var color = GetThemeColor("color", ThemeType);
        var rect = new Rect2(Vector2.Zero, Size).Grow(-width / 2f);
        var radius = Math.Min(GetThemeConstant("corner", ThemeType), Math.Min(rect.Size.X, rect.Size.Y) / 2);
        var outline = Outline(rect, radius);

        // Walk the outline once, drawing every other dash-length stretch as one polyline so dashes bend round corners
        var on = true;
        var along = 0f;
        var stroke = new List<Vector2> { outline[0] };
        for (var i = 0; i + 1 < outline.Count; i++)
        {
            var from = outline[i];
            var to = outline[i + 1];
            var length = from.DistanceTo(to);
            var travelled = 0f;
            while (travelled < length)
            {
                var step = Math.Min(dash - along, length - travelled);
                travelled += step;
                along += step;
                var point = from.Lerp(to, travelled / length);
                if (on)
                {
                    stroke.Add(point);
                }
                if (along >= dash)
                {
                    if (on && stroke.Count > 1)
                    {
                        DrawPolyline(stroke.ToArray(), color, width, antialiased: true);
                    }
                    on = !on;
                    along = 0;
                    stroke = [point];
                }
            }
        }
        if (on && stroke.Count > 1)
        {
            DrawPolyline(stroke.ToArray(), color, width, antialiased: true);
        }
    }

    // Clockwise from the end of the top-left corner, closed
    private static List<Vector2> Outline(Rect2 rect, float radius)
    {
        var points = new List<Vector2>();
        var corners = new[]
        {
            (Centre: new Vector2(rect.End.X - radius, rect.Position.Y + radius), Start: -Mathf.Pi / 2),
            (Centre: new Vector2(rect.End.X - radius, rect.End.Y - radius), Start: 0f),
            (Centre: new Vector2(rect.Position.X + radius, rect.End.Y - radius), Start: Mathf.Pi / 2),
            (Centre: new Vector2(rect.Position.X + radius, rect.Position.Y + radius), Start: Mathf.Pi),
        };
        points.Add(new Vector2(rect.Position.X + radius, rect.Position.Y));
        foreach (var (centre, start) in corners)
        {
            for (var s = 0; s <= ArcSegments; s++)
            {
                points.Add(centre + Vector2.FromAngle(start + s * Mathf.Pi / 2 / ArcSegments) * radius);
            }
        }
        points.Add(points[0]);
        return points;
    }
}
