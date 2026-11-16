namespace MergedCells.Core.Tests;

// Chapter 4 (traps), pinned by level ID. 30 Trap and 31 Start Over are the tutorials; 68 Bait and 77 Twin Lanes easy (0
// to 19); 74 Snare, 76 Deadfall, 78 Near Miss, 75 Pitfall, 70 Landing Zone and 69 Tripwire medium (20 to 39: two easy
// and six medium, the developer's choice, so the chapter keeps getting harder); 79 Gauntlet, 71 Minefield and 72 Back
// Door hard (40 to 59); 73 Moat extreme (60 to 79); and 80 Cage, the developer's shape, scoring above that, closes it
public class ShippedChapter4Tests
{
    private const int NearMiss = 78;
    private const int Cage = 80;

    private static readonly int[] Easy = [68, 77];
    private static readonly int[] Medium = [74, 76, NearMiss, 75, 70, 69];
    private static readonly int[] Hard = [79, 71, 72];
    private static readonly int[] Extreme = [73, Cage];

    // Every trap on these changes the map when taken off alone. The Cage's 31 traps are drawn for its shape, and most of
    // them don't
    private static readonly int[] EveryTrapMatters = [30, 31, 68, 77, 74, 76, NearMiss, 75, 70, 69, 79, 71, 72, 73];

    private static readonly GameConfig Config =
        GameConfig.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "game_config.json")));

    private static readonly DifficultyRater Rater = new(Config.Difficulty);

    private static Chapter Chapter() => ShippedCampaignTests.Campaign().Chapters[3];

    private static Level Level(int id) => Chapter().Levels.Single(level => level.Id == id);

    private static int Score(int id) => Rater.Score(Rater.Measure(Level(id).Map)!, DifficultyMode.Campaign);

    public static TheoryData<int> EveryLevel => new(Chapter().Levels.Select(level => level.Id));

    [Fact]
    public void The_chapter_holds_15_levels()
    {
        Assert.Equal(15, Chapter().Levels.Count);
    }

    [Fact]
    public void The_chapter_opens_with_the_two_tutorials_and_ends_with_the_Cage()
    {
        var ids = Chapter().Levels.Select(level => level.Id).ToArray();

        Assert.Equal([30, 31, Cage], [ids[0], ids[1], ids[^1]]);
    }

    public static TheoryData<int, int, int> BandRows =>
        new(Easy.Select(id => (id, 0, 19))
            .Concat(Medium.Select(id => (id, 20, 39)))
            .Concat(Hard.Select(id => (id, 40, 59)))
            .Append((73, 60, 79)));

    [Theory]
    [MemberData(nameof(BandRows))]
    public void Each_level_scores_inside_its_band(int id, int minScore, int maxScore)
    {
        Assert.InRange(Score(id), minScore, maxScore);
    }

    [Fact]
    public void Cage_scores_above_the_extreme_band()
    {
        Assert.True(Score(Cage) >= 80);
    }

    // Left and right round the corridor look alike, but the left lane owns the bottom-left corner and costs one more
    // move, so the way round is a decision
    [Fact]
    public void Cage_has_one_best_route()
    {
        Assert.Equal(1L, Solver.Solve(Level(Cage).Map).OptimalRoutes);
    }

    public static TheoryData<int, int> BruteForceBars =>
        new(Medium.Select(id => (id, 2)).Concat(Hard.Select(id => (id, 3))).Concat(Extreme.Select(id => (id, 4))));

    [Theory]
    [MemberData(nameof(BruteForceBars))]
    public void Medium_hard_and_extreme_levels_beat_brute_force_by_the_bar(int id, int bar)
    {
        var extra = Rater.Measure(Level(id).Map)!.BruteForceExtraMoves;

        Assert.True(extra is null || extra >= bar, $"Level {id}: brute force takes {extra} extra moves, the bar is {bar}");
    }

    public static TheoryData<int> HardAndExtreme => new(Hard.Concat(Extreme));

    [Theory]
    [MemberData(nameof(HardAndExtreme))]
    public void Hard_and_extreme_levels_move_away_at_least_once(int id)
    {
        Assert.True(Rater.Measure(Level(id).Map)!.AwayMoves >= 1);
    }

    // Each is what promote worked out from the band's 1-star extra moves (2 for medium, 1 for hard, extreme and insane)
    public static TheoryData<int, int> OneStarExtraMoves =>
        new(Medium.Select(id => (id, 2)).Concat(Hard.Concat(Extreme).Select(id => (id, 1))));

    [Theory]
    [MemberData(nameof(OneStarExtraMoves))]
    public void Star_limits_are_the_reachable_ones(int id, int oneStarExtraMoves)
    {
        var level = Level(id);

        Assert.Equal(StarLimits.Reachable(level.Map, 10, 10, oneStarExtraMoves), Config.StarsFor(level));
    }

    [Theory]
    [MemberData(nameof(EveryLevel))]
    public void Every_level_relies_on_its_traps(int id)
    {
        Assert.True(TwistReliance.Of(Level(id).Map, Rater) >= 1);
    }

    [Theory]
    [MemberData(nameof(EveryLevel))]
    public void Every_level_has_traps_and_no_other_twist(int id)
    {
        var map = Level(id).Map;
        var tiles = Enumerable.Range(0, map.Rows * map.Columns).Select(index => map.TileAt(new Cell(index / map.Columns, index % map.Columns))).ToArray();

        Assert.Equal(
            (true, false, 0),
            (tiles.Contains(Tile.Trap), tiles.Any(tile => tile is Tile.Wall or Tile.Sticky), map.Teleporters.Count));
    }

    [Theory]
    [MemberData(nameof(EveryLevel))]
    public void No_level_has_a_dead_end(int id)
    {
        Assert.Equal(0, Solver.Solve(Level(id).Map).DeadEnds);
    }

    [Theory]
    [MemberData(nameof(EveryLevel))]
    public void No_level_is_wider_or_taller_than_13(int id)
    {
        var map = Level(id).Map;

        Assert.True(map.Rows <= 13 && map.Columns <= 13, $"Level {id} is {map.Rows}×{map.Columns}");
    }

    public static TheoryData<int> TrapsThatAllMatter => new(EveryTrapMatters);

    [Theory]
    [MemberData(nameof(TrapsThatAllMatter))]
    public void Every_trap_matters_on_the_listed_levels(int id)
    {
        Assert.Empty(TwistReliance.TrapsThatDontMatter(Level(id).Map, Rater));
    }

    // The developer's rule for it: going most of the way along the trapped route, with a small detour, is still par
    [Fact]
    public void Near_Miss_costs_one_or_two_moves_over_its_trap_free_par()
    {
        var map = Level(NearMiss).Map;

        Assert.InRange(Solver.Solve(map).Par!.Value - Solver.Solve(TwistReliance.WithoutTwists(map)).Par!.Value, 1, 2);
    }
}
