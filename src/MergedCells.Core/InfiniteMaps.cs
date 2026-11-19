namespace MergedCells.Core;

/// <summary>
/// A map for infinite mode, its par, metrics and score in infinite mode's weights. <paramref name="InBand"/> is false when
/// the climb ran out of steps and this is the nearest map it found.
/// </summary>
public sealed record InfiniteMap(Map Map, int Par, DifficultyMetrics Metrics, int Score, bool InBand);

/// <summary>
/// Makes infinite mode's maps: one climb toward the chosen difficulty's score band, with infinite mode's own step budget,
/// keeping the nearest map when the band is out of reach. Immutable and reads nothing global, so it can run on any thread.
/// </summary>
public sealed class InfiniteMaps
{
    private readonly InfiniteConfig config;
    private readonly MapClimber climber;

    public InfiniteMaps(InfiniteConfig infinite, GeneratorConfig generator, DifficultyConfig difficulty, ClimbConfig climb)
    {
        ArgumentNullException.ThrowIfNull(infinite);
        config = infinite;
        climber = new MapClimber(generator, difficulty, infinite.Climb(climb));
    }

    /// <summary>The climb's step budget, which <see cref="Next"/>'s step counter counts up to.</summary>
    public int Steps => config.ClimbSteps;

    /// <summary>
    /// The map for the choice and seed, or null when the generator made no starting map within the budget. The token and
    /// the counter are the climb's (see <see cref="MapClimber.Climb"/>): neither changes the map.
    /// </summary>
    public InfiniteMap? Next(InfiniteChoice choice, ulong seed, CancellationToken cancel = default, Action<int>? stepsTaken = null)
    {
        if (climber.Climb(Request(choice), seed, cancel, stepsTaken) is not { } climbed)
        {
            return null;
        }
        // The target has no metric minimums or twist reliance, so a distance of 0 is a score inside the band
        return new InfiniteMap(climbed.Map, climbed.Solution.Par!.Value, climbed.Metrics, climbed.Score, climbed.Distance == 0);
    }

    internal ClimbRequest Request(InfiniteChoice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        var band = config.Difficulty(choice.Difficulty);
        return new ClimbRequest(
            new MapRequest(choice.Side, config.MinPar, config.MaxPar, choice.Irregular, choice.Twists(config)),
            new DifficultyTarget(band.MinScore, band.MaxScore),
            DifficultyMode.Infinite);
    }
}
