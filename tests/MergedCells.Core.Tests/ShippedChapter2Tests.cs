namespace MergedCells.Core.Tests;

// Chapter 2 (irregular regions), pinned by level ID. By score band: 34 Bend and 35 Curl are the tutorials; 42, 43 and 52
// easy (0 to 19); 53, 46, 47, 48 and 36 Outer Ring medium (20 to 39); 9 Dogleg, 49 and 50 hard (40 to 59); 51 extreme
// (60 to 79); and 10 Drop In, scoring above that, closes the chapter
public class ShippedChapter2Tests
{
    private const int OuterRing = 36;
    private const int DropIn = 10;

    private static readonly GameConfig Config =
        GameConfig.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "game_config.json")));

    private static readonly DifficultyRater Rater = new(Config.Difficulty);

    private static Level Level(int id) => ShippedCampaignTests.Campaign().Levels.Single(level => level.Id == id);

    [Fact]
    public void Outer_Ring_has_par_8()
    {
        Assert.Equal(8, Level(OuterRing).Par);
    }

    [Fact]
    public void Outer_Ring_s_best_route_goes_round_the_outside()
    {
        Direction[] outside = [Direction.Down, Direction.Down, Direction.Up, Direction.Right, Direction.Up, Direction.Left, Direction.Left, Direction.Left];

        Assert.Equal(outside, Solver.Solve(Level(OuterRing).Map).Path);
    }

    [Fact]
    public void Outer_Ring_has_one_optimal_route()
    {
        Assert.Equal(1, Solver.Solve(Level(OuterRing).Map).OptimalRoutes);
    }

    [Fact]
    public void Outer_Ring_is_a_10_by_10_square()
    {
        var map = Level(OuterRing).Map;

        Assert.Equal((10, 10), (map.Rows, map.Columns));
    }

    [Theory]
    [InlineData(42, 0, 19)]
    [InlineData(43, 0, 19)]
    [InlineData(52, 0, 19)]
    [InlineData(53, 20, 39)]
    [InlineData(46, 20, 39)]
    [InlineData(47, 20, 39)]
    [InlineData(48, 20, 39)]
    [InlineData(OuterRing, 20, 39)]
    [InlineData(9, 40, 59)]
    [InlineData(49, 40, 59)]
    [InlineData(50, 40, 59)]
    [InlineData(51, 60, 79)]
    public void Each_level_scores_inside_its_band(int id, int minScore, int maxScore)
    {
        var score = Rater.Score(Rater.Measure(Level(id).Map)!, DifficultyMode.Campaign);

        Assert.InRange(score, minScore, maxScore);
    }

    [Fact]
    public void Drop_In_scores_above_the_extreme_band()
    {
        Assert.True(Rater.Score(Rater.Measure(Level(DropIn).Map)!, DifficultyMode.Campaign) >= 80);
    }

    [Theory]
    [InlineData(53, 2)]
    [InlineData(46, 2)]
    [InlineData(47, 2)]
    [InlineData(48, 2)]
    [InlineData(OuterRing, 3)]
    [InlineData(9, 3)]
    [InlineData(49, 3)]
    [InlineData(50, 3)]
    [InlineData(51, 4)]
    public void Medium_hard_and_extreme_levels_beat_brute_force_by_the_bar(int id, int bar)
    {
        var extra = Rater.Measure(Level(id).Map)!.BruteForceExtraMoves;

        Assert.True(extra is null || extra >= bar, $"Level {id}: brute force takes {extra} extra moves, the bar is {bar}");
    }

    // Each is what promote worked out from the band's 1-star extra moves (2 for medium, 1 for hard and extreme)
    [Theory]
    [InlineData(53, 2)]
    [InlineData(46, 2)]
    [InlineData(47, 2)]
    [InlineData(48, 2)]
    [InlineData(OuterRing, 1)]
    [InlineData(49, 1)]
    [InlineData(50, 1)]
    [InlineData(51, 1)]
    public void Star_limits_are_the_reachable_ones(int id, int oneStarExtraMoves)
    {
        var level = Level(id);

        Assert.Equal(StarLimits.Reachable(level.Map, 10, 10, oneStarExtraMoves), Config.StarsFor(level));
    }

    [Fact]
    public void The_chapter_holds_15_levels()
    {
        Assert.Equal(15, ShippedCampaignTests.Campaign().Chapters[1].Levels.Count);
    }

    [Fact]
    public void The_chapter_opens_with_the_two_tutorials_and_ends_with_Drop_In()
    {
        var ids = ShippedCampaignTests.Campaign().Chapters[1].Levels.Select(level => level.Id).ToArray();

        Assert.Equal([34, 35, DropIn], [ids[0], ids[1], ids[^1]]);
    }

    [Theory]
    [InlineData(34)]
    [InlineData(35)]
    public void Each_tutorial_has_text_lines_and_no_ghost_swipe(int id)
    {
        var tutorial = Level(id).Tutorial!;

        Assert.Equal((2, null), (tutorial.Lines.Count, tutorial.SwipeHint));
    }

    // An irregular region earns its place when cutting it into its rows, or into its columns, changes par, the decoys, the
    // brute-force excess or the away moves: a bend that only decorates the map changes none of them
    private static DifficultyMetrics? MeasureAfterCutting(Map map, int region, bool intoRows)
    {
        var grid = new int[map.Rows, map.Columns];
        var next = 1_000_000;
        for (var row = 0; row < map.Rows; row++)
        {
            for (var column = 0; column < map.Columns; column++)
            {
                var id = map.RegionAt(new Cell(row, column));
                var before = intoRows ? new Cell(row, column - 1) : new Cell(row - 1, column);
                var continues = id == region && map.Contains(before) && map.RegionAt(before) == region;
                grid[row, column] = id != region ? id : continues ? grid[before.Row, before.Column] : ++next;
            }
        }

        return Rater.Measure(Map.Create(grid, map.Start, map.End));
    }

    [Theory]
    [InlineData(34)]
    [InlineData(35)]
    [InlineData(42)]
    [InlineData(43)]
    [InlineData(52)]
    [InlineData(53)]
    [InlineData(46)]
    [InlineData(47)]
    [InlineData(48)]
    [InlineData(OuterRing)]
    [InlineData(9)]
    [InlineData(49)]
    [InlineData(50)]
    [InlineData(51)]
    [InlineData(DropIn)]
    public void Every_level_needs_an_irregular_region(int id)
    {
        var map = Level(id).Map;
        var metrics = Rater.Measure(map)!;
        var irregular = map.Rectangles.Where(rectangle => !map.IsRectangle(rectangle.Id)).Select(rectangle => rectangle.Id);

        bool Changes(DifficultyMetrics? cut) => cut is null || TwistReliance.Changes(metrics, cut) > 0;
        var needed = irregular.Count(region => Changes(MeasureAfterCutting(map, region, true)) || Changes(MeasureAfterCutting(map, region, false)));

        Assert.True(needed >= 1, $"Level {id}: cutting any irregular region into rows or columns changes nothing");
    }
}
