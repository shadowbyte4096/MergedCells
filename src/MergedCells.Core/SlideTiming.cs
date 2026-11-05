namespace MergedCells.Core;

public static class SlideTiming
{
    /// <summary>
    /// How long a slide of this many squares takes, in seconds: constant speed, capped at the config's maximum.
    /// Throws <see cref="ArgumentOutOfRangeException"/> when squares is below 1, or a config value isn't positive and finite.
    /// </summary>
    public static double Seconds(int squares, SlideConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentOutOfRangeException.ThrowIfLessThan(squares, 1);
        RequirePositiveFinite(config.SquaresPerSecond, nameof(config.SquaresPerSecond));
        RequirePositiveFinite(config.MaxSeconds, nameof(config.MaxSeconds));

        return Math.Min(squares / config.SquaresPerSecond, config.MaxSeconds);
    }

    private static void RequirePositiveFinite(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Must be positive and finite.");
        }
    }
}
