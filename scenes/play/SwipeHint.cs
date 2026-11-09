using Godot;
using MergedCells.Core;

namespace MergedCells;

// The ghost finger on a tutorial level: a faint dot sliding along a short fading track in the swipe's direction, in the
// space between the board and the bottom bar, well away from the player. It never takes touches.
public partial class SwipeHint : Control
{
    // The loop's phases, as fractions of loop_ms: fade in, slide, fade out, then a pause
    private const double FadeInEnd = 0.15;
    private const double SlideEnd = 0.70;
    private const double FadeOutEnd = 0.90;

    private Direction? direction;
    private Vector2 centre;
    private bool fits;
    private double seconds;

    // Null hides it
    public Direction? Direction
    {
        get => direction;
        set
        {
            direction = value;
            seconds = 0;
            Visible = direction is not null && fits;
        }
    }

    // Viewport y values: the board's bottom edge on screen and the bottom bar's top. Hidden when the track doesn't fit.
    public void Place(float boardBottom, float barTop)
    {
        var gap = barTop - boardBottom;
        var across = direction is Core.Direction.Up or Core.Direction.Down
            ? GetThemeConstant("track_length", nameof(SwipeHint)) + GetThemeConstant("dot_size", nameof(SwipeHint))
            : GetThemeConstant("dot_size", nameof(SwipeHint));
        fits = gap >= across;
        centre = new Vector2(Size.X / 2, (boardBottom + barTop) / 2);
        Visible = direction is not null && fits;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (Visible)
        {
            seconds += delta;
            QueueRedraw();
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationThemeChanged)
        {
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        if (direction is not { } swipe)
        {
            return;
        }

        var length = GetThemeConstant("track_length", nameof(SwipeHint));
        var width = GetThemeConstant("track_width", nameof(SwipeHint));
        var radius = GetThemeConstant("dot_size", nameof(SwipeHint)) / 2f;
        var loop = GetThemeConstant("loop_ms", nameof(SwipeHint)) / 1000.0;
        var dot = GetThemeColor("dot", nameof(SwipeHint));
        var track = GetThemeColor("track", nameof(SwipeHint));

        var along = swipe switch
        {
            Core.Direction.Up => Vector2.Up,
            Core.Direction.Down => Vector2.Down,
            Core.Direction.Left => Vector2.Left,
            _ => Vector2.Right,
        };
        var side = new Vector2(-along.Y, along.X) * width / 2;
        var start = centre - along * length / 2;
        var end = centre + along * length / 2;
        DrawPolygon(
            [start + side, end + side, end - side, start - side],
            [track with { A = 0 }, track, track, track with { A = 0 }]);

        var phase = loop > 0 ? seconds % loop / loop : 0;
        if (phase >= FadeOutEnd)
        {
            return;
        }
        var first = start + along * radius;
        var last = end - along * radius;
        var travel = Mathf.SmoothStep((float)FadeInEnd, (float)SlideEnd, (float)phase);
        var alpha = phase < FadeInEnd
            ? phase / FadeInEnd
            : phase < SlideEnd ? 1 : 1 - (phase - SlideEnd) / (FadeOutEnd - SlideEnd);
        DrawCircle(first.Lerp(last, travel), radius, dot with { A = dot.A * (float)alpha }, antialiased: true);
    }
}
