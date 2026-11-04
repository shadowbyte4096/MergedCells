using System;
using Godot;
using MergedCells.Core;

namespace MergedCells;

public partial class MoveInput : Node
{
    private static readonly (StringName Action, Direction Direction)[] Actions =
    [
        ("move_up", Direction.Up),
        ("move_down", Direction.Down),
        ("move_left", Direction.Left),
        ("move_right", Direction.Right),
    ];

    private SwipeDetector swipes;

    public event Action<Direction> MoveRequested;

    public override void _Ready()
    {
        swipes = new SwipeDetector(GameConfigFile.Current.Input.SwipeThreshold);
    }

    // Unhandled, so menus and HUD controls see events first. Touches are never marked handled,
    // because the camera reads the same touches for its two-finger gestures.
    public override void _UnhandledInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventKey key:
                HandleKey(key);
                break;
            case InputEventScreenTouch touch:
                if (touch.Pressed)
                {
                    swipes.Press(touch.Index, touch.Position.X, touch.Position.Y);
                }
                else
                {
                    swipes.Release(touch.Index);
                }
                break;
            case InputEventScreenDrag drag:
                if (swipes.Drag(drag.Index, drag.Position.X, drag.Position.Y) is { } direction)
                {
                    MoveRequested?.Invoke(direction);
                }
                break;
        }
    }

    public override void _Notification(int what)
    {
        // The OS may never send the releases for touches held while the app loses focus
        if (what == NotificationApplicationFocusOut || what == NotificationApplicationPaused)
        {
            swipes?.Reset();
        }
    }

    private void HandleKey(InputEventKey key)
    {
        if (key.Echo)
        {
            return;
        }
        foreach (var (action, direction) in Actions)
        {
            if (key.IsActionPressed(action))
            {
                GetViewport().SetInputAsHandled();
                MoveRequested?.Invoke(direction);
                return;
            }
        }
    }
}
