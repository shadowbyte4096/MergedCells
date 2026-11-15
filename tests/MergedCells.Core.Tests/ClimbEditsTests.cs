using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

// Each edit runs on one map with 200 random streams, and every result it applies is checked
public class ClimbEditsTests
{
    private const int Size = 10;

    public static TheoryData<int> Edits => new(Enum.GetValues<ClimbEdit>().Select(edit => (int)edit));

    private static Map Twisted(ulong seed) =>
        GeneratedMaps.Make(new MapRequest(Size, 1, 100, Bends: true, Twists: GeneratedMaps.EveryTwist), seed);

    private static IReadOnlyList<Cell> StartArea(Map map) => StartAndEnd.Starts(map.Rows, map.End, GeneratorConfigs.With());

    private static List<(ClimbMap Before, ClimbMap After)> Applied(ClimbEdit edit, Map map)
    {
        var startArea = StartArea(map);
        var results = new List<(ClimbMap, ClimbMap)>();
        for (ulong stream = 0; stream < 200; stream++)
        {
            var before = ClimbMap.From(map);
            var after = before.Copy();
            if (ClimbEdits.Apply(edit, after, new GameRandom(stream), startArea))
            {
                results.Add((before, after));
            }
        }
        Assert.NotEmpty(results);
        return results;
    }

    private static Task<List<(ClimbMap Before, ClimbMap After)>> AppliedAsync(ClimbEdit edit, Map map, CancellationToken token) =>
        Task.Run(() => Applied(edit, map), token);

    // True when every two squares sharing a region in the finer map share one in the coarser map
    private static bool Refines(ClimbMap finer, ClimbMap coarser)
    {
        var partner = new Dictionary<int, int>();
        foreach (var cell in Cells(finer))
        {
            var coarse = coarser.Regions[cell.Row, cell.Column];
            if (partner.TryGetValue(finer.Regions[cell.Row, cell.Column], out var seen) && seen != coarse)
            {
                return false;
            }
            partner[finer.Regions[cell.Row, cell.Column]] = coarse;
        }
        return true;
    }

    private static IEnumerable<Cell> Cells(ClimbMap map) =>
        Enumerable.Range(0, map.Size * map.Size).Select(i => new Cell(i / map.Size, i % map.Size));

    private static (int Walls, int Sticky, int Traps, int Pairs) Twists(ClimbMap map) =>
        (Cells(map).Count(cell => map.Tiles[cell.Row, cell.Column] == Tile.Wall),
            Cells(map).Count(cell => map.Tiles[cell.Row, cell.Column] == Tile.Sticky),
            Cells(map).Count(cell => map.Tiles[cell.Row, cell.Column] == Tile.Trap),
            map.Teleporters.Count);

    private static bool OnTeleporter(ClimbMap map, Cell cell) => map.Teleporters.Any(pair => pair.First == cell || pair.Second == cell);

    [Theory(Timeout = 10_000)]
    [AutoData]
    public async Task Split_cuts_a_region_into_connected_pieces_that_cover_it(ulong seed)
    {
        var results = await AppliedAsync(ClimbEdit.Split, Twisted(seed), TestContext.Current.CancellationToken);

        Assert.All(results, result =>
        {
            Assert.True(Refines(result.After, result.Before));
            Assert.True(result.After.RegionCount > result.Before.RegionCount);
            Assert.Empty(Map.Validate(result.After.Regions, result.After.Start, result.After.End));
        });
    }

    [Theory(Timeout = 10_000)]
    [AutoData]
    public async Task Merge_joins_two_neighbouring_regions_into_one(ulong seed)
    {
        var results = await AppliedAsync(ClimbEdit.Merge, Twisted(seed), TestContext.Current.CancellationToken);

        Assert.All(results, result =>
        {
            Assert.True(Refines(result.Before, result.After));
            Assert.Equal(result.Before.RegionCount - 1, result.After.RegionCount);
        });
    }

    [Theory(Timeout = 10_000)]
    [AutoData]
    public async Task Move_square_leaves_its_old_region_connected_or_gone(ulong seed)
    {
        var results = await AppliedAsync(ClimbEdit.MoveSquare, Twisted(seed), TestContext.Current.CancellationToken);

        Assert.All(results, result => Assert.DoesNotContain(
            Map.Validate(result.After.Regions, result.After.Start, result.After.End),
            problem => problem.Kind == MapProblemKind.RegionNotConnected));
    }

