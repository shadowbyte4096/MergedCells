namespace MergedCells.Core;

/// <summary>
/// What the player picks on infinite mode's setup screen. Every change goes through the <c>With</c> methods, which keep the
/// size inside the sizes the difficulty allows for the switches as they are.
/// </summary>
public sealed record InfiniteChoice(int Difficulty, int Side, bool Walls, bool Traps, bool Teleporters, bool Irregular)
{
    // Irregular regions don't count: they change a map's shapes, not its twists
    public bool HasTwists => Walls || Traps || Teleporters;

    public SideRange Sizes(InfiniteConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var difficulty = config.Difficulty(Difficulty);
        return HasTwists ? difficulty.Twists : difficulty.Plain;
    }

    public static InfiniteChoice Start(InfiniteConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return new InfiniteChoice(config.StartDifficulty, config.StartSide, false, false, false, false).Clamped(config);
    }

    public InfiniteChoice WithDifficulty(int difficulty, InfiniteConfig config)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(difficulty, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(difficulty, InfiniteConfig.DifficultyCount);
        return (this with { Difficulty = difficulty }).Clamped(config);
    }

    public InfiniteChoice WithSide(int side, InfiniteConfig config) => (this with { Side = side }).Clamped(config);

    public InfiniteChoice WithWalls(bool on, InfiniteConfig config) => (this with { Walls = on }).Clamped(config);

    public InfiniteChoice WithTraps(bool on, InfiniteConfig config) => (this with { Traps = on }).Clamped(config);

    public InfiniteChoice WithTeleporters(bool on, InfiniteConfig config) => (this with { Teleporters = on }).Clamped(config);

    public InfiniteChoice WithIrregular(bool on) => this with { Irregular = on };

    /// <summary>A choice saved under another config, made to fit this one: a difficulty outside 1 to 10 becomes the start difficulty.</summary>
    public InfiniteChoice Fitted(InfiniteConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var difficulty = Difficulty is >= 1 and <= InfiniteConfig.DifficultyCount ? Difficulty : config.StartDifficulty;
        return (this with { Difficulty = difficulty }).Clamped(config);
    }

    // Null with walls, traps and teleporters all off, so the generator makes a map with no twists
    public TwistDensities? Twists(InfiniteConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return HasTwists
            ? new TwistDensities(
                Walls ? config.WallPercent : 0,
                0,
                Traps ? config.TrapPercent : 0,
                Teleporters ? config.TeleporterPercent : 0)
            : null;
    }

    private InfiniteChoice Clamped(InfiniteConfig config)
    {
        var sizes = Sizes(config);
        return this with { Side = Math.Clamp(Side, sizes.MinSide, sizes.MaxSide) };
    }
}
