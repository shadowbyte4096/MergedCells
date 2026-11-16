using AutoFixture.Xunit3;
using static MergedCells.Core.Tests.TestSkeletons;

namespace MergedCells.Core.Tests;

public class MapClimberSkeletonTests
{
    private static readonly MapRequest Request = new(7, 1, 30, Bends: true, Twists: new TwistDensities(10, 0, 0, 0));

    private static readonly MapClimber Climber =
        new(GeneratorConfigs.With(), DifficultyConfigs.With(), ClimbConfigs.With(maxSteps: 400, stallSteps: 100));

    private static async Task<Map> ClimbAsync(MapSkeleton skeleton, ulong seed, CancellationToken token, MapRequest? request = null)
    {
        var climbRequest = new ClimbRequest(request ?? Request, new DifficultyTarget(0, 100), DifficultyMode.Campaign, skeleton);
        var climbed = await Task.Run(() => Climber.Climb(climbRequest, seed), token);
        return climbed!.Map;
    }

    [Theory(Timeout = 30_000)]
    [AutoData]
    public async Task Fixed_regions_come_out_unchanged(ulong seed)
    {
        var map = await ClimbAsync(Gap(), seed, TestContext.Current.CancellationToken);
        var region = map.RegionAt(new(0, 2));

        Assert.Equal(Enumerable.Range(2, 5).Select(column => new Cell(0, column)), Squares(7).Where(cell => map.RegionAt(cell) == region));
    }

    [Theory(Timeout = 30_000)]
    [AutoData]
    public async Task Fixed_tiles_come_out_unchanged(ulong seed)
    {
        var map = await ClimbAsync(Gap(), seed, TestContext.Current.CancellationToken);

        Assert.All(GapWallColumns, column => Assert.Equal(Tile.Wall, map.TileAt(new(3, column))));
    }

    [Theory(Timeout = 30_000)]
    [AutoData]
    public async Task Fixed_traps_come_out_unchanged(ulong seed)
    {
        var tiles = GapTiles();
        tiles[5, 3] = Tile.Trap;

        var map = await ClimbAsync(Gap(tiles), seed, TestContext.Current.CancellationToken);

        Assert.Equal(Tile.Trap, map.TileAt(new(5, 3)));
    }

    [Theory(Timeout = 30_000)]
    [AutoData]
    public async Task Start_is_one_of_the_skeletons_starts(ulong seed)
    {
        Cell[] starts = [new(0, 0), new(1, 0), new(2, 0)];

        Assert.Contains((await ClimbAsync(Gap(starts: starts), seed, TestContext.Current.CancellationToken)).Start, starts);
    }

    [Theory(Timeout = 30_000)]
    [AutoData]
    public async Task Placed_wall_count_is_kept(ulong seed)
    {
        var map = await ClimbAsync(Gap(), seed, TestContext.Current.CancellationToken);

        Assert.Equal(GapWallColumns.Length + Percentage.Of(10, 49), Squares(7).Count(cell => map.TileAt(cell) == Tile.Wall));
    }

    [Theory(Timeout = 30_000)]
    [AutoData]
    public async Task One_seed_gives_one_map(ulong seed)
    {
        var first = await ClimbAsync(Gap(), seed, TestContext.Current.CancellationToken);
        var second = await ClimbAsync(Gap(), seed, TestContext.Current.CancellationToken);

        Assert.Equal(LevelFingerprint.Of(first, 1), LevelFingerprint.Of(second, 1));
    }

    [Fact]
    public void Size_mismatch_throws()
    {
        var request = new ClimbRequest(Request with { Size = 8 }, new DifficultyTarget(0, 100), DifficultyMode.Campaign, Gap());

        Assert.Throws<ArgumentException>(() => Climber.Climb(request, 1));
    }

    // Section 7's example with every square but the end fixed, and the start fixed, leaves the climber nothing to edit
    [Theory(Timeout = 30_000)]
    [AutoData]
    public async Task Fully_fixed_skeleton_with_a_fixed_start_gives_its_one_map(ulong seed)
    {
        var example = TestMaps.ExampleRegions();
        var regions = new int?[4, 4];
        foreach (var cell in Squares(4).Where(cell => cell != TestMaps.ExampleEnd))
        {
            regions[cell.Row, cell.Column] = example[cell.Row, cell.Column];
        }
        var skeleton = new MapSkeleton(regions, new Tile?[4, 4], TestMaps.ExampleEnd, [TestMaps.ExampleStart]);

        var map = await ClimbAsync(skeleton, seed, TestContext.Current.CancellationToken, new MapRequest(4, 1, 30));

        Assert.Equal(LevelFingerprint.Of(TestMaps.Example(), 3), LevelFingerprint.Of(map, 3));
    }
}
