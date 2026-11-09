using Godot;

namespace MergedCells;

// One dot per open chapter on the level select, the current one solid. Sizes and colours come from the PageDots theme entries.
public partial class PageDots : Control
{
    private const string ThemeType = "PageDots";

    private int count;
    private int current;

    public void Set(int dots, int selected)
    {
        count = dots;
        current = selected;
        UpdateMinimumSize();
        QueueRedraw();
    }

    public override Vector2 _GetMinimumSize()
    {
        var size = GetThemeConstant("dot_size", ThemeType);
        return new Vector2(count == 0 ? 0 : count * size + (count - 1) * GetThemeConstant("dot_gap", ThemeType), size);
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
        var size = GetThemeConstant("dot_size", ThemeType);
        var gap = GetThemeConstant("dot_gap", ThemeType);
        var left = (Size.X - _GetMinimumSize().X) / 2;
        for (var i = 0; i < count; i++)
        {
            var centre = new Vector2(left + i * (size + gap) + size / 2f, Size.Y / 2);
            DrawCircle(centre, size / 2f, GetThemeColor(i == current ? "dot_on" : "dot_off", ThemeType), antialiased: true);
        }
    }
}
