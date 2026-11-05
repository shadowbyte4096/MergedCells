using System.Collections.Generic;
using Godot;

namespace MergedCells;

// The fading strip behind a sliding player. Its own node, so the trail never redraws the board
public partial class TrailView : Node2D
{
    private sealed class Strip
    {
        public Vector2 From;
        public Vector2 To;
        public float Fade = 1;
        public bool Fading;
    }

    private readonly List<Strip> strips = [];
    private PlayerLook look;
    private StyleBoxFlat box;

    [Export]
    public PlayerLook Look
    {
        get => look;
        set
        {
            look = value;
            if (look != null)
            {
                box = new StyleBoxFlat { AntiAliasing = true };
                box.SetCornerRadiusAll(Mathf.RoundToInt(look.CornerFraction * BoardView.CellSize));
            }
            QueueRedraw();
        }
    }

    public void Begin(Vector2 from)
    {
        Release();
        strips.Add(new Strip { From = from, To = from });
        QueueRedraw();
    }

    public void Stretch(Vector2 to)
    {
        if (Growing() is { } strip)
        {
            strip.To = to;
            QueueRedraw();
        }
    }

    public void Release()
    {
        if (Growing() is { } strip)
        {
            strip.Fading = true;
        }
    }

    public void Clear()
    {
        strips.Clear();
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (look == null)
        {
            return;
        }
        var changed = false;
        for (var i = strips.Count - 1; i >= 0; i--)
        {
            var strip = strips[i];
            if (!strip.Fading)
            {
                continue;
            }
            strip.Fade -= (float)(delta / look.TrailFadeSeconds);
            if (strip.Fade <= 0)
            {
                strips.RemoveAt(i);
            }
            changed = true;
        }
        if (changed)
        {
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        if (look == null)
        {
            return;
        }
        var half = look.SideFraction * BoardView.CellSize / 2;
        foreach (var strip in strips)
        {
            // The box's colour is read when it's drawn, so one box serves every strip
            box.BgColor = new Color(look.Color, look.TrailAlpha * strip.Fade);
            DrawStyleBox(box, new Rect2(strip.From, Vector2.Zero).Expand(strip.To).Grow(half));
        }
    }

    private Strip Growing() => strips.Count > 0 && !strips[^1].Fading ? strips[^1] : null;
}
