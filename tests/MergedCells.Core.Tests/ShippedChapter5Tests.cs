namespace MergedCells.Core.Tests;

// Chapter 5 (teleporters), pinned by level ID, in the developer's shape: 32 Teleport and 33 Carried Away are the
// tutorials; 81 Pinball (the developer's idea) and 83 Landing Pad easy (0 to 19); 85 Hijack, 86 Relay and 84 Which Way
// medium (20 to 39); 90 Ferry, 93 Courier, 91 Shuttle, 88 Switchboard and 87 Wrong Letter hard (40 to 59); 92 Wormhole
// and 89 Backflip extreme (60 to 79); and 82 The Web, the developer's checkerboard, insane (80 and up), closes it
public class ShippedChapter5Tests
{
    private const int Pinball = 81;
    private const int TheWeb = 82;

    private static readonly int[] Easy = [Pinball, 83];
    private static readonly int[] Medium = [85, 86, 84];
    private static readonly int[] Hard = [90, 93, 91, 88, 87];
    private static readonly int[] Extreme = [92, 89];

    // Every pair on these changes the map when taken off alone. Pinball's and The Web's pairs build their boards: with
    // both of Pinball's first moves winning, one pair off leaves the other winning, and two of The Web's twelve don't
    private static readonly int[] EveryPairMatters = [32, 33, 83, 85, 86, 84, 90, 93, 91, 88, 87, 92, 89];

