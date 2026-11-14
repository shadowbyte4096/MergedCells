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
        var config = GeneratorConfigs.With(0, 0, 0, 1, maxStripPercent: 1, maxBlockSide: 2, bendPercent: 0, maxBendSquares: 3, cornerPercent: 100, minDistancePercent: 0, maxAttempts: 1);

        Assert.Null(Record.Exception(() => new MapGenerator(config)));
    }

    [Fact]
    public void Accepts_the_largest_block_side()
    {
        Assert.Null(Record.Exception(() => new MapGenerator(GeneratorConfigs.With(maxBlockSide: 10))));
    }

    [Theory]
    [InlineAutoData(1, 1, 1)]
    [InlineAutoData(5, 0, 5)]
    [InlineAutoData(5, 4, 3)]
    public void Rejects_an_invalid_request(int size, int minPar, int maxPar, ulong seed)
    {
        var generator = new MapGenerator(GeneratorConfigs.With());

        Assert.Throws<ArgumentOutOfRangeException>(() => generator.Generate(new MapRequest(size, minPar, maxPar), seed));
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
    public void An_impossible_par_range_gives_null(ulong seed)
    {
        var generator = new MapGenerator(GeneratorConfigs.With(maxAttempts: 20));

        Assert.Null(generator.Generate(new MapRequest(5, 100, 200), seed));
    }

    [Theory]
    [AutoData]
    public void Generates_a_100x100_map(ulong seed)
    {
        var generated = new MapGenerator(GeneratorConfigs.With()).Generate(new MapRequest(100, 1, 10_000), seed);

        Assert.NotNull(generated);
    }

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

    // The region grid as rows of letters (A for region 1), then the start, the end and par
    private static string[] Picture(GeneratedMap generated)
    {
        var map = generated.Map;
        var rows = Enumerable.Range(0, map.Rows)
            .Select(row => new string(Enumerable.Range(0, map.Columns).Select(column => Letter(map.RegionAt(new Cell(row, column)))).ToArray()));
        return [.. rows, $"start {map.Start}", $"end {map.End}", $"par {generated.Solution.Par}"];
    }

    private static char Letter(int id) => (char)(id <= 26 ? 'A' + id - 1 : 'a' + id - 27);
}
