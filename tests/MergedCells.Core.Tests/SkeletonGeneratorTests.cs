using AutoFixture.Xunit3;
using static MergedCells.Core.Tests.TestSkeletons;

namespace MergedCells.Core.Tests;

public class SkeletonGeneratorTests
{
    private static readonly MapRequest Request = new(7, 1, 30, Bends: true, Twists: new TwistDensities(10, 0, 0, 0));

    private static readonly MapGenerator Generator = new(GeneratorConfigs.With());

    private static Map Generate(MapSkeleton skeleton, ulong seed, MapGenerator? generator = null, MapRequest? request = null)
    {
        var generated = (generator ?? Generator).Generate(request ?? Request, seed, skeleton);
        Assert.SkipWhen(generated is null, $"No map for seed {seed}");
        return generated!.Map;
    }

    [Theory]
    [AutoData]
    public void Fixed_region_is_one_region_of_exactly_its_squares(ulong seed)
    {
        var map = Generate(Gap(), seed);
        var region = map.RegionAt(new(0, 2));

        Assert.Equal(
            Enumerable.Range(2, 5).Select(column => new Cell(0, column)),
            Squares(7).Where(cell => map.RegionAt(cell) == region));
    }

    [Theory]
    [AutoData]
    public void Fixed_walls_are_walls(ulong seed)
    {
        var map = Generate(Gap(), seed);

        Assert.All(GapWallColumns, column => Assert.Equal(Tile.Wall, map.TileAt(new(3, column))));
    }

    [Theory]
    [AutoData]
    public void Placed_walls_never_land_on_fixed_tiles_or_start_squares(ulong seed)
    {
        var tiles = GapTiles();
        for (var column = 0; column < 6; column++)
        {
            tiles[6, column] = Tile.None;
        }

        var map = Generate(Gap(tiles, [new(0, 0)]), seed);

        Assert.All(Enumerable.Range(0, 6).Select(column => new Cell(6, column)).Append(new Cell(0, 0)), cell => Assert.Equal(Tile.None, map.TileAt(cell)));
    }

    [Theory]
    [AutoData]
    public void Placed_walls_keep_their_count(ulong seed)
    {
        var map = Generate(Gap(), seed);

        Assert.Equal(GapWallColumns.Length + Percentage.Of(10, 49), Squares(7).Count(cell => map.TileAt(cell) == Tile.Wall));
    }

    [Theory]
    [AutoData]
    public void Start_is_one_of_the_skeletons_starts(ulong seed)
    {
        Cell[] starts = [new(0, 0), new(1, 0)];

        Assert.Contains(Generate(Gap(starts: starts), seed).Start, starts);
    }

    // At 20% the start area is the 2x2 corner, and three of its squares are walls
    [Theory]
    [AutoData]
    public void Start_avoids_fixed_walls_in_the_start_area(ulong seed)
    {
        var tiles = GapTiles();
        tiles[0, 0] = Tile.Wall;
        tiles[0, 1] = Tile.Wall;
        tiles[1, 0] = Tile.Wall;

        var map = Generate(Gap(tiles), seed, new MapGenerator(GeneratorConfigs.With(cornerPercent: 20)));

        Assert.Equal(new Cell(1, 1), map.Start);
    }

    [Fact]
    public void Start_area_with_no_square_for_the_start_throws()
    {
        var tiles = GapTiles();
        tiles[0, 0] = Tile.Wall;

        Assert.Throws<ArgumentException>(() => new MapGenerator(GeneratorConfigs.With(cornerPercent: 1)).Generate(Request, 1, Gap(tiles), TestContext.Current.CancellationToken));
    }

    // At 9x9 the start must be 8 rows plus columns from the end, and [3, 4] is only 7 from the far corner: the area is
    // empty for want of distance, not because of twists
    [Fact]
    public void Start_area_too_near_the_end_throws_saying_so()
    {
        var skeleton = new MapSkeleton(new int?[9, 9], new Tile?[9, 9], new Cell(3, 4), []);

        var problem = Assert.Throws<ArgumentException>(() => StartAndEnd.Starts(skeleton, GeneratorConfigs.With()));

        Assert.Contains("too near the top-left", problem.Message, StringComparison.Ordinal);
    }

    [Theory]
    [AutoData]
    public void Fixed_pairs_come_first_and_unchanged(ulong seed)
    {
        var map = Generate(Gap(teleporters: GapPairs), seed, request: PairRequest);

        Assert.Equal(GapPairs, map.Teleporters.Take(2));
    }

    [Theory]
    [AutoData]
    public void Placed_pairs_never_land_on_fixed_pair_squares(ulong seed)
    {
        var map = Generate(Gap(teleporters: GapPairs), seed, request: PairRequest);
        var fixedSquares = GapPairs.SelectMany(pair => new[] { pair.First, pair.Second }).ToHashSet();

        Assert.DoesNotContain(map.Teleporters.Skip(GapPairs.Length), pair => fixedSquares.Contains(pair.First) || fixedSquares.Contains(pair.Second));
    }

    [Theory]
    [AutoData]
    public void Start_never_lands_on_a_fixed_pair(ulong seed)
    {
        var map = Generate(Gap(teleporters: GapStartAreaPairs), seed, request: PairRequest);

        Assert.DoesNotContain(map.Start, GapStartAreaPairs.SelectMany(pair => new[] { pair.First, pair.Second }));
    }

    // Each start-area square paired with a square outside the area, so the area holds no plain square for the start
    [Fact]
    public void Start_area_covered_by_fixed_pairs_throws_saying_so()
    {
        var config = GeneratorConfigs.With();
        var end = new Cell(6, 6);
        var area = StartAndEnd.Starts(7, end, config);
        var outside = Squares(7).Where(cell => !area.Contains(cell) && cell != end).ToList();
        var skeleton = new MapSkeleton(
            new int?[7, 7], new Tile?[7, 7], end, [], area.Select((cell, i) => new TeleporterPair(cell, outside[i])).ToList());

        var problem = Assert.Throws<ArgumentException>(() => StartAndEnd.Starts(skeleton, config));

        Assert.Contains("holds a fixed twist", problem.Message, StringComparison.Ordinal);
    }

    [Theory]
    [AutoData]
    public void One_seed_gives_one_map(ulong seed)
    {
        var first = Generate(Gap(), seed);
        var second = Generate(Gap(), seed);

        Assert.Equal(LevelFingerprint.Of(first, 1), LevelFingerprint.Of(second, 1));
    }

    [Fact]
    public void Size_mismatch_throws()
    {
        Assert.Throws<ArgumentException>(() => Generator.Generate(Request with { Size = 8 }, 1, Gap(), TestContext.Current.CancellationToken));
    }
}
