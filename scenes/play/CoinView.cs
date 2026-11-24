using Godot;

namespace MergedCells;

// A coin drawn as three discs in the UI theme's colours: the coin, a ring cut out of it in the colour behind it, and a
// solid centre. ThemeType picks the entries, so the mode select's balance and the results card size it differently.
public partial class CoinView : Control
{
    [Export] public string ThemeType { get; set; } = "TotalCoin";

    public override Vector2 _GetMinimumSize()
    {
        var size = GetThemeConstant("size", ThemeType);
        return new Vector2(size, size);
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
        var radius = GetThemeConstant("size", ThemeType) / 2f;
        var coin = GetThemeColor("coin", ThemeType);
        var centre = Size / 2;
        DrawCircle(centre, radius, coin, antialiased: true);
        // Theme constants are whole numbers, so the ring and centre are percentages of the coin
        DrawCircle(centre, radius * GetThemeConstant("ring_percent", ThemeType) / 100f, GetThemeColor("ring", ThemeType), antialiased: true);
        DrawCircle(centre, radius * GetThemeConstant("centre_percent", ThemeType) / 100f, coin, antialiased: true);
    }
}
