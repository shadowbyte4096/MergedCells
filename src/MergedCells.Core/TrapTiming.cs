namespace MergedCells.Core;

// The player's return after a slide into a trap: shrinking on the trap, hidden, then growing on the start
public static class TrapTiming
{
    /// <summary>
    /// How long the return takes, in seconds. Throws <see cref="ArgumentOutOfRangeException"/> when the shrink or grow
    /// time isn't positive and finite, or the pause is negative or not finite.
    /// </summary>
    public static double Seconds(SlideConfig config)
    {
        Check(config);
        return config.TrapShrinkSeconds + config.TrapPauseSeconds + config.TrapGrowSeconds;
    }

    /// <summary>
    /// The player's frame <paramref name="seconds"/> into the return: whether it's on the start yet, and its scale. Past
    /// the end it's on the start at full size. Throws as <see cref="Seconds"/> does.
    /// </summary>
    public static (bool OnStart, double Scale) At(double seconds, SlideConfig config)
    {
        Check(config);
        if (seconds < config.TrapShrinkSeconds)
        {
            var shrunk = Math.Max(seconds, 0) / config.TrapShrinkSeconds;
            return (false, 1 - shrunk * shrunk);
        }
        var growing = seconds - config.TrapShrinkSeconds - config.TrapPauseSeconds;
        if (growing <= 0)
        {
            return (true, 0);
        }
        var left = 1 - Math.Min(growing / config.TrapGrowSeconds, 1);
        return (true, 1 - left * left);
    }

    private static void Check(SlideConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        RequirePositiveFinite(config.TrapShrinkSeconds, nameof(config.TrapShrinkSeconds));
        RequirePositiveFinite(config.TrapGrowSeconds, nameof(config.TrapGrowSeconds));
        if (!double.IsFinite(config.TrapPauseSeconds) || config.TrapPauseSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(config.TrapPauseSeconds), config.TrapPauseSeconds, "Must be zero or more and finite.");
        }
    }

    private static void RequirePositiveFinite(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Must be positive and finite.");
        }
    }
}