    [Theory(Timeout = 10_000)]
    [AutoData]
    public async Task Move_start_stays_in_the_start_area_and_off_twists(ulong seed)
    {
        var map = Twisted(seed);
        var startArea = StartArea(map);

        var results = await AppliedAsync(ClimbEdit.MoveStart, map, TestContext.Current.CancellationToken);

        Assert.All(results, result =>
        {
            var start = result.After.Start;
            Assert.Contains(start, startArea);
            Assert.NotEqual(result.Before.Start, start);
            Assert.Equal(Tile.None, result.After.Tiles[start.Row, start.Column]);
            Assert.False(OnTeleporter(result.After, start));
        });
    }

    // A run of three across in row 4: the start is above row 4 and the end right of column 3
    [Theory(Timeout = 10_000)]
    [AutoData]
    public async Task Wall_run_moves_whole(ulong seed)
    {
        var plain = GeneratedMaps.Make(new MapRequest(8, 1, 100), seed);
        var map = Map.Create(plain.RegionGrid(), plain.Start, plain.End, TestMaps.Walls(8, 8, (4, 0), (4, 1), (4, 2)));

        var results = await AppliedAsync(ClimbEdit.MoveTwist, map, TestContext.Current.CancellationToken);

        Assert.All(results, result =>
        {
            var walls = Cells(result.After).Where(cell => result.After.Tiles[cell.Row, cell.Column] == Tile.Wall).ToArray();
            Assert.Equal(3, walls.Length);
            Assert.All(walls, wall => Assert.Equal(walls[0].Row, wall.Row));
            Assert.Equal([walls[0].Column, walls[0].Column + 1, walls[0].Column + 2], walls.Select(wall => wall.Column));
        });
    }

    [Theory(Timeout = 10_000)]
    [AutoData]
    public async Task Teleporter_keeps_its_pair_s_place(ulong seed)
    {
        var map = GeneratedMaps.Make(new MapRequest(Size, 1, 100, Twists: new TwistDensities(TeleporterPercent: 4)), seed);

        var results = await AppliedAsync(ClimbEdit.MoveTwist, map, TestContext.Current.CancellationToken);

        Assert.All(results, result =>
        {
            var moved = result.Before.Teleporters.Zip(result.After.Teleporters)
                .Sum(pairs => (pairs.First.First != pairs.Second.First ? 1 : 0) + (pairs.First.Second != pairs.Second.Second ? 1 : 0));
            Assert.Equal(1, moved);
        });
    }

    [Theory(Timeout = 10_000)]
    [MemberData(nameof(Edits))]
    public async Task No_edit_changes_twist_counts(int edit)
    {
        var results = await AppliedAsync((ClimbEdit)edit, Twisted(3), TestContext.Current.CancellationToken);

        Assert.All(results, result => Assert.Equal(Twists(result.Before), Twists(result.After)));
    }

    [Theory(Timeout = 10_000)]
    [MemberData(nameof(Edits))]
    public async Task No_edit_touches_the_end_or_puts_a_twist_on_the_start(int edit)
    {
        var results = await AppliedAsync((ClimbEdit)edit, Twisted(3), TestContext.Current.CancellationToken);

        Assert.All(results, result =>
        {
            var after = result.After;
            var endRegion = after.Regions[after.End.Row, after.End.Column];
            Assert.Equal(1, Cells(after).Count(cell => after.Regions[cell.Row, cell.Column] == endRegion));
            Assert.Equal((Tile.None, Tile.None), (after.Tiles[after.End.Row, after.End.Column], after.Tiles[after.Start.Row, after.Start.Column]));
            Assert.False(OnTeleporter(after, after.Start));
        });
    }

    [Theory(Timeout = 10_000)]
    [MemberData(nameof(Edits))]
    public async Task Every_edited_map_is_valid(int edit)
    {
        var results = await AppliedAsync((ClimbEdit)edit, Twisted(3), TestContext.Current.CancellationToken);

        Assert.All(results, result => Assert.Empty(
            Map.Validate(result.After.Regions, result.After.Start, result.After.End, result.After.Tiles, result.After.Teleporters)));
    }
}
