using System;
using Godot;
using MergedCells.Core;
using Numerics = System.Numerics;

namespace MergedCells;

// Applies Core's CameraSpace to the Camera2D: big levels open with a zoom in on the start, the camera glides after
// the player, and a pinch, the mouse wheel or the zoom keys zoom the board.
public partial class BoardCamera : Camera2D
{
    private static readonly StringName ZoomInAction = "zoom_in";
    private static readonly StringName ZoomOutAction = "zoom_out";

    private readonly PinchTracker pinch = new();
    private CameraConfig config;
    private Rect2 bounds;
    private float topInset;
    private float bottomInset;
    private Vector2 band;
    private CameraSpace space;
    private CameraView view;
    private float playerZoom;
    private CameraGlide glide;
    private double glideElapsed;
    private bool opening;

    // In viewport units, round the whole map
    [Export] public float Margin { get; set; } = 24f;

    // Off while the results are up, so the zoom keys do nothing, like the move keys
    public bool InputEnabled { get; set; } = true;

    public override void _Ready()
    {
        config = GameConfigFile.Current.Camera;
        GetViewport().SizeChanged += Rebuild;
    }

    // Viewport units kept free at the top and bottom, such as the HUD's bars
    public void SetInsets(float top, float bottom)
    {
        topInset = top;
        bottomInset = bottom;
        Rebuild();
    }

    // A new level, with the board's bounds and the start square's centre in board units
    public void Open(Rect2 bounds, Vector2 start)
    {
        this.bounds = bounds;
        glide = null;
        opening = false;
        Rebuild();
        if (space == null)
        {
            return;
        }

        view = space.Whole;
        playerZoom = space.ReadableZoom;
        if (space.OpensWhole)
        {
            GD.Print("Camera: whole map");
        }
        else
        {
            Begin(space.PlanOpening(ToCore(start)));
            opening = true;
            GD.Print($"Camera: opening zoom, {SquaresAcross(space.ReadableZoom):0} squares across");
        }
        Apply();
    }

    // A slide has started, between two square centres in board units
    public void Follow(Vector2 from, Vector2 to)
    {
        if (space == null)
        {
            return;
        }
        FinishOpening();
        if (space.PlanFollow(view, Heading, playerZoom, ToCore(from), ToCore(to)) is { } plan)
        {
            Begin(plan);
        }
    }

    // The player snapped to a square (undo, restart), in board units
    public void Recentre(Vector2 at)
    {
        if (space == null)
        {
            return;
        }
        FinishOpening();
        if (space.PlanRecentre(view, Heading, playerZoom, ToCore(at)) is { } plan)
        {
            Begin(plan);
        }
    }

    // Runs before BoardView's _Process (tree order), which reads the canvas transform this sets
    public override void _Process(double delta)
    {
        if (glide == null || space == null)
        {
            return;
        }
        glideElapsed += delta;
        view = space.Clamp(glide.Sample(glideElapsed));
        if (glide.IsDone(glideElapsed))
        {
            glide = null;
            opening = false;
        }
        Apply();
    }

