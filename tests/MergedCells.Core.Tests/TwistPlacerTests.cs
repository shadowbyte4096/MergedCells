using AutoFixture;
using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class TwistPlacerTests
{
    private static readonly Cell Corner = new(0, 0);

    private static Cell FarCorner(int size) => new(size - 1, size - 1);

    private static ulong Seed() => new Fixture().Create<ulong>();

    private static (Tile[,]? Tiles, TeleporterPair[] Teleporters) Place(int size, TwistDensities twists, ulong seed, int maxWallRun = 3) =>
        TwistPlacer.Place(size, Corner, FarCorner(size), twists, maxWallRun, new GameRandom(seed));

    private static List<Cell[]> Runs(int size, int count, int maxWallRun, ulong seed) =>
        TwistPlacer.WallRuns(new FreeSquares(size, Corner, FarCorner(size)), count, maxWallRun, new GameRandom(seed));

    private static List<Cell> Squares(Tile[,]? tiles, Tile tile)
    {
        var squares = new List<Cell>();
        if (tiles == null)
        {
            return squares;
        }
        for (var row = 0; row < tiles.GetLength(0); row++)
        {
            for (var column = 0; column < tiles.GetLength(1); column++)
            {
                if (tiles[row, column] == tile)
                {
                    squares.Add(new Cell(row, column));
                }
            }
        }
        return squares;
    }

    private static List<Tile> TwistTiles(Tile[,]? tiles) =>
        tiles == null ? [] : tiles.Cast<Tile>().Where(tile => tile != Tile.None).ToList();

    private static List<Cell> InReadingOrder(IEnumerable<Cell> cells) =>
        cells.OrderBy(cell => cell.Row).ThenBy(cell => cell.Column).ToList();

    [Fact]
    public void Holds_every_square_but_the_start_and_end()
    {
        var free = new FreeSquares(4, new Cell(0, 1), new Cell(3, 2));

        Assert.Equal((14, false, false, true), (free.Count, free.Contains(new Cell(0, 1)), free.Contains(new Cell(3, 2)), free.Contains(new Cell(2, 2))));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(4, 0)]
    [InlineData(0, 4)]
    public void A_square_off_the_map_is_not_free(int row, int column)
    {
        Assert.False(new FreeSquares(4, Corner, FarCorner(4)).Contains(new Cell(row, column)));
    }

    [Theory]
    [AutoData]
    public void A_taken_square_is_no_longer_free(ulong seed)
    {
        var free = new FreeSquares(4, Corner, FarCorner(4));

        var taken = free.Take(new GameRandom(seed));

        Assert.Equal((13, false), (free.Count, free.Contains(taken)));
    }

    [Fact]
    public void A_removed_square_is_no_longer_free()
    {
        var free = new FreeSquares(4, Corner, FarCorner(4));

        free.Remove(new Cell(1, 2));

        Assert.Equal((13, false), (free.Count, free.Contains(new Cell(1, 2))));
    }

    [Theory]
    [AutoData]
    public void No_twists_place_nothing(ulong seed)
    {
        var (tiles, teleporters) = Place(8, new TwistDensities(), seed);

        Assert.Equal((true, 0), (tiles == null, teleporters.Length));
    }

    [Theory]
    [AutoData]
    public void No_twists_draw_no_random_numbers(ulong seed)
    {
        var random = new GameRandom(seed);

        TwistPlacer.Place(8, Corner, FarCorner(8), new TwistDensities(), 3, random);

        Assert.Equal(new GameRandom(seed).NextUInt(), random.NextUInt());
    }

    // On a 10x10 map, so each percent is that many squares
    public static TheoryData<TwistDensities, Tile, int> TileCounts => new()
    {
        { new TwistDensities(WallPercent: 10), Tile.Wall, 10 },
        { new TwistDensities(StickyPercent: 7), Tile.Sticky, 7 },
        { new TwistDensities(TrapPercent: 3), Tile.Trap, 3 },
    };

    [Theory]
    [MemberData(nameof(TileCounts))]
    public void Places_the_density_s_count_of_a_tile(TwistDensities twists, Tile tile, int expected)
    {
        var (tiles, _) = Place(10, twists, Seed());

        Assert.Equal(Enumerable.Repeat(tile, expected), TwistTiles(tiles));
    }

    public static TheoryData<TwistDensities, Tile> OneTileOff => new()
    {
        { new TwistDensities(0, 10, 10, 10), Tile.Wall },
        { new TwistDensities(10, 0, 10, 10), Tile.Sticky },
        { new TwistDensities(10, 10, 0, 10), Tile.Trap },
    };

    [Theory]
    [MemberData(nameof(OneTileOff))]
    public void A_tile_at_0_is_not_placed(TwistDensities twists, Tile tile)
    {
        var (tiles, _) = Place(10, twists, Seed());

        Assert.Empty(Squares(tiles, tile));
    }

    [Theory]
    [AutoData]
    public void Teleporters_at_0_are_not_placed(ulong seed)
    {
        var (_, teleporters) = Place(10, new TwistDensities(10, 10, 10, 0), seed);

        Assert.Empty(teleporters);
    }

    // On a 10x10 map: the percent is that many teleporter squares, rounded up to whole pairs
    [Theory]
    [InlineAutoData(1, 1)]
    [InlineAutoData(3, 2)]
    [InlineAutoData(4, 2)]
    public void Pairs_for_a_density(int teleporterPercent, int pairs, ulong seed)
    {
        var (_, teleporters) = Place(10, new TwistDensities(TeleporterPercent: teleporterPercent), seed);

        Assert.Equal(pairs, teleporters.Length);
    }

    [Theory]
    [AutoData]
    public void Pairs_stop_at_45(ulong seed)
    {
        var (_, teleporters) = Place(20, new TwistDensities(TeleporterPercent: 100), seed);

        Assert.Equal(TeleporterLetters.Count, teleporters.Length);
    }

    // Walls come first and take all 7 free squares
    [Theory]
    [AutoData]
    public void Placement_stops_when_the_squares_run_out(ulong seed)
    {
        var (tiles, teleporters) = Place(3, new TwistDensities(100, 100, 100, 100), seed);

        Assert.Equal((7, 0, 0, 0), (Squares(tiles, Tile.Wall).Count, Squares(tiles, Tile.Sticky).Count, Squares(tiles, Tile.Trap).Count, teleporters.Length));
    }

    // 6 walls leave one of the 7 free squares, too few for a pair
    [Theory]
    [AutoData]
    public void No_pair_is_half_made(ulong seed)
    {
        var (_, teleporters) = Place(3, new TwistDensities(WallPercent: 60, TeleporterPercent: 100), seed);

        Assert.Empty(teleporters);
    }

    [Theory]
    [AutoData]
    public void Wall_runs_are_straight_and_unbroken(ulong seed)
    {
        Assert.All(Runs(12, 30, 4, seed), run => Assert.True(IsStraightLine(run), string.Join(" ", run)));
    }

    private static bool IsStraightLine(Cell[] run) =>
        run.Select((cell, i) => cell == new Cell(run[0].Row, run[0].Column + i)).All(inLine => inLine)
        || run.Select((cell, i) => cell == new Cell(run[0].Row + i, run[0].Column)).All(inLine => inLine);

    [Theory]
    [InlineAutoData(1)]
    [InlineAutoData(3)]
    public void Wall_runs_are_at_most_maxWallRun_long(int maxWallRun, ulong seed)
    {
        Assert.All(Runs(12, 30, maxWallRun, seed), run => Assert.InRange(run.Length, 1, maxWallRun));
    }

    [Theory]
    [AutoData]
    public void Some_wall_runs_are_longer_than_1(ulong seed)
    {
        Assert.Contains(Runs(12, 30, 3, seed), run => run.Length > 1);
    }

    [Theory]
    [AutoData]
    public void Wall_runs_add_up_to_the_count(ulong seed)
    {
        Assert.Equal(30, Runs(12, 30, 3, seed).Sum(run => run.Length));
    }

    [Theory]
    [AutoData]
    public void Wall_runs_never_share_a_square(ulong seed)
    {
        var squares = Runs(12, 30, 3, seed).SelectMany(run => run).ToList();

        Assert.Equal(squares.Count, squares.Distinct().Count());
    }

    [Theory]
    [AutoData]
    public void Wall_runs_use_only_free_squares(ulong seed)
    {
        var free = new FreeSquares(6, Corner, FarCorner(6));
        var random = new GameRandom(seed);
        var used = Enumerable.Range(0, 10).Select(_ => free.Take(random)).Append(Corner).Append(FarCorner(6)).ToHashSet();

        var runs = TwistPlacer.WallRuns(free, 20, 3, random);

        Assert.DoesNotContain(runs.SelectMany(run => run), used.Contains);
    }

    [Theory]
    [AutoData]
    public void A_run_longer_than_the_map_is_clipped(ulong seed)
    {
        var squares = Runs(4, 14, 100, seed).SelectMany(run => run);

        Assert.All(squares, cell => Assert.True(Map.IsInside(cell, 4, 4), cell.ToString()));
    }

    public static TheoryData<TwistDensities, Tile> SingleTiles => new()
    {
        { new TwistDensities(StickyPercent: 10), Tile.Sticky },
        { new TwistDensities(TrapPercent: 10), Tile.Trap },
    };

    // Each is one pick from the free squares, so they match a same-seeded list's first ten picks. Sticky squares that
    // land side by side join into one pad, which the developer is happy to see now and then
    [Theory]
    [MemberData(nameof(SingleTiles))]
    public void Sticky_squares_and_traps_are_single_picks(TwistDensities twists, Tile tile)
    {
        var seed = Seed();
        var free = new FreeSquares(10, Corner, FarCorner(10));
        var random = new GameRandom(seed);
        var picks = Enumerable.Range(0, 10).Select(_ => free.Take(random)).ToList();

        var (tiles, _) = Place(10, twists, seed);

        Assert.Equal(InReadingOrder(picks), Squares(tiles, tile));
    }

    [Theory]
    [AutoData]
    public void Teleporter_pairs_are_consecutive_picks(ulong seed)
    {
        var free = new FreeSquares(10, Corner, FarCorner(10));
        var random = new GameRandom(seed);
        var picks = Enumerable.Range(0, 6).Select(_ => free.Take(random)).ToList();

        var (_, teleporters) = Place(10, new TwistDensities(TeleporterPercent: 6), seed);

        Assert.Equal(Enumerable.Range(0, 3).Select(i => new TeleporterPair(picks[2 * i], picks[2 * i + 1])), teleporters);
    }

    // At 100% a twist takes every free square it can, so one placed on the start or the end would show
    public static TheoryData<TwistDensities> Full => new()
    {
        new TwistDensities(WallPercent: 100),
        new TwistDensities(StickyPercent: 100),
        new TwistDensities(TrapPercent: 100),
        new TwistDensities(TeleporterPercent: 100),
    };

    [Theory]
    [MemberData(nameof(Full))]
    public void No_twist_is_on_the_start_or_the_end(TwistDensities twists)
    {
        var random = new GameRandom(Seed());
        var (start, end) = StartAndEnd.Place(8, GeneratorConfigs.With(), random);

        var (tiles, teleporters) = TwistPlacer.Place(8, start, end, twists, 3, random);

        var tileOnStartOrEnd = tiles != null && (tiles[start.Row, start.Column] != Tile.None || tiles[end.Row, end.Column] != Tile.None);
        var pairOnStartOrEnd = teleporters.Any(pair => pair.First == start || pair.First == end || pair.Second == start || pair.Second == end);
        Assert.Equal((false, false), (tileOnStartOrEnd, pairOnStartOrEnd));
    }

    [Fact]
    public void Every_placement_is_a_valid_map()
    {
        var config = GeneratorConfigs.With();

        Assert.All(new Fixture().CreateMany<ulong>(50), seed =>
        {
            var random = new GameRandom(seed);
            var (start, end) = StartAndEnd.Place(10, config, random);
            var regions = RegionCutter.Cut(10, end, config, bends: true, random);

            var (tiles, teleporters) = TwistPlacer.Place(10, start, end, new TwistDensities(10, 10, 10, 10), 3, random);

            Assert.Empty(Map.Validate(regions, start, end, tiles, teleporters));
        });
    }
}
