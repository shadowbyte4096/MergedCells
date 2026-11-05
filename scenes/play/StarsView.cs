using Godot;

namespace MergedCells;

// Three stars, the earned ones filled, drawn as polygons in the UI theme's colours
public partial class StarsView : Control
{
    private const string ThemeType = "Play";

    private int earned;

    public int Earned
    {
        get => earned;
        set
        {
            earned = value;
            QueueRedraw();
        }
    }

    public override Vector2 _GetMinimumSize()
    {
        var size = GetThemeConstant("star_size", ThemeType);
        var gap = GetThemeConstant("star_gap", ThemeType);
        return new Vector2(3 * size + 2 * gap, size);
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
        var filled = GetThemeColor("star_filled", ThemeType);
        var empty = GetThemeColor("star_empty", ThemeType);
        // Theme constants are whole numbers, so the inner points' radius is a percentage of the outer
        var innerPercent = GetThemeConstant("star_inner_percent", ThemeType);
        var left = (Size.X - (3 * size + 2 * gap)) / 2;
        for (var i = 0; i < 3; i++)
        {
            var centre = new Vector2(left + i * (size + gap) + size / 2f, Size.Y / 2);
            DrawColoredPolygon(Star(centre, size / 2f, innerPercent), i < earned ? filled : empty);
        }
    }

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
