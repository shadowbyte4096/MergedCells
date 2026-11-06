using System.Numerics;

namespace MergedCells.Core;

/// <summary>
/// A timed camera move between two views, eased in and out, with an optional zoom-out that peaks halfway.
/// Samples aren't clamped: the caller clamps each one with the current <see cref="CameraSpace"/>.
/// </summary>
public sealed class CameraGlide
{
    /// <param name="halfwayZoom">The zoom at the halfway point. Ignored when it isn't below the zoom the glide
    /// would have there anyway, so a glide never zooms in past its ends.</param>
    /// <param name="seconds">How long the move takes, after the delay.</param>
    /// <param name="delay">Seconds spent on the from view first.</param>
    public CameraGlide(CameraView from, CameraView to, float? halfwayZoom, double seconds, double delay = 0)
    {
        RequirePositiveFinite(from.Zoom, nameof(from));
        RequirePositiveFinite(to.Zoom, nameof(to));
        if (halfwayZoom is { } halfway)
        {
            RequirePositiveFinite(halfway, nameof(halfwayZoom));
        }
        RequirePositiveFinite(seconds, nameof(seconds));
        if (!double.IsFinite(delay) || delay < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Must be zero or more, and finite.");
        }

        From = from;
        To = to;
        HalfwayZoom = halfwayZoom;
        Seconds = seconds;
        Delay = delay;
    }

    public CameraView From { get; }

    public CameraView To { get; }

    public float? HalfwayZoom { get; }

    public double Seconds { get; }

    public double Delay { get; }

    /// <summary>The view at this many seconds since the glide started, the delay included.</summary>
    public CameraView Sample(double elapsed)
    {
        var t = Math.Clamp((elapsed - Delay) / Seconds, 0, 1);
        if (t == 0)
        {
            return From;
        }
        if (t == 1)
        {
            return To;
        }

        var eased = t * t * (3 - 2 * t);
        var logFrom = Math.Log(From.Zoom);
        var logTo = Math.Log(To.Zoom);
        var logZoom = logFrom + (logTo - logFrom) * eased;
        if (HalfwayZoom is { } halfway)
        {
            var depth = Math.Log(halfway) - (logFrom + logTo) / 2;
            if (depth < 0)
            {
                logZoom += depth * Math.Sin(Math.PI * t);
            }
        }
        return new CameraView(Vector2.Lerp(From.Focus, To.Focus, (float)eased), (float)Math.Exp(logZoom));
    }

    public bool IsDone(double elapsed) => elapsed >= Delay + Seconds;

    private static void RequirePositiveFinite(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Must be positive and finite.");
        }
    }
}
