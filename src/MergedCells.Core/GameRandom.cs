namespace MergedCells.Core;

// The game's own random numbers: PCG32 (O'Neill), integer only, so a seed gives the same numbers on every device and
// runtime. The daily puzzle depends on that, so the algorithm and its stream must never change.
internal sealed class GameRandom
{
    private const ulong Multiplier = 6364136223846793005UL;
    // pcg32-demo's stream, so its published outputs for seed 42 check this implementation
    private const ulong Stream = 54;

    private readonly ulong increment;
    private ulong state;

    // The arithmetic wraps and truncates on purpose, so it is unchecked whatever the build's overflow setting
    public GameRandom(ulong seed)
    {
        increment = (Stream << 1) | 1;
        NextUInt();
        state = unchecked(state + seed);
        NextUInt();
    }

    public uint NextUInt()
    {
        unchecked
        {
            var old = state;
            state = old * Multiplier + increment;
            var xorShifted = (uint)(((old >> 18) ^ old) >> 27);
            var rotation = (int)(old >> 59);
            return (xorShifted >> rotation) | (xorShifted << (-rotation & 31));
        }
    }

    // Lemire's multiply-and-reject method, so every value below the bound is equally likely
    public int Next(int maxExclusive)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxExclusive, 1);
        unchecked
        {
            var bound = (uint)maxExclusive;
            var product = (ulong)NextUInt() * bound;
            if ((uint)product < bound)
            {
                var threshold = (0u - bound) % bound;
                while ((uint)product < threshold)
                {
                    product = (ulong)NextUInt() * bound;
                }
            }
            return (int)(product >> 32);
        }
    }

    public int Next(int minInclusive, int maxExclusive)
    {
        var width = (long)maxExclusive - minInclusive;
        if (width < 1 || width > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxExclusive), $"The range [{minInclusive}, {maxExclusive}) must hold 1 to {int.MaxValue} values.");
        }
        return minInclusive + Next((int)width);
    }

    // Fisher-Yates from the end
    public void Shuffle<T>(IList<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
