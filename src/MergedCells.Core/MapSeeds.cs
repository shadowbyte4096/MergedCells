namespace MergedCells.Core;

/// <summary>
/// Seeds for maps that needn't be the same on every device (infinite mode's): the clock's ticks and a counter, mixed with
/// whole-number arithmetic so nothing but <see cref="GameRandom"/> draws a random number.
/// </summary>
public static class MapSeeds
{
    // 2^64 divided by the golden ratio: odd, so the multiply never loses a tick's information
    private const ulong Mixer = 0x9E3779B97F4A7C15UL;

    public static ulong From(long ticks, int counter) => unchecked((ulong)ticks * Mixer + (ulong)counter);
}
