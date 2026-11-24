using System;
using System.Text.Json;
using Godot;
using MergedCells.Core;
using NVector2 = System.Numerics.Vector2;

namespace MergedCells;

// The startup intro, played once over the first mode select: the icon opens out from the launch screen's circle, the
// title rises in, then the scene gives way to the live menu while the title glides onto the menu's own. Core's
// IntroTimeline works out each frame; intro.gdshader paints it
public partial class IntroLayer : CanvasLayer
{
    private const string ArtPath = "res://icons/icon-art.json";

    private readonly IntroClock clock = new();
    private ColorRect canvas;
    private ShaderMaterial material;
    private Label title;
    private IconArt art;
    private IntroTimeline timeline;
    private Color ink;
    private bool pressed;
    private bool finished;

    public event Action Finished;

    [Export] public AppLooks Looks { get; set; }

    // The mode select underneath, whose title the intro's title lands on; null until it's open
    public Func<ModeSelect> Menu { get; set; }

    public bool Playing => !finished;

    public override void _Ready()
    {
        canvas = GetNode<ColorRect>("Canvas");
        title = GetNode<Label>("Title");
        material = (ShaderMaterial)canvas.Material;

        try
        {
            art = IconArt.Parse(FileAccess.GetFileAsString(ArtPath));
        }
        catch (JsonException exception)
        {
            GD.PushError($"{ArtPath}: {exception.Message}");
            Skip();
            return;
        }

        var viewport = ViewportSize();
        timeline = new IntroTimeline(GameConfigFile.Current.Intro, art, LaunchCircle.For(viewport));

        var board = Looks.Board(dark: true);
        ink = board.Outline;
        material.SetShaderParameter("background", board.Background);
        material.SetShaderParameter("ink", ink);
        material.SetShaderParameter("left_fill", board.Fills[art.LeftBlock.Fill]);
        material.SetShaderParameter("right_fill", board.Fills[art.RightBlock.Fill]);
        material.SetShaderParameter("left_shape", new Vector2((float)art.LeftBlock.Corner, (float)art.LeftBlock.Outline));
        material.SetShaderParameter("right_shape", new Vector2((float)art.RightBlock.Corner, (float)art.RightBlock.Outline));
        material.SetShaderParameter("player", new Vector4((float)art.Player.X, (float)art.Player.Y, (float)art.Player.Side, (float)art.Player.Outline));
        material.SetShaderParameter("player_corner", (float)art.Player.Corner);
        material.SetShaderParameter("fade_end", (float)art.Player.FadeEnd);
        // The first frame drawn is the launch frame
        Draw(timeline.At(0, viewport, IntroTimeline.SceneTitle(viewport)));
    }

    public override void _Process(double delta)
    {
        if (finished)
        {
            return;
        }
        var viewport = ViewportSize();
        var menu = Menu?.Invoke();
        if (menu is not null)
        {
            menu.TitleShown = false;
        }
        var frame = timeline.At(clock.Tick(delta), viewport, menu?.TitleSpot ?? IntroTimeline.SceneTitle(viewport));
        Draw(frame);
        if (frame.Done)
        {
            Skip();
        }
    }

    // A tap or click skips on release, so neither the touch nor the mouse press Godot emulates from it reaches the menu
    // underneath. Any key but Android's back skips at once; Main pairs back presses and skips through Skip.
    public override void _Input(InputEvent @event)
    {
        if (finished)
        {
            return;
        }
        switch (@event)
        {
            case InputEventScreenTouch touch:
                GetViewport().SetInputAsHandled();
                SkipOnRelease(touch.Pressed);
                break;
            case InputEventMouseButton button:
                GetViewport().SetInputAsHandled();
                SkipOnRelease(button.Pressed);
                break;
            case InputEventScreenDrag or InputEventMouseMotion:
                GetViewport().SetInputAsHandled();
                break;
            case InputEventKey { Pressed: true, Echo: false } key when key.Keycode != Key.Back:
                GetViewport().SetInputAsHandled();
                Skip();
                break;
        }
    }

    public void Skip()
    {
        if (finished)
        {
            return;
        }
        finished = true;
        if (Menu?.Invoke() is { } menu)
        {
            menu.TitleShown = true;
        }
        Finished?.Invoke();
        QueueFree();
    }

    private void SkipOnRelease(bool down)
    {
        if (down)
        {
            pressed = true;
        }
        else if (pressed)
        {
            Skip();
        }
    }

    private void Draw(IntroFrame frame)
    {
        material.SetShaderParameter("background_alpha", frame.BackgroundAlpha);
        material.SetShaderParameter("scene_alpha", frame.SceneAlpha);
        material.SetShaderParameter("circle_centre", ToGodot(frame.CircleCentre));
        material.SetShaderParameter("circle_radius", frame.CircleRadius);
        material.SetShaderParameter("art_origin", ToGodot(frame.ArtOrigin));
        material.SetShaderParameter("art_scale", frame.ArtScale);
        var left = art.LeftBlock;
        var leftRight = (float)(left.X + left.Width);
        material.SetShaderParameter("left_block", new Vector4(frame.LeftBlockLeft, (float)left.Y, leftRight - frame.LeftBlockLeft, (float)left.Height));
        var right = art.RightBlock;
        material.SetShaderParameter("right_block", new Vector4((float)right.X, (float)right.Y, frame.RightBlockRight - (float)right.X, (float)right.Height));
        material.SetShaderParameter("trail_start", frame.TrailStart);
        PlaceTitle(frame);
    }

    // Godot font sizes are whole numbers, so the label takes the nearest size and scales by what's left over; at the
    // end it's the menu title's own size at scale 1, so the hand-over is exact
    private void PlaceTitle(IntroFrame frame)
    {
        var size = Math.Max(1, (int)Math.Round(frame.Title.FontSize));
        title.AddThemeFontSizeOverride("font_size", size);
        // A label is never smaller than its text, so it's centred by the size it actually takes
        title.Size = new Vector2(frame.Title.Width, title.GetThemeFont("font").GetHeight(size));
        title.PivotOffset = title.Size / 2;
        title.Scale = Vector2.One * (frame.Title.FontSize / size);
        title.Position = ToGodot(frame.Title.Centre) - title.Size / 2;
        var target = Menu?.Invoke()?.TitleColour ?? ink;
        title.AddThemeColorOverride("font_color", ink.Lerp(target, frame.TitleColourMix));
        title.Modulate = new Color(1, 1, 1, frame.TitleAlpha);
    }

    private NVector2 ViewportSize() => ToCore(GetViewport().GetVisibleRect().Size);

    private static NVector2 ToCore(Vector2 vector) => new(vector.X, vector.Y);

    private static Vector2 ToGodot(NVector2 vector) => new(vector.X, vector.Y);
}
