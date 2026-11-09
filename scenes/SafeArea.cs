using System;
using Godot;

namespace MergedCells;

public static class SafeArea
{
    // In viewport units: how far the camera hole and gesture bar reach into the window. Desktop windows aren't cut into,
    // and there the safe area is only the screen's usable rect.
    public static (float Top, float Bottom) Insets(Node node)
    {
        if (!OS.HasFeature("mobile"))
        {
            return (0, 0);
        }
        var safe = DisplayServer.GetDisplaySafeArea();
        var window = new Rect2I(DisplayServer.WindowGetPosition(), DisplayServer.WindowGetSize());
        var scale = node.GetTree().Root.GetFinalTransform().Scale.Y;
        var top = Math.Max(0, safe.Position.Y - window.Position.Y) / scale;
        var bottom = Math.Max(0, window.End.Y - safe.End.Y) / scale;
        GD.Print($"Safe area: top {top:0.#}, bottom {bottom:0.#}");
        return (top, bottom);
    }
}
