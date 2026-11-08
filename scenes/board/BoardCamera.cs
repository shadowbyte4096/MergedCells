using System;
using Godot;
using MergedCells.Core;
using Numerics = System.Numerics;

namespace MergedCells;

// Applies Core's CameraSpace to the Camera2D: big levels open with a zoom in on the start, the camera glides after
// the player, two fingers pan and pinch, a left-mouse drag pans, and the mouse wheel or the zoom keys zoom the board.
public partial class BoardCamera : Camera2D
{
    private static readonly StringName ZoomInAction = "zoom_in";
    private static readonly StringName ZoomOutAction = "zoom_out";

    private PinchTracker pinch;
    private DragTracker drag;
    // A two-finger gesture or mouse drag has moved the view since it began
    private bool gestureMoved;
    // This frame's gesture steps, joined, applied once in _Process: one finger's step at a time would wobble the zoom
    private GestureStep? pendingStep;
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
        pinch = new PinchTracker(config.GestureSlop);
        drag = new DragTracker(config.GestureSlop);
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

    // A slide has started, between two square centres in board units, lasting slideSeconds. A gesture that has moved the
    // view holds the camera, so a move made during it (a key, or a remembered move starting) plans no glide
    public void Follow(Vector2 from, Vector2 to, double slideSeconds)
    {
        if (space == null || gestureMoved)
        {
            return;
        }
        FinishOpening();
        if (space.PlanFollow(view, Heading, playerZoom, ToCore(from), ToCore(to), slideSeconds) is { } plan)
        {
            Begin(plan);
        }
    }

    // The player snapped to a square (undo, restart), in board units. Held by a moving gesture, as Follow is
    public void Recentre(Vector2 at)
    {
        if (space == null || gestureMoved)
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
        if (pinch?.TakeStep() is { } step)
        {
            Queue(step);
        }
        FlushGesture();
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
            case InputEventScreenDrag screenDrag:
                pinch.Drag(screenDrag.Index, screenDrag.Position.X, screenDrag.Position.Y);
                break;
            // Android also sends a mouse event for every touch; those must never pan
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } press
                when press.Device != InputEvent.DeviceIdEmulation:
                drag.Press(press.Position.X, press.Position.Y);
                GetViewport().SetInputAsHandled();
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

    // A drag's motion and release are read here, before the HUD, so a drag carries on over a button and its release
    // there still ends it. The press stays unhandled-only, so a press on a button never starts one.
    public override void _Input(InputEvent @event)
    {
        if (drag is not { IsDown: true } || @event.Device == InputEvent.DeviceIdEmulation)
        {
            return;
        }
        switch (@event)
        {
            case InputEventMouseMotion motion:
                if (drag.Move(motion.Position.X, motion.Position.Y) is { } step)
                {
                    Queue(step);
                }
                break;
            case InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left }:
                drag.Release();
                FlushGesture();
                EndGesture();
                break;
        }
    }

    public override void _Notification(int what)
    {
        // The OS may never send the releases for touches or buttons held while the app loses focus
        if (what == NotificationApplicationFocusOut || what == NotificationApplicationPaused)
        {
            pinch?.Reset();
            drag?.Release();
            pendingStep = null;
            gestureMoved = false;
        }
    }

    private CameraView Heading => glide == null ? view : space.Clamp(glide.To);

    private void HandleTouch(InputEventScreenTouch touch)
    {
        var wasPinching = pinch.IsPinching;
        // This frame's drags so far count before a finger lifts
        if (wasPinching && pinch.TakeStep() is { } last)
        {
            Queue(last);
        }
        if (touch.Pressed)
        {
            pinch.Press(touch.Index, touch.Position.X, touch.Position.Y);
        }
        else
        {
            pinch.Release(touch.Index);
        }

        if (wasPinching && !pinch.IsPinching)
        {
            FlushGesture();
            EndGesture();
        }
    }

    private void Queue(GestureStep step) => pendingStep = pendingStep is { } pending ? pending.Then(step) : step;

    private void FlushGesture()
    {
        if (pendingStep is { } step)
        {
            pendingStep = null;
            ApplyGesture(step);
        }
    }

    // A pinch or drag step. Every step takes over from any glide, including one a move started mid-gesture, and the zoom
    // it leaves becomes the player's zoom
    private void ApplyGesture(GestureStep step)
    {
        if (space == null || !InputEnabled)
        {
            return;
        }
        FinishOpening();
        glide = null;
        gestureMoved = true;
        var bandTop = new Numerics.Vector2(0, topInset);
        view = space.PanZoom(view, step.Scale, step.From - bandTop, step.To - bandTop);
        playerZoom = view.Zoom;
        Apply();
    }

    private void EndGesture()
    {
        if (!gestureMoved)
        {
            return;
        }
        gestureMoved = false;
        var centre = new Vector2I(Mathf.FloorToInt(view.Focus.Y / BoardView.CellSize), Mathf.FloorToInt(view.Focus.X / BoardView.CellSize));
        GD.Print($"View: {SquaresAcross(view.Zoom):0.0} squares across, centred on [{centre.X}, {centre.Y}]");
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
            glide = new CameraGlide(
                glide.From, space.Resized(glide.To, previous), glide.HalfwayZoom, glide.Seconds, glide.Delay, glide.Offsets);
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