    private static readonly GameConfig Config =
        GameConfig.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "game_config.json")));

    private static readonly DifficultyRater Rater = new(Config.Difficulty);

    private static Chapter Chapter() => ShippedCampaignTests.Campaign().Chapters[4];

    private static Level Level(int id) => Chapter().Levels.Single(level => level.Id == id);

    private static int Score(int id) => Rater.Score(Rater.Measure(Level(id).Map)!, DifficultyMode.Campaign);

    private static IEnumerable<Cell> Squares(Map map) =>
        Enumerable.Range(0, map.Rows * map.Columns).Select(index => new Cell(index / map.Columns, index % map.Columns));

    private static bool IsTeleporter(Map map, Cell cell) => map.Teleporters.Any(pair => pair.First == cell || pair.Second == cell);

    public static TheoryData<int> EveryLevel => new(Chapter().Levels.Select(level => level.Id));

    [Fact]
    public void The_chapter_holds_15_levels()
    {
        Assert.Equal(15, Chapter().Levels.Count);
    }

    [Fact]
    public void The_chapter_opens_with_the_two_tutorials_and_ends_with_The_Web()
    {
        var ids = Chapter().Levels.Select(level => level.Id).ToArray();

        Assert.Equal([32, 33, TheWeb], [ids[0], ids[1], ids[^1]]);
    }

    public static TheoryData<int, int, int> BandRows =>
        new(Easy.Select(id => (id, 0, 19))
            .Concat(Medium.Select(id => (id, 20, 39)))
            .Concat(Hard.Select(id => (id, 40, 59)))
            .Concat(Extreme.Select(id => (id, 60, 79))));

    [Theory]
    [MemberData(nameof(BandRows))]
    public void Each_level_scores_inside_its_band(int id, int minScore, int maxScore)
    {
        Assert.InRange(Score(id), minScore, maxScore);
    }

    [Fact]
    public void The_Web_scores_in_the_insane_band()
    {
        Assert.True(Score(TheWeb) >= 80);
    }

    public static TheoryData<int, int> BruteForceBars =>
        new(Medium.Select(id => (id, 2)).Concat(Hard.Select(id => (id, 3))).Concat(Extreme.Select(id => (id, 4))).Append((TheWeb, 5)));

    [Theory]
    [MemberData(nameof(BruteForceBars))]
    public void Levels_beat_brute_force_by_their_band_s_bar(int id, int bar)
    {
        var extra = Rater.Measure(Level(id).Map)!.BruteForceExtraMoves;

        Assert.True(extra is null || extra >= bar, $"Level {id}: brute force takes {extra} extra moves, the bar is {bar}");
    }

    public static TheoryData<int> HardAndHarder => new(Hard.Concat(Extreme).Append(TheWeb));

    [Theory]
    [MemberData(nameof(HardAndHarder))]
    public void Hard_and_harder_levels_move_away_at_least_once(int id)
    {
        Assert.True(Rater.Measure(Level(id).Map)!.AwayMoves >= 1);
    }

    // Each is what promote worked out from the band's 1-star extra moves (2 for medium, 1 for hard, extreme and insane)
    public static TheoryData<int, int> OneStarExtraMoves =>
        new(Medium.Select(id => (id, 2)).Concat(Hard.Concat(Extreme).Append(TheWeb).Select(id => (id, 1))));

    [Theory]
    [MemberData(nameof(OneStarExtraMoves))]
    public void Star_limits_are_the_reachable_ones(int id, int oneStarExtraMoves)
    {
        var level = Level(id);

        Assert.Equal(StarLimits.Reachable(level.Map, 10, 10, oneStarExtraMoves), Config.StarsFor(level));
    }

    [Theory]
    [MemberData(nameof(EveryLevel))]
    public void Every_level_relies_on_its_teleporters(int id)
    {
        Assert.True(TwistReliance.Of(Level(id).Map, Rater) >= 1);
    }

    [Theory]
    [MemberData(nameof(EveryLevel))]
    public void Every_level_has_pairs_and_no_tiles(int id)
    {
        var map = Level(id).Map;

        Assert.Equal((true, false), (map.Teleporters.Count > 0, map.HasTiles));
    }

    [Theory]
    [MemberData(nameof(EveryLevel))]
    public void No_level_has_a_dead_end(int id)
    {
        Assert.Equal(0, Solver.Solve(Level(id).Map).DeadEnds);
    }

    [Theory]
    [MemberData(nameof(EveryLevel))]
    public void No_level_is_wider_or_taller_than_14(int id)
    {
        var map = Level(id).Map;

        Assert.True(map.Rows <= 14 && map.Columns <= 14, $"Level {id} is {map.Rows}×{map.Columns}");
    }

    public static TheoryData<int> PairsThatAllMatter => new(EveryPairMatters);

    [Theory]
    [MemberData(nameof(PairsThatAllMatter))]
    public void Every_pair_matters_on_the_listed_levels(int id)
    {
        Assert.Empty(TwistReliance.PairsThatDontMatter(Level(id).Map, Rater));
    }

    // The developer asked that it win whichever way the player first swipes
    [Theory]
    [InlineData(Direction.Right)]
    [InlineData(Direction.Down)]
    public void Pinball_wins_from_either_first_move(Direction direction)
    {
        var map = Level(Pinball).Map;

        Assert.True(map.Slide(map.Start, direction).ReachedEnd);
    }

    [Fact]
    public void Pinball_is_one_block_covered_in_teleporters()
    {
        var map = Level(Pinball).Map;
        var rest = Squares(map).Where(cell => cell != map.Start && cell != map.End).ToList();

        Assert.Equal((true, 1), (rest.All(cell => IsTeleporter(map, cell)), rest.Append(map.Start).Select(map.RegionAt).Distinct().Count()));
    }

    [Fact]
    public void The_Web_is_a_checkerboard_of_single_squares()
    {
        var map = Level(TheWeb).Map;

        Assert.Equal(
            (map.Rows * map.Columns, true),
            (map.Rectangles.Count, Squares(map).All(cell => IsTeleporter(map, cell) == ((cell.Row + cell.Column) % 2 == 1))));
    }

    [Fact]
    public void The_Web_has_one_best_route()
    {
        Assert.Equal(1L, Solver.Solve(Level(TheWeb).Map).OptimalRoutes);
    }

    // The developer asked for the pairs as far apart as the board allows: rows and columns each add at most 40 here
    [Fact]
    public void The_Web_s_pairs_are_as_far_apart_as_they_can_be()
    {
        var pairs = Level(TheWeb).Map.Teleporters;

        Assert.Equal(80, pairs.Sum(pair => Math.Abs(pair.First.Row - pair.Second.Row) + Math.Abs(pair.First.Column - pair.Second.Column)));
    }
}
