namespace MergedCells.Core.Tests;

// Par 6's 2-star limit is 9 under these stars; a window of 3 keeps the window cases short
public class InfiniteCardTests
{
    private static readonly InfiniteScoring Scoring = InfiniteConfigs.Scoring(efficiencyWindow: 3);

    private static readonly StarsConfig Stars = new()
    {
        TwoStarMinExtraMoves = 2,
        TwoStarExtraParPercent = 50,
        OneStarMinExtraMoves = 4,
        OneStarExtraParPercent = 100,
    };

    // Every map but the last finished once at par 6; the last map's finishes in order, the card being the last one's
    private static (InfiniteRun Run, MapScore Score) Played(InfiniteScoring scoring, int[] earlier, params int[] last)
    {
        var run = new InfiniteRun(scoring, Stars);
        foreach (var moves in earlier)
        {
            run.StartMap(6);
            run.Finish(moves);
        }
        run.StartMap(6);
        MapScore score = null!;
        foreach (var moves in last)
        {
            score = run.Finish(moves);
        }
        return (run, score);
    }

    // The bests before the last finish as given, after it raised by the run's values, as Main merges them
    private static InfiniteCard Card((InfiniteRun Run, MapScore Score) played, InfiniteBest? before, InfiniteScoring? scoring = null) =>
        InfiniteCard.Of(played.Score, played.Run, before, (before ?? InfiniteBest.None).Max(played.Run.Best), scoring ?? Scoring);

    private static InfiniteBest High => new(99, 999_999, 10000);

    [Fact]
    public void Par_line_shows_the_efficiency()
    {
        Assert.Equal("Par 6 · 75% efficient", Card(Played(Scoring, [], 8), High).ParLine);
    }

    [Fact]
    public void Par_line_names_the_limit_when_over_it()
    {
        Assert.Equal("Par 6 · 54% efficient · streak at 9", Card(Played(Scoring, [], 11), High).ParLine);
    }

    [Fact]
    public void First_finish_has_no_best_line()
    {
        Assert.Null(Card(Played(Scoring, [], 8), High).BestLine);
    }

    [Fact]
    public void Better_retry_says_improved()
    {
        Assert.Equal("Improved · was 11 moves", Card(Played(Scoring, [], 11, 8), High).BestLine);
    }

    [Fact]
    public void Worse_retry_shows_the_counted_finish()
    {
        Assert.Equal("Your best · 8 moves", Card(Played(Scoring, [], 8, 11), High).BestLine);
    }

    [Fact]
    public void One_move_is_singular()
    {
        var run = new InfiniteRun(Scoring, Stars);
        run.StartMap(1);
        run.Finish(1);
        var score = run.Finish(3);

        Assert.Equal("Your best · 1 move", InfiniteCard.Of(score, run, High, High, Scoring).BestLine);
    }

    // As the headline and the HUD's moves line write them
    [Fact]
    public void Move_counts_have_no_thousands_separator()
    {
        var run = new InfiniteRun(Scoring, Stars);
        run.StartMap(500);
        run.Finish(1001);
        var score = run.Finish(1000);

        Assert.Equal("Improved · was 1001 moves", InfiniteCard.Of(score, run, High, High, Scoring).BestLine);
    }

    [Fact]
    public void Par_and_its_limit_have_no_thousands_separator()
    {
        var run = new InfiniteRun(Scoring, Stars);
        run.StartMap(1000);

        Assert.Equal("Par 1000 · 66% efficient · streak at 1500", InfiniteCard.Of(run.Finish(1501), run, High, High, Scoring).ParLine);
    }

    [Fact]
    public void Streak_note_says_broken()
    {
        Assert.Equal(new StatTile("Streak", "0", "broken · was 2", true), Card(Played(Scoring, [6, 6], 11), High).Streak);
    }

    [Fact]
    public void Streak_note_says_new_best()
    {
        Assert.Equal(new StatTile("Streak", "2", "New best", true), Card(Played(Scoring, [6], 6), new InfiniteBest(1, 999_999, 10000)).Streak);
    }

