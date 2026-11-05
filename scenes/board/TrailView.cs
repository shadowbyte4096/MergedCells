using System.Collections.Generic;
using Godot;

namespace MergedCells;

// The fading strip behind a sliding player. Its own node, so the trail never redraws the board. Each part fades by
// the time since the player passed it, so the strip shrinks behind the player mid-slide.
public partial class TrailView : Node2D
{
    private const int ArcSegments = 6;

    private sealed class Strip
    {
        public Vector2 From;
        public double FromTime;
        public Vector2 To;
        public double ToTime;
        public bool Growing = true;
    }

    private readonly List<Strip> strips = [];
    private PlayerLook look;
    private double clock;

    [Export]
    public PlayerLook Look
    {
        get => look;
        set
        {
            look = value;
            QueueRedraw();
        }
    }

    // Game time, like the slide animation. Processed before Play, which stretches the strip, so a part is stamped
    // with the frame it's drawn in.
    public override void _Ready() => ProcessPriority = -1;

    public void Begin(Vector2 from)
    {
        Release();
        strips.Add(new Strip { From = from, FromTime = clock, To = from, ToTime = clock });
        QueueRedraw();
    }

    public void Stretch(Vector2 to)
    {
        if (GrowingStrip() is { } strip)
        {
            strip.To = to;
            strip.ToTime = clock;
            QueueRedraw();
        }
    }

    public void Release()
    {
        if (GrowingStrip() is { } strip)
        {
            strip.Growing = false;
        }
    }

    public void Clear()
    {
        strips.Clear();
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        clock += delta;
        if (look == null || strips.Count == 0)
        {
            return;
        }
        strips.RemoveAll(strip => !strip.Growing && clock - strip.ToTime >= look.TrailFadeSeconds);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (look == null)
        {
            return;
        }
        var now = clock;
        var half = look.SideFraction * BoardView.CellSize / 2;
        var radius = Mathf.Min(Mathf.Round(look.CornerFraction * BoardView.CellSize), half);
        foreach (var strip in strips)
        {
            DrawStrip(strip, now, half, radius);
        }
    }

    // The player moves at a constant speed along a strip, so a part's age, and with it its alpha, is linear along it
    private void DrawStrip(Strip strip, double now, float half, float radius)
    {
        var fade = look.TrailFadeSeconds;
        var headAge = (float)(now - strip.ToTime);
        var tailAge = (float)(now - strip.FromTime);
        if (headAge >= fade)
        {
            return;
        }
        var tail = strip.From;
        if (tailAge > fade)
        {
            tail = strip.From.Lerp(strip.To, (tailAge - fade) / (tailAge - headAge));
            tailAge = fade;
        }

        var axis = strip.To - tail;
        var length = axis.Length();
        var points = RoundedRect(new Rect2(tail, Vector2.Zero).Expand(strip.To).Grow(half), radius);
        var colors = new Color[points.Length];
        for (var i = 0; i < points.Length; i++)
        {
            // Past either end of the strip, a part is as old as that end
            var along = length > 0 ? Mathf.Clamp((points[i] - tail).Dot(axis) / (length * length), 0, 1) : 1;
            var age = Mathf.Lerp(tailAge, headAge, along);
            colors[i] = new Color(look.Color, look.TrailAlpha * (1 - age / fade));
        }
        DrawPolygon(points, colors);
    }

    private static Vector2[] RoundedRect(Rect2 rect, float radius)
    {
        var segments = radius > 0 ? ArcSegments : 0;
        (Vector2 Centre, float Start)[] corners =
        [
            (rect.Position + new Vector2(radius, radius), Mathf.Pi),
            (new Vector2(rect.End.X - radius, rect.Position.Y + radius), Mathf.Pi * 1.5f),
            (rect.End - new Vector2(radius, radius), 0),
            (new Vector2(rect.Position.X + radius, rect.End.Y - radius), Mathf.Pi * 0.5f),
        ];
        var points = new Vector2[corners.Length * (segments + 1)];
        var index = 0;
        foreach (var (centre, start) in corners)
        {
            for (var step = 0; step <= segments; step++)
            {
                var angle = segments == 0 ? start : start + Mathf.Pi / 2 * step / segments;
                points[index++] = centre + radius * Vector2.FromAngle(angle);
            }
        }
        return points;
    }

    private Strip GrowingStrip() => strips.Count > 0 && strips[^1].Growing ? strips[^1] : null;
}
