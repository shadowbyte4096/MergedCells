using System;
using System.Collections.Generic;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// One finger (or the left mouse button) edits the board; Core's EditStroke decides tap or drag and drops a stroke a second
// finger joins. Touch-downs and lifts are read unhandled, so a press on a button never starts a stroke; drags in _Input, so
// a stroke that crosses a button carries on. Touches are never marked handled: the camera reads them for two fingers.
public partial class EditorInput : Node
{
    // Mouse and touch never mix on one device, so the mouse's finger just needs to differ from touch indices
    private const int MouseFinger = -1;

    private EditStroke stroke;
    private BoardView board;
    private bool enabled = true;

    public event Action<IReadOnlyList<Cell>> StrokeMoved;

    public event Action<StrokeEnd> StrokeEnded;

    public event Action StrokeCancelled;

    // The band the board shows in, between the status line and the bottom bar, in viewport units. The bars let presses
    // through, so a press on one is off the board here: the squares under it can't be seen
    public float BandTop { get; set; }

    public float BandBottom { get; set; } = float.MaxValue;

    // Off while a sheet is open; turning it off drops any stroke
    public bool Enabled
    {
        get => enabled;
        set
        {
            enabled = value;
            if (!value)
            {
                Reset();
            }
        }
    }

    public override void _Ready()
    {
        stroke = new EditStroke(GameConfigFile.Current.Camera.GestureSlop);
        board = GetNode<BoardView>("../Board");
    }

    public void Reset()
    {
        var wasActive = stroke?.IsActive ?? false;
        stroke?.Reset();
        if (wasActive)
        {
            StrokeCancelled?.Invoke();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!enabled || board.Map == null)
        {
            return;
        }
        switch (@event)
        {
            case InputEventScreenTouch { Pressed: true } touch:
                Press(touch.Index, touch.Position);
                break;
            case InputEventScreenTouch { Canceled: true } touch:
                // A cancelled touch is a release that applies nothing
                if (stroke.Release(touch.Index) is not null)
                {
                    StrokeCancelled?.Invoke();
                }
                break;
            case InputEventScreenTouch touch:
                Release(touch.Index);
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } press
                when press.Device != InputEvent.DeviceIdEmulation:
                Press(MouseFinger, press.Position);
                break;
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (!enabled || board.Map == null)
        {
            return;
        }
        switch (@event)
        {
            case InputEventScreenDrag drag:
                Drag(drag.Index, drag.Position);
                break;
            case InputEventMouseMotion motion when motion.Device != InputEvent.DeviceIdEmulation:
                Drag(MouseFinger, motion.Position);
                break;
            case InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left } release
                when release.Device != InputEvent.DeviceIdEmulation:
                Release(MouseFinger);
                break;
        }
    }

    public override void _Notification(int what)
    {
        // The OS may never send the releases for touches held while the app loses focus
        if (what == NotificationApplicationFocusOut || what == NotificationApplicationPaused)
        {
            Reset();
        }
    }

    private void Press(int finger, Vector2 position)
    {
        var wasActive = stroke.IsActive;
        stroke.Press(finger, position.X, position.Y, CellAt(position));
        // A second finger, or a lost release, drops the stroke under way
        if (wasActive)
        {
            StrokeCancelled?.Invoke();
        }
        if (stroke.IsActive)
        {
            StrokeMoved?.Invoke(stroke.Cells);
        }
    }

    private void Drag(int finger, Vector2 position)
    {
        if (stroke.Drag(finger, position.X, position.Y, CellAt(position)).Count > 0)
        {
            StrokeMoved?.Invoke(stroke.Cells);
        }
    }

    private void Release(int finger)
    {
        if (stroke.Release(finger) is { } end)
        {
            StrokeEnded?.Invoke(end);
        }
    }

    // The square under a viewport position, or null off the board
    private Cell? CellAt(Vector2 position)
    {
        if (position.Y < BandTop || position.Y > BandBottom)
        {
            return null;
        }
        var local = board.GetGlobalTransformWithCanvas().AffineInverse() * position;
        var cell = new Cell(Mathf.FloorToInt(local.Y / BoardView.CellSize), Mathf.FloorToInt(local.X / BoardView.CellSize));
        return board.Map.Contains(cell) ? cell : null;
    }
}
