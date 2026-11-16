namespace MergedCells.Core.Tests;

// Chapter 3 (walls), pinned by level ID. 26 Wall and 27 Blocked are the tutorials; 54 Off by One, 55 Staircase and 56
// Short Stop easy (0 to 19); 57 Bolt, 58 Ledge, 59 Arrow, 60 The Gap and 61 Spiral medium (20 to 39, the Spiral counted
// medium at the developer's call though it scores hard); 62 Bulwark, 63 Pillar Field and 64 Four Rooms hard (40 to 59);
// 65 Hairpin extreme (60 to 79); and 67 Maze, the developer's own drawing, scoring above that, closes the chapter (66 was
// its first drawing, retired when they redrew it to leave one best route)
public class ShippedChapter3Tests
{
    private const int Spiral = 61;
    private const int Maze = 67;

    private static readonly int[] Easy = [54, 55, 56];
    private static readonly int[] Medium = [57, 58, 59, 60, Spiral];
    private static readonly int[] Hard = [62, 63, 64];
    private static readonly int[] Extreme = [65, Maze];

    // About half the chapter uses walls as stoppers (the developer's rule): these stop against a wall on the best route
    private static readonly int[] WallStoppers = [26, 54, 55, 56, 59, Spiral, 63, 64, Maze];

    private static readonly GameConfig Config =
        GameConfig.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "game_config.json")));

    private static readonly DifficultyRater Rater = new(Config.Difficulty);

    private static Chapter Chapter() => ShippedCampaignTests.Campaign().Chapters[2];

    private static Level Level(int id) => Chapter().Levels.Single(level => level.Id == id);

    private static int Score(int id) => Rater.Score(Rater.Measure(Level(id).Map)!, DifficultyMode.Campaign);

    public static TheoryData<int> EveryLevel => new(Chapter().Levels.Select(level => level.Id));

    [Fact]
    public void The_chapter_holds_15_levels()
    {
        Assert.Equal(15, Chapter().Levels.Count);
    }

    [Fact]
    public void The_chapter_opens_with_the_two_tutorials_and_ends_with_the_Maze()
    {
        var ids = Chapter().Levels.Select(level => level.Id).ToArray();

        Assert.Equal([26, 27, Maze], [ids[0], ids[1], ids[^1]]);
    }

    [Theory]
    [InlineData(54, 0, 19)]
    [InlineData(55, 0, 19)]
    [InlineData(56, 0, 19)]
    [InlineData(57, 20, 39)]
    [InlineData(58, 20, 39)]
    [InlineData(59, 20, 39)]
    [InlineData(60, 20, 39)]
    [InlineData(62, 40, 59)]
    [InlineData(63, 40, 59)]
    [InlineData(64, 40, 59)]
    [InlineData(65, 60, 79)]
    public void Each_level_scores_inside_its_band(int id, int minScore, int maxScore)
    {
        Assert.InRange(Score(id), minScore, maxScore);
    }

    // The lookahead players never finish it, which is all that lifts it into hard; a person follows the spiral
    [Fact]
    public void Spiral_scores_hard_though_it_counts_as_medium()
    {
        Assert.InRange(Score(Spiral), 40, 59);
    }

    [Fact]
    public void Maze_scores_above_the_extreme_band()
    {
        Assert.True(Score(Maze) >= 80);
    }

    // The developer's rule for it: only one way to make par
    [Fact]
    public void Maze_has_one_best_route()
    {
        Assert.Equal(1L, Solver.Solve(Level(Maze).Map).OptimalRoutes);
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

    // Each is what promote worked out from the band's 1-star extra moves (2 for medium, 1 for hard and extreme)
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
    public void Every_level_relies_on_its_walls(int id)
    {
        Assert.True(TwistReliance.Of(Level(id).Map, Rater) >= 1);
    }

    [Theory]
    [MemberData(nameof(EveryLevel))]
    public void No_level_has_a_dead_end(int id)
    {
        Assert.Equal(0, Solver.Solve(Level(id).Map).DeadEnds);
    }

    [Theory]
    [MemberData(nameof(EveryLevel))]
    public void No_level_is_wider_or_taller_than_12(int id)
    {
        var map = Level(id).Map;

        Assert.True(map.Rows <= 12 && map.Columns <= 12, $"Level {id} is {map.Rows}×{map.Columns}");
    }

    public static TheoryData<int> Stoppers => new(WallStoppers);

    [Theory]
    [MemberData(nameof(Stoppers))]
    public void Each_wall_stop_level_stops_against_a_wall(int id)
    {
        var map = Level(id).Map;

        Assert.True(WallStops.Count(map, Solver.Solve(map).Path) >= 1);
    }

    [Fact]
    public void At_least_7_levels_stop_against_a_wall()
    {
        var count = Chapter().Levels.Count(level => WallStops.Count(level.Map, Solver.Solve(level.Map).Path) >= 1);

        Assert.True(count >= 7, $"{count} of the chapter's levels stop against a wall");
    }

    public static TheoryData<string> Bands => new("easy", "medium", "hard", "extreme");

    [Theory]
    [MemberData(nameof(Bands))]
    public void Every_band_has_a_wall_stop_level(string band)
    {
        var ids = band switch { "easy" => Easy, "medium" => Medium, "hard" => Hard, _ => Extreme };

        Assert.Contains(ids, id => WallStops.Count(Level(id).Map, Solver.Solve(Level(id).Map).Path) >= 1);
    }

    [Fact]
    public void Wall_s_first_line_is_the_reworded_one()
    {
        Assert.Equal("Walls stop you one square short", Level(26).Tutorial!.Lines[0]);
    }

    [Fact]
    public void The_chapter_card_s_first_line_is_the_reworded_one()
    {
        Assert.Equal("Walls stop a slide one square short.", Chapter().Intro[0]);
    }
}
