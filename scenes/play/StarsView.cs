using System.Collections.Generic;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// A row of stars drawn as polygons in the UI theme's colours, each in its look: earned, new, ghost (a dashed outline) or
// empty. ThemeType picks the theme entries, so the results card, level tiles and star totals size them differently.
public partial class StarsView : Control
{
    private IReadOnlyList<StarLook> looks = StarLooks.For(0, 0);

    [Export] public string ThemeType { get; set; } = "Play";

    public IReadOnlyList<StarLook> Looks
    {
        set
        {
            looks = value;
            UpdateMinimumSize();
            QueueRedraw();
        }
    }

    public override Vector2 _GetMinimumSize()
    {
        var size = GetThemeConstant("star_size", ThemeType);
        return new Vector2(RowWidth(size, GetThemeConstant("star_gap", ThemeType)), size);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationThemeChanged)
        {
            UpdateMinimumSize();
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        var size = GetThemeConstant("star_size", ThemeType);
        var gap = GetThemeConstant("star_gap", ThemeType);
        // Theme constants are whole numbers, so the inner points' radius is a percentage of the outer
        var innerPercent = GetThemeConstant("star_inner_percent", ThemeType);
        var left = (Size.X - RowWidth(size, gap)) / 2;
        for (var i = 0; i < looks.Count; i++)
        {
            var centre = new Vector2(left + i * (size + gap) + size / 2f, Size.Y / 2);
            var points = Star(centre, size / 2f, innerPercent);
            switch (looks[i])
            {
                case StarLook.Ghost:
                    var color = GetThemeColor("star_ghost", ThemeType);
                    var width = GetThemeConstant("star_ghost_width", ThemeType);
                    var dash = GetThemeConstant("star_ghost_dash", ThemeType);
                    for (var p = 0; p < points.Length; p++)
                    {
                        DrawDashedLine(points[p], points[(p + 1) % points.Length], color, width, dash, antialiased: true);
                    }
                    break;
                case StarLook.Earned:
                    DrawColoredPolygon(points, GetThemeColor("star_filled", ThemeType));
                    break;
                case StarLook.New:
                    DrawColoredPolygon(points, GetThemeColor("star_new", ThemeType));
                    break;
                default:
                    DrawColoredPolygon(points, GetThemeColor("star_empty", ThemeType));
                    break;
            }
        }
    }

    private int RowWidth(int size, int gap) => looks.Count == 0 ? 0 : looks.Count * size + (looks.Count - 1) * gap;

    private static Vector2[] Star(Vector2 centre, float outer, int innerPercent)
    {
        var inner = outer * innerPercent / 100f;
        var points = new Vector2[10];
        for (var i = 0; i < 10; i++)
        {
            var angle = -Mathf.Pi / 2 + i * Mathf.Pi / 5;
            points[i] = centre + Vector2.FromAngle(angle) * (i % 2 == 0 ? outer : inner);
        }
        return points;
    }
}
