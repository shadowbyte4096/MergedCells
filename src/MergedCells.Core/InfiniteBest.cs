namespace MergedCells.Core;

/// <summary>
/// The personal bests for one infinite choice: the longest streak, the highest run score, and the highest average efficiency
/// over the scoring window's maps in a row, in hundredths of a percent, or null while no run has reached the window.
/// </summary>
public sealed record InfiniteBest(int Streak, long Run, int? Efficiency)
{
    // What a choice with nothing saved counts as
    public static InfiniteBest None { get; } = new(0, 0, null);

    public InfiniteBest Max(InfiniteBest other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new InfiniteBest(
            Math.Max(Streak, other.Streak),
            Math.Max(Run, other.Run),
            Efficiency is { } mine && other.Efficiency is { } theirs ? Math.Max(mine, theirs) : Efficiency ?? other.Efficiency);
    }
}