    [Fact]
    public void Streak_note_shows_the_best()
    {
        Assert.Equal(new StatTile("Streak", "2", "best 99", false), Card(Played(Scoring, [6], 6), High).Streak);
    }

    [Fact]
    public void Streak_over_the_limit_from_zero_shows_the_best()
    {
        Assert.Equal(new StatTile("Streak", "0", "best 99", false), Card(Played(Scoring, [], 11), High).Streak);
    }

    [Fact]
    public void Run_tile_shows_thousands_and_this_maps_points()
    {
        var scoring = InfiniteConfigs.Scoring(pointsPerPar: 100, efficiencyWindow: 3);

        Assert.Equal(new StatTile("Run", "1,260", "+660 this map", false), Card(Played(scoring, [6], 6), High, scoring).Run);
    }

    [Fact]
    public void Run_note_says_new_best()
    {
        Assert.Equal(new StatTile("Run", "60", "+60 · new best", true), Card(Played(Scoring, [], 6), null).Run);
    }

    [Fact]
    public void Worse_retry_over_the_limit_keeps_the_counted_streak()
    {
        Assert.Equal(new StatTile("Streak", "2", "best 99", false), Card(Played(Scoring, [6], 6, 11), High).Streak);
    }

    [Fact]
    public void Worse_retry_keeps_the_counted_points_in_the_run_tile()
    {
        Assert.Equal(new StatTile("Run", "45", "+45 this map", false), Card(Played(Scoring, [], 8, 11), High).Run);
    }

    [Fact]
    public void Window_tile_before_full()
    {
        Assert.Equal(new StatTile("Last 3", "2 of 3", "so far 87%", false), Card(Played(Scoring, [6], 8), High).Efficiency);
    }

    [Fact]
    public void Window_tile_full_with_new_best()
    {
        Assert.Equal(new StatTile("Last 3", "100%", "New best", true), Card(Played(Scoring, [6, 6], 6), null).Efficiency);
    }

    [Fact]
    public void Window_tile_full_shows_the_best()
    {
        Assert.Equal(new StatTile("Last 3", "83%", "best 94%", false), Card(Played(Scoring, [6, 8], 8), new InfiniteBest(0, 0, 9400)).Efficiency);
    }

    [Fact]
    public void Window_key_names_the_configured_window()
    {
        var scoring = InfiniteConfigs.Scoring(efficiencyWindow: 10);

        Assert.Equal("Last 10", Card(Played(scoring, [], 6), High, scoring).Efficiency.Key);
    }

    [Fact]
    public void Run_line_reads_score_and_streak()
    {
        var scoring = InfiniteConfigs.Scoring(pointsPerPar: 100, efficiencyWindow: 3);

        Assert.Equal("Run 1,260 · streak 2", InfiniteCard.RunLine(Played(scoring, [6], 6).Run));
    }

    [Fact]
    public void Run_line_of_a_new_run_reads_zeros()
    {
        Assert.Equal("Run 0 · streak 0", InfiniteCard.RunLine(new InfiniteRun(Scoring, Stars)));
    }

    [Fact]
    public void Setup_tiles_are_null_without_a_best()
    {
        Assert.Null(InfiniteCard.SetupTiles(null, Scoring));
    }

    [Fact]
    public void Setup_tiles_show_a_dash_without_an_efficiency()
    {
        Assert.Equal(new StatTile("Best 3", "–", "", false), InfiniteCard.SetupTiles(new InfiniteBest(7, 3410, null), Scoring)![2]);
    }

    [Fact]
    public void Setup_tiles_show_the_bests()
    {
        Assert.Equal(
            [new StatTile("Streak", "7", "", false), new StatTile("Run", "3,410", "", false), new StatTile("Best 10", "94%", "", false)],
            InfiniteCard.SetupTiles(new InfiniteBest(7, 3410, 9400), InfiniteConfigs.Scoring(efficiencyWindow: 10)));
    }
}