    // Touches are never marked handled: MoveInput reads the same ones for swipes
    public override void _UnhandledInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventScreenTouch touch:
                HandleTouch(touch);
                break;
            case InputEventScreenDrag drag:
                if (pinch.Drag(drag.Index, drag.Position.X, drag.Position.Y) is { } step)
                {
                    ZoomBy(step.Scale, new Vector2(step.Focal.X, step.Focal.Y));
                }
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown } wheel:
                if (InputEnabled && space != null)
                {
                    // Smooth-scrolling wheels and trackpads report part of a notch; 0 means the platform doesn't say
                    var notches = wheel.Factor == 0 ? 1 : wheel.Factor;
                    ZoomBy(space.ZoomFactor(wheel.ButtonIndex == MouseButton.WheelUp ? notches : -notches), wheel.Position);
                    GetViewport().SetInputAsHandled();
                    LogZoom();
                }
                break;
            case InputEventKey key:
                HandleKey(key);
                break;
        }
    }

    public override void _Notification(int what)
    {
        // The OS may never send the releases for touches held while the app loses focus
        if (what == NotificationApplicationFocusOut || what == NotificationApplicationPaused)
        {
            pinch.Reset();
        }
    }

    private CameraView Heading => glide == null ? view : space.Clamp(glide.To);

    private void HandleTouch(InputEventScreenTouch touch)
    {
        var wasPinching = pinch.IsPinching;
        if (touch.Pressed)
        {
            pinch.Press(touch.Index, touch.Position.X, touch.Position.Y);
        }
        else
        {
            pinch.Release(touch.Index);
        }

        if (!wasPinching && pinch.IsPinching && space != null)
        {
            FinishOpening();
            glide = null;
        }
        else if (wasPinching && !pinch.IsPinching)
        {
            LogZoom();
        }
    }

    private void HandleKey(InputEventKey key)
    {
        if (!InputEnabled || space == null)
        {
            return;
        }
        var zoomIn = key.IsActionPressed(ZoomInAction, allowEcho: true);
        if (!zoomIn && !key.IsActionPressed(ZoomOutAction, allowEcho: true))
        {
            return;
        }
        GetViewport().SetInputAsHandled();
        var bandCentre = new Vector2(band.X / 2, topInset + band.Y / 2);
        ZoomBy(space.ZoomFactor(zoomIn ? 1 : -1), bandCentre);
        LogZoom();
    }

    private void ZoomBy(float factor, Vector2 viewportPoint)
    {
        if (space == null || !InputEnabled)
        {
            return;
        }
        FinishOpening();
        glide = null;
        view = space.ZoomAt(view, factor, ToCore(viewportPoint - new Vector2(0, topInset)));
        playerZoom = view.Zoom;
        Apply();
    }

    private void Begin(CameraGlide plan)
    {
        glide = plan;
        glideElapsed = 0;
    }

    private void FinishOpening()
    {
        if (!opening)
        {
            return;
        }
        view = space.Clamp(glide.To);
        glide = null;
        opening = false;
        Apply();
    }

    // The board, the window or the bars changed. Never snaps the view: a glide carries on, clamped to the new limits
    private void Rebuild()
    {
        if (config == null || bounds.Size.X <= 0 || bounds.Size.Y <= 0)
        {
            return;
        }
        var newBand = GetViewportRect().Size - new Vector2(0, topInset + bottomInset);
        // A minimised window can shrink the band to nothing; the previous space stays until it's back
        if (newBand.X <= 2 * Margin || newBand.Y <= 2 * Margin)
        {
            return;
        }

        var previous = space;
        band = newBand;
        space = new CameraSpace(
            new BoardRect(bounds.Position.X, bounds.Position.Y, bounds.Size.X, bounds.Size.Y),
            ToCore(band),
            Margin,
            BoardView.CellSize,
            config);
        playerZoom = space.Resized(new CameraView(view.Focus, playerZoom), previous).Zoom;
        view = space.Resized(view, previous);
        if (glide != null)
        {
            // A glide heading for the old whole map should end on the new one
            glide = new CameraGlide(glide.From, space.Resized(glide.To, previous), glide.HalfwayZoom, glide.Seconds, glide.Delay);
        }
        Apply();
    }

    private void Apply()
    {
        if (space == null)
        {
            return;
        }
        // The camera centres on the viewport, so shift it to put the focus in the middle of the band between the insets
        Position = new Vector2(view.Focus.X, view.Focus.Y) - new Vector2(0, (topInset - bottomInset) / 2 / view.Zoom);
        Zoom = new Vector2(view.Zoom, view.Zoom);
        ForceUpdateScroll();
    }

    private void LogZoom() => GD.Print($"Zoom: {SquaresAcross(view.Zoom):0.0} squares across");

    private float SquaresAcross(float zoom) => band.X / (zoom * BoardView.CellSize);

    private static Numerics.Vector2 ToCore(Vector2 vector) => new(vector.X, vector.Y);
}
