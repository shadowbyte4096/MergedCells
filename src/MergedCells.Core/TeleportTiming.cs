namespace MergedCells.Core;

// The blink at a teleporter: the player shrinks into it, stays hidden while the camera brings the partner on screen,
// then grows out of the partner
public static class TeleportTiming
{
    /// <summary>
    /// How long the blink takes, from the player reaching the teleporter to the end of the grow. <paramref name="wait"/>
    /// is when, counted from the same moment, the camera's glide to the partner ends (0 for none): the grow starts at
    /// the later of that and the shrink's end. Throws <see cref="ArgumentOutOfRangeException"/> when the shrink or grow
    /// time isn't positive and finite, or the wait is negative or not finite.
    /// </summary>
    public static double Seconds(double wait, SlideConfig config) => GrowStart(wait, config) + config.TeleportGrowSeconds;

    /// <summary>
    /// The player's frame <paramref name="seconds"/> after reaching the teleporter: whether it's on the partner yet, and
    /// its scale. Past the end it's on the partner at full size. Throws as <see cref="Seconds"/> does.
    /// </summary>
    public static (bool OnExit, double Scale) At(double seconds, double wait, SlideConfig config)
    {
        var growStart = GrowStart(wait, config);
        if (seconds < config.TeleportShrinkSeconds)
        {
            var shrunk = Math.Max(seconds, 0) / config.TeleportShrinkSeconds;
            return (false, 1 - shrunk * shrunk);
        }
        var growing = seconds - growStart;
        if (growing <= 0)
        {
            return (true, 0);
        }
        var left = 1 - Math.Min(growing / config.TeleportGrowSeconds, 1);
        return (true, 1 - left * left);
    }

    private static double GrowStart(double wait, SlideConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        RequirePositiveFinite(config.TeleportShrinkSeconds, nameof(config.TeleportShrinkSeconds));
        RequirePositiveFinite(config.TeleportGrowSeconds, nameof(config.TeleportGrowSeconds));
        if (!double.IsFinite(wait) || wait < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(wait), wait, "Must be zero or more and finite.");
        }
        return Math.Max(config.TeleportShrinkSeconds, wait);
    }

    private static void RequirePositiveFinite(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Must be positive and finite.");
        }
    }
}
