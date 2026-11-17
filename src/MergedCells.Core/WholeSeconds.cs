namespace MergedCells.Core;

public static class WholeSeconds
{
    /// <summary>The time in UTC with any fraction of a second dropped, as the custom level files keep times.</summary>
    public static DateTimeOffset Of(DateTimeOffset time) =>
        new(time.UtcTicks - time.UtcTicks % TimeSpan.TicksPerSecond, TimeSpan.Zero);
}
