using AutoFixture;
using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class MapGeneratorTests
{
    // Each is the nearest value the generator rejects
    public static TheoryData<GeneratorConfig> OutOfRangeConfigs => new()
    {
        GeneratorConfigs.With(acrossWeight: -1),
        GeneratorConfigs.With(downWeight: -1),
        GeneratorConfigs.With(singleWeight: -1),
        GeneratorConfigs.With(blockWeight: -1),
        GeneratorConfigs.With(0, 0, 0, 0),
        GeneratorConfigs.With(maxStripPercent: 0),
        GeneratorConfigs.With(maxStripPercent: 101),
        GeneratorConfigs.With(maxBlockSide: 1),
        GeneratorConfigs.With(maxBlockSide: 11),
        GeneratorConfigs.With(bendPercent: -1),
        GeneratorConfigs.With(bendPercent: 101),
        GeneratorConfigs.With(maxBendSquares: 2),
        GeneratorConfigs.With(cornerPercent: 0),
        GeneratorConfigs.With(cornerPercent: 101),
        GeneratorConfigs.With(minDistancePercent: -1),
        GeneratorConfigs.With(minDistancePercent: 101),
        GeneratorConfigs.With(maxAttempts: 0),
        GeneratorConfigs.With(maxWallRun: 0),
        GeneratorConfigs.With(maxWallRun: 101),
    };

    [Theory]
    [MemberData(nameof(OutOfRangeConfigs))]
    public void Rejects_a_config_number_out_of_range(GeneratorConfig config)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MapGenerator(config));
    }

    [Fact]
    public void Accepts_every_range_s_edge()
    {
        var config = GeneratorConfigs.With(0, 0, 0, 1, maxStripPercent: 1, maxBlockSide: 2, bendPercent: 0, maxBendSquares: 3, cornerPercent: 100, minDistancePercent: 0, maxAttempts: 1, maxWallRun: 1);

        Assert.Null(Record.Exception(() => new MapGenerator(config)));
    }

    [Fact]
    public void Accepts_the_largest_block_side()
    {
        Assert.Null(Record.Exception(() => new MapGenerator(GeneratorConfigs.With(maxBlockSide: 10))));
    }

    [Fact]
    public void Accepts_the_longest_wall_run()
    {
        Assert.Null(Record.Exception(() => new MapGenerator(GeneratorConfigs.With(maxWallRun: 100))));
    }

    [Theory]
    [InlineAutoData(1, 1, 1)]
    [InlineAutoData(5, 0, 5)]
    [InlineAutoData(5, 4, 3)]
    public void Rejects_an_invalid_request(int size, int minPar, int maxPar, ulong seed)
    {
        var generator = new MapGenerator(GeneratorConfigs.With());

        Assert.Throws<ArgumentOutOfRangeException>(() => generator.Generate(new MapRequest(size, minPar, maxPar), seed, TestContext.Current.CancellationToken));
    }

    // The filter, on hand-made maps: no generated map has turned up solvable with dead ends, so a seed can't test that
    [Fact]
    public void Keeps_a_solvable_map_with_par_in_range()
    {
        Assert.True(MapGenerator.Keeps(Solver.Solve(TestMaps.Example()), new MapRequest(4, 3, 3)));
    }

    // Par 1, but the four squares round the ring can't reach the end (from SolverTests)
    [Fact]
    public void Rejects_a_map_with_dead_ends()
    {
        var map = Map.Create(TestMaps.Grid("AAAA", "ABAA", "AAAA"), new Cell(1, 0), new Cell(1, 1));

        Assert.False(MapGenerator.Keeps(Solver.Solve(map), new MapRequest(4, 1, 10)));
    }

    [Fact]
    public void Rejects_an_unsolvable_map()
    {
        var map = Map.Create(TestMaps.Grid("AAA", "ABA", "AAA"), new Cell(0, 0), new Cell(1, 1));

        Assert.False(MapGenerator.Keeps(Solver.Solve(map), new MapRequest(3, 1, 10)));
    }

    // The example map's par is 3
    [Theory]
    [InlineData(4, 10)]
    [InlineData(1, 2)]
    public void Rejects_par_outside_the_range(int minPar, int maxPar)
    {
        Assert.False(MapGenerator.Keeps(Solver.Solve(TestMaps.Example()), new MapRequest(4, minPar, maxPar)));
    }

    [Theory]
    [AutoData]
    public void Map_is_the_requested_size(ulong seed)
    {
        var generated = Generated(new MapRequest(9, 1, 100), seed);

        Assert.Equal((9, 9), (generated.Map.Rows, generated.Map.Columns));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Map_is_solvable_with_no_dead_ends_and_par_in_range(bool bends)
    {
        var generator = new MapGenerator(GeneratorConfigs.With());

        var generated = new Fixture().CreateMany<ulong>(30)
            .Select(seed => generator.Generate(new MapRequest(10, 3, 12, bends), seed))
            .OfType<GeneratedMap>()
            .ToList();

        Assert.NotEmpty(generated);
        Assert.All(generated, map =>
        {
            var solution = Solver.Solve(map.Map);
            Assert.True(solution.IsSolvable && solution.DeadEnds == 0, $"Dead ends: {solution.DeadEnds}");
            Assert.InRange(Assert.NotNull(solution.Par), 3, 12);
        });
    }

    [Theory]
    [AutoData]
    public void Solution_is_the_map_s(ulong seed)
    {
        var generated = Generated(new MapRequest(8, 1, 100), seed);

        Assert.Equal(Solver.Solve(generated.Map).Path, generated.Solution.Path);
    }

    [Theory]
    [AutoData]
    public void Size_2_gives_a_map(ulong seed)
    {
        var generated = Generated(new MapRequest(2, 1, 100), seed);

        Assert.Equal((new Cell(0, 0), new Cell(1, 1)), (generated.Map.Start, generated.Map.End));
    }

    [Theory]
    [AutoData]
    public void One_seed_gives_one_map(ulong seed)
    {
        var first = Generated(new MapRequest(12, 1, 100, Bends: true), seed);
        var second = Generated(new MapRequest(12, 1, 100, Bends: true), seed);

        Assert.Equal(Picture(first), Picture(second));
    }

    [Theory]
    [AutoData]
    public void Cancelled_token_stops_generating(ulong seed)
    {
        var generator = new MapGenerator(GeneratorConfigs.With());

        Assert.Throws<OperationCanceledException>(() => generator.Generate(new MapRequest(8, 1, 100), seed, new CancellationToken(canceled: true)));
    }

    // Teleporter maps can loop, so they're made on another thread under a timeout
    [Theory(Timeout = 10_000)]
    [AutoData]
    public async Task Live_token_gives_the_same_map(ulong seed)
    {
        var generator = new MapGenerator(GeneratorConfigs.With());
        var request = new MapRequest(8, 1, 100, Twists: GeneratedMaps.EveryTwist);
        using var source = new CancellationTokenSource();
        var testToken = TestContext.Current.CancellationToken;

        var withToken = (await Task.Run(() => generator.Generate(request, seed, source.Token), testToken))!;
        var without = (await Task.Run(() => generator.Generate(request, seed, testToken), testToken))!;

        Assert.Equal(LevelFingerprint.Of(without.Map, without.Solution.Par!.Value), LevelFingerprint.Of(withToken.Map, withToken.Solution.Par!.Value));
    }

    [Theory]
    [AutoData]
    public void An_impossible_par_range_gives_null(ulong seed)
    {
        var generator = new MapGenerator(GeneratorConfigs.With(maxAttempts: 20));

        Assert.Null(generator.Generate(new MapRequest(5, 100, 200), seed, TestContext.Current.CancellationToken));
    }

    [Theory]
    [AutoData]
    public void Generates_a_100x100_map(ulong seed)
    {
        var generated = new MapGenerator(GeneratorConfigs.With()).Generate(new MapRequest(100, 1, 10_000), seed, TestContext.Current.CancellationToken);

        Assert.NotNull(generated);
    }

    public static TheoryData<TwistDensities> OutOfRangeDensities => new()
    {
        new TwistDensities(WallPercent: -1),
        new TwistDensities(WallPercent: 101),
        new TwistDensities(StickyPercent: -1),
        new TwistDensities(StickyPercent: 101),
        new TwistDensities(TrapPercent: -1),
        new TwistDensities(TrapPercent: 101),
        new TwistDensities(TeleporterPercent: -1),
        new TwistDensities(TeleporterPercent: 101),
    };

    [Theory]
    [MemberData(nameof(OutOfRangeDensities))]
    public void Rejects_a_twist_density_out_of_range(TwistDensities twists)
    {
        var generator = new MapGenerator(GeneratorConfigs.With());

        Assert.Throws<ArgumentOutOfRangeException>(() => generator.Generate(new MapRequest(5, 1, 10, Twists: twists), Seed(), TestContext.Current.CancellationToken));
    }

    // Par 2 with no dead ends, but Left and Right from the start both loop
    [Fact(Timeout = 10_000)]
    public async Task Keeps_rejects_a_map_with_a_looping_slide()
    {
        var solution = await Task.Run(() => Solver.Solve(TestMaps.LoopingLane()), TestContext.Current.CancellationToken);

        Assert.False(MapGenerator.Keeps(solution, new MapRequest(5, 1, 10)));
    }

    public static TheoryData<TwistDensities> TwistMixes => new()
    {
        new TwistDensities(WallPercent: 8),
        new TwistDensities(StickyPercent: 8),
        new TwistDensities(TrapPercent: 6),
        new TwistDensities(TeleporterPercent: 4),
        new TwistDensities(6, 4, 3, 2),
    };

    [Theory(Timeout = 10_000)]
    [MemberData(nameof(TwistMixes))]
    public async Task Map_with_twists_is_solvable_with_no_dead_ends_no_loops_and_par_in_range(TwistDensities twists)
    {
        var generator = new MapGenerator(GeneratorConfigs.With());

        var solutions = await Task.Run(
            () => new Fixture().CreateMany<ulong>(30)
                .Select(seed => generator.Generate(new MapRequest(10, 3, 12, Twists: twists), seed))
                .OfType<GeneratedMap>()
                .Select(generated => Solver.Solve(generated.Map))
                .ToList(),
            TestContext.Current.CancellationToken);

        Assert.NotEmpty(solutions);
        Assert.All(solutions, solution =>
        {
            Assert.True(
                solution.IsSolvable && solution.DeadEnds == 0 && solution.LoopingSlides == 0,
                $"Dead ends: {solution.DeadEnds}, looping slides: {solution.LoopingSlides}");
            Assert.InRange(Assert.NotNull(solution.Par), 3, 12);
        });
    }

    [Fact(Timeout = 10_000)]
    public async Task Map_has_the_twists_it_asked_for()
    {
        var map = (await Task.Run(
            () => Generated(new MapRequest(10, 1, 100, Twists: new TwistDensities(6, 4, 3, 2)), Seed()),
            TestContext.Current.CancellationToken)).Map;

        var tiles = Enumerable.Range(0, map.Rows * map.Columns).Select(i => map.TileAt(new Cell(i / map.Columns, i % map.Columns))).ToHashSet();
        Assert.Equal((true, true, true, true), (tiles.Contains(Tile.Wall), tiles.Contains(Tile.Sticky), tiles.Contains(Tile.Trap), map.Teleporters.Count > 0));
    }

    [Fact]
    public void No_generated_map_has_a_trap_on_the_start_or_the_end()
    {
        var generator = new MapGenerator(GeneratorConfigs.With());

        var maps = new Fixture().CreateMany<ulong>(30)
            .Select(seed => generator.Generate(new MapRequest(8, 1, 100, Twists: new TwistDensities(TrapPercent: 20)), seed))
            .OfType<GeneratedMap>()
            .Select(generated => generated.Map)
            .ToList();

        Assert.NotEmpty(maps);
        Assert.All(maps, map => Assert.Equal((Tile.None, Tile.None), (map.TileAt(map.Start), map.TileAt(map.End))));
    }

    [Theory]
    [AutoData]
    public void Zero_densities_give_the_plain_map(ulong seed)
    {
        var plain = Generated(new MapRequest(8, 1, 100), seed);

        var zeros = Generated(new MapRequest(8, 1, 100, Twists: new TwistDensities()), seed);

        Assert.Equal(Picture(plain), Picture(zeros));
    }

    [Theory(Timeout = 10_000)]
    [AutoData]
    public async Task One_seed_gives_one_twisted_map(ulong seed)
    {
        var request = new MapRequest(12, 1, 100, Bends: true, Twists: new TwistDensities(6, 4, 3, 2));

        var (first, second) = await Task.Run(
            () => (Picture(Generated(request, seed)), Picture(Generated(request, seed))), TestContext.Current.CancellationToken);

        Assert.Equal(first, second);
    }

    // Two free squares, both walled, so no try can be won
    [Fact(Timeout = 10_000)]
    public async Task Overfull_densities_never_throw()
    {
        var generator = new MapGenerator(GeneratorConfigs.With(maxAttempts: 5));

        var exception = await Task.Run(
            () => Record.Exception(() => generator.Generate(new MapRequest(2, 1, 100, Twists: new TwistDensities(100, 100, 100, 100)), Seed(), TestContext.Current.CancellationToken)),
            TestContext.Current.CancellationToken);

        Assert.Null(exception);
    }

    // Any change to the twist pass changes this
    [Fact(Timeout = 10_000)]
    public async Task Generated_map_with_twists_is_pinned()
    {
        var generated = await Task.Run(
            () => Generated(new MapRequest(8, 1, 100, Twists: new TwistDensities(10, 5, 5, 4)), 18), TestContext.Current.CancellationToken);

        Assert.Equal(PinnedTwists, Picture(generated));
    }

    // The plain pin's regions, then walls in runs of 2, 3 and 2, four sticky squares, four traps and two pairs
    private static readonly string[] PinnedTwists =
    [
        "AABBCDEE",
        "FGBBCDEE",
        "FGHICDJK",
        "FLHMNOJK",
        "FLHPNOJK",
        "FLHQROSK",
        "FTTTTTTK",
        "UUUUVWWK",
        ".....#..",
        "..x..#s.",
        "..s.....",
        "........",
        "...#s##x",
        "...#....",
        "..x#....",
        "...s.x..",
        "pairs [3,4]-[2,0] [6,6]-[7,0]",
        "start Cell { Row = 0, Column = 2 }",
        "end Cell { Row = 5, Column = 4 }",
        "par 5",
    ];

    private static ulong Seed() => new Fixture().Create<ulong>();

    // Any change to the random numbers, the placement or the cutting changes these, and with them every daily puzzle
    [Fact]
    public void Generated_map_is_pinned()
    {
        var generated = Generated(new MapRequest(8, 1, 100), 18);

        Assert.Equal(PinnedPlain, Picture(generated));
    }

    [Fact]
    public void Generated_map_with_bends_is_pinned()
    {
        var generated = Generated(new MapRequest(8, 1, 100, Bends: true), 18);

        Assert.Equal(PinnedBent, Picture(generated));
    }

    // Par 6 exactly is missed by the first four tries, so this pins that every try draws from the one stream
    [Fact]
    public void Generated_map_after_retries_is_pinned()
    {
        var generated = Generated(new MapRequest(8, 6, 6), 18);

        Assert.Equal(PinnedAfterRetries, Picture(generated));
    }

    private static readonly string[] PinnedAfterRetries =
    [
        "AAAAAAAB",
        "CCCCCDDB",
        "EFGGGHHB",
        "IIIIIIIJ",
        "KKKLMNNJ",
        "OOOLMPQR",
        "SSSLMTUR",
        "VVVVVVUR",
        "start Cell { Row = 2, Column = 1 }",
        "end Cell { Row = 6, Column = 5 }",
        "par 6",
    ];

    private static readonly string[] PinnedPlain =
    [
        "AABBCDEE",
        "FGBBCDEE",
        "FGHICDJK",
        "FLHMNOJK",
        "FLHPNOJK",
        "FLHQROSK",
        "FTTTTTTK",
        "UUUUVWWK",
        "start Cell { Row = 0, Column = 2 }",
        "end Cell { Row = 5, Column = 4 }",
        "par 3",
    ];

    // The same fill as the plain map, then C, G and R bent into an L, a zigzag and a T
    private static readonly string[] PinnedBent =
    [
        "AABBCDEE",
        "FGBBCDEE",
        "FGGCCDHI",
        "FJGKLMHI",
        "FJGNLMHI",
        "FJGOPMQI",
        "FRRRRRRI",
        "SSSSRTTI",
        "start Cell { Row = 0, Column = 2 }",
        "end Cell { Row = 5, Column = 4 }",
        "par 3",
    ];

    // With the test's default numbers; a request no map passes fails the assertion rather than throwing later
    private static GeneratedMap Generated(MapRequest request, ulong seed)
    {
        var generated = new MapGenerator(GeneratorConfigs.With()).Generate(request, seed);
        Assert.NotNull(generated);
        return generated;
    }

    // The region grid as rows of letters (A for region 1); on a map with twists, the tiles as rows of . # s x and the pairs
    // in order; then the start, the end and par
    private static string[] Picture(GeneratedMap generated)
    {
        var map = generated.Map;
        var rows = Enumerable.Range(0, map.Rows)
            .Select(row => new string(Enumerable.Range(0, map.Columns).Select(column => Letter(map.RegionAt(new Cell(row, column)))).ToArray()));
        string[] tiles = map.HasTiles
            ? Enumerable.Range(0, map.Rows)
                .Select(row => new string(Enumerable.Range(0, map.Columns).Select(column => TileLetter(map.TileAt(new Cell(row, column)))).ToArray()))
                .ToArray()
            : [];
        string[] pairs = map.Teleporters.Count > 0
            ? ["pairs " + string.Join(" ", map.Teleporters.Select(pair => $"[{pair.First.Row},{pair.First.Column}]-[{pair.Second.Row},{pair.Second.Column}]"))]
            : [];
        return [.. rows, .. tiles, .. pairs, $"start {map.Start}", $"end {map.End}", $"par {generated.Solution.Par}"];
    }

    private static char TileLetter(Tile tile) => tile switch
    {
        Tile.Wall => '#',
        Tile.Sticky => 's',
        Tile.Trap => 'x',
        _ => '.',
    };

    private static char Letter(int id) => (char)(id <= 26 ? 'A' + id - 1 : 'a' + id - 27);
}
