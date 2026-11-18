using System;
using Godot;

namespace MergedCells;

// Diagonal stripes over the parts of its parent slider's track a value can't take, laid over the slider and never taking
// a touch. The colour is the theme's SliderHatch/colors/hatch
public partial class SliderHatch : Control
{
    // The band's height and the stripes' spacing and width, in viewport units: a drawing detail, not a tunable number
    private const float BandHeight = 18;
    private const float Spacing = 9;
    private const float StripeWidth = 3;

    private float low;
    private float high = 1;

    // Values below low and above high (fractions of the slider's range, 0 to 1) are hatched
    public void SetBlocked(float lowFraction, float highFraction)
    {
        low = Math.Clamp(lowFraction, 0, 1);
        high = Math.Clamp(highFraction, 0, 1);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var slider = GetParent<Slider>();
        // The knob's centre travels from half its width in from each end, so the hatch stops where the knob can
        var knob = slider.GetThemeIcon("grabber").GetWidth();
        var travel = Size.X - knob;
        var colour = GetThemeColor("hatch", "SliderHatch");
        if (low > 0)
        {
            Stripes(0, knob / 2 + low * travel, colour);
        }
        if (high < 1)
        {
            Stripes(knob / 2 + high * travel, Size.X, colour);
        }
    }

    private void Stripes(float from, float to, Color colour)
    {
        var top = (Size.Y - BandHeight) / 2;
        var bottom = top + BandHeight;
        for (var x = from - BandHeight; x < to; x += Spacing)
        {
            // Each stripe runs up and to the right across the band, cut to the hatched stretch
            var start = new Vector2(x, bottom);
            var end = new Vector2(x + BandHeight, top);
            if (start.X < from)
            {
                start = new Vector2(from, bottom - (from - x));
            }
            if (end.X > to)
            {
                end = new Vector2(to, bottom - (to - x));
            }
            if (end.X > start.X)
            {
                DrawLine(start, end, colour, StripeWidth, true);
            }
        }
    }
}
