namespace MergedCells.Core;

/// <summary>
/// Makes random maps from a seed: the start and end in opposite corner areas, the grid cut into mostly thin regions,
/// and only maps the solver can win, with no dead ends and par in range, kept. One seed, request and config always give
/// one map on every device. Immutable, so one generator can serve several threads.
/// </summary>
public sealed class MapGenerator
{
    private readonly GeneratorConfig config;

    /// <summary>Throws <see cref="ArgumentOutOfRangeException"/> naming any config number out of range.</summary>
    public MapGenerator(GeneratorConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentOutOfRangeException.ThrowIfNegative(config.AcrossWeight, "generator.acrossWeight");
        ArgumentOutOfRangeException.ThrowIfNegative(config.DownWeight, "generator.downWeight");
        ArgumentOutOfRangeException.ThrowIfNegative(config.SingleWeight, "generator.singleWeight");
        ArgumentOutOfRangeException.ThrowIfNegative(config.BlockWeight, "generator.blockWeight");
        if ((long)config.AcrossWeight + config.DownWeight + config.SingleWeight + config.BlockWeight is < 1 or > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(config), "The generator's four weights must add up to at least 1 and fit in a whole number.");
        }
        CheckRange(config.MaxStripPercent, 1, 100, "generator.maxStripPercent");
        // Blocks are meant to be small, and the search for one that fits grows with the fourth power of this
        CheckRange(config.MaxBlockSide, 2, 10, "generator.maxBlockSide");
        CheckRange(config.BendPercent, 0, 100, "generator.bendPercent");
        ArgumentOutOfRangeException.ThrowIfLessThan(config.MaxBendSquares, 3, "generator.maxBendSquares");
        CheckRange(config.CornerPercent, 1, 100, "generator.cornerPercent");
        CheckRange(config.MinDistancePercent, 0, 100, "generator.minDistancePercent");
        ArgumentOutOfRangeException.ThrowIfLessThan(config.MaxAttempts, 1, "generator.maxAttempts");
        this.config = config;
    }

    private static void CheckRange(int value, int min, int max, string name)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, min, name);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, max, name);
    }

    /// <summary>
    /// Tries up to the config's <c>maxAttempts</c> maps from one random stream started at <paramref name="seed"/>, and
    /// returns the first that is solvable, has no dead ends and has par in the requested range, or null when none does.
    /// Throws <see cref="ArgumentOutOfRangeException"/> for a size below 2, a minimum par below 1, or a maximum below the
    /// minimum.
    /// </summary>
    public GeneratedMap? Generate(MapRequest request, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.Size, 2, nameof(request.Size));
        ArgumentOutOfRangeException.ThrowIfLessThan(request.MinPar, 1, nameof(request.MinPar));
        ArgumentOutOfRangeException.ThrowIfLessThan(request.MaxPar, request.MinPar, nameof(request.MaxPar));

        var random = new GameRandom(seed);
        for (var attempt = 0; attempt < config.MaxAttempts; attempt++)
        {
            var (start, end) = StartAndEnd.Place(request.Size, config, random);
            var regions = RegionCutter.Cut(request.Size, end, config, request.Bends, random);
            var map = Map.Create(regions, start, end);
            var solution = Solver.Solve(map);
            if (Keeps(solution, request))
            {
                return new GeneratedMap(map, solution);
            }
        }
        return null;
    }

    // Dead ends are rejected because nothing on the board would show the player why they're stuck (a ring made by bends)
    internal static bool Keeps(SolveResult solution, MapRequest request) =>
        solution.IsSolvable && solution.DeadEnds == 0 && solution.Par >= request.MinPar && solution.Par <= request.MaxPar;
}
