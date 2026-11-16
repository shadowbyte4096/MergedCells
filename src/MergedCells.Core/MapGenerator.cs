namespace MergedCells.Core;

/// <summary>
/// Makes random maps from a seed: the start and end in opposite corner areas, the grid cut into mostly thin regions,
/// the request's twists placed, and only maps the solver can win, with no dead ends, no looping slides and par in range,
/// kept. One seed, request and config always give one map on every device. Immutable, so one generator can serve several
/// threads.
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
        // A run is clipped by the map anyway; the cap keeps maxWallRun + 1 from overflowing
        CheckRange(config.MaxWallRun, 1, 100, "generator.maxWallRun");
        this.config = config;
    }

    private static void CheckRange(int value, int min, int max, string name)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, min, name);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, max, name);
    }

    /// <summary>
    /// Tries up to the config's <c>maxAttempts</c> maps from one random stream started at <paramref name="seed"/>, each
    /// with the request's twists, and returns the first that is solvable, has no dead ends and no looping slides, and has
    /// par in the requested range, or null when none does. Throws <see cref="ArgumentOutOfRangeException"/> for a size
    /// below 2, a minimum par below 1, a maximum below the minimum, or a twist density outside 0 to 100.
    /// </summary>
    public GeneratedMap? Generate(MapRequest request, ulong seed)
    {
        Check(request);

        var random = new GameRandom(seed);
        for (var attempt = 0; attempt < config.MaxAttempts; attempt++)
        {
            var (start, end) = StartAndEnd.Place(request.Size, config, random);
            var regions = RegionCutter.Cut(request.Size, end, config, request.Bends, random);
            var (tiles, teleporters) = request.Twists is null
                ? (null, [])
                : TwistPlacer.Place(request.Size, start, end, request.Twists, config.MaxWallRun, random);
            var map = Map.Create(regions, start, end, tiles, teleporters);
            var solution = Solver.Solve(map);
            if (Keeps(solution, request))
            {
                return new GeneratedMap(map, solution);
            }
        }
        return null;
    }

    // A concept level's starting maps: what the skeleton fixes stays, and the start, the cut and the twists fill the rest
    // in the order Generate draws them
    internal GeneratedMap? Generate(MapRequest request, ulong seed, MapSkeleton skeleton)
    {
        Check(request);
        ArgumentNullException.ThrowIfNull(skeleton);
        if (request.Size != skeleton.Size)
        {
            throw new ArgumentException($"The request is {request.Size}×{request.Size} but the skeleton is {skeleton.Size}×{skeleton.Size}.", nameof(skeleton));
        }
        var starts = StartAndEnd.Starts(skeleton, config);
        var end = skeleton.End;

        var random = new GameRandom(seed);
        for (var attempt = 0; attempt < config.MaxAttempts; attempt++)
        {
            var start = starts[random.Next(starts.Count)];
            var regions = RegionCutter.Cut(request.Size, end, config, request.Bends, random, skeleton);
            var (tiles, teleporters) = request.Twists is null
                ? (null, [])
                : TwistPlacer.Place(request.Size, start, end, request.Twists, config.MaxWallRun, random, skeleton);
            var map = Map.Create(regions, start, end, WithFixedTiles(tiles, skeleton), teleporters);
            var solution = Solver.Solve(map);
            if (Keeps(solution, request))
            {
                return new GeneratedMap(map, solution);
            }
        }
        return null;
    }

    private static Tile[,]? WithFixedTiles(Tile[,]? tiles, MapSkeleton skeleton)
    {
        for (var row = 0; row < skeleton.Size; row++)
        {
            for (var column = 0; column < skeleton.Size; column++)
            {
                if (skeleton.TileAt(new Cell(row, column)) is { } tile && tile != Tile.None)
                {
                    tiles ??= new Tile[skeleton.Size, skeleton.Size];
                    tiles[row, column] = tile;
                }
            }
        }
        return tiles;
    }

    private static void Check(MapRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.Size, 2, nameof(request.Size));
        ArgumentOutOfRangeException.ThrowIfLessThan(request.MinPar, 1, nameof(request.MinPar));
        ArgumentOutOfRangeException.ThrowIfLessThan(request.MaxPar, request.MinPar, nameof(request.MaxPar));
        if (request.Twists is { } twists)
        {
            CheckRange(twists.WallPercent, 0, 100, "Twists.WallPercent");
            CheckRange(twists.StickyPercent, 0, 100, "Twists.StickyPercent");
            CheckRange(twists.TrapPercent, 0, 100, "Twists.TrapPercent");
            CheckRange(twists.TeleporterPercent, 0, 100, "Twists.TeleporterPercent");
        }
    }

    // Dead ends are rejected because nothing on the board may show the player why they're stuck (a ring made by bends, a
    // pocket a teleporter leads into), and looping slides because one is a silent no-op, like a swipe that didn't register
    internal static bool Keeps(SolveResult solution, MapRequest request) =>
        solution.IsSolvable && solution.DeadEnds == 0 && solution.LoopingSlides == 0
        && solution.Par >= request.MinPar && solution.Par <= request.MaxPar;
}
