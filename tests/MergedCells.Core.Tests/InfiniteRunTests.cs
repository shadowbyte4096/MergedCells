namespace MergedCells.Core.Tests;

// Par 6's 2-star limit is 9 under these stars; a window of 3 keeps the window cases short
public class InfiniteRunTests
{
    private static readonly InfiniteScoring Scoring = InfiniteConfigs.Scoring(efficiencyWindow: 3);

    private static readonly StarsConfig Stars = new()
    {
        TwoStarMinExtraMoves = 2,
        TwoStarExtraParPercent = 50,
        OneStarMinExtraMoves = 4,
        OneStarExtraParPercent = 100,
    };

    private static InfiniteRun NewRun() => new(Scoring, Stars);

    // Each map finished once, at par 6 unless given
    private static InfiniteRun RunOf(params int[] moves)
    {
        var run = NewRun();
        foreach (var count in moves)
        {
            run.StartMap(6);
            run.Finish(count);
        }
        return run;
    }

    [Fact]
    public void Streak_grows_within_the_limit()
    {
        Assert.Equal(2, RunOf(9, 6).Streak);
    }

    [Fact]
    public void Over_the_limit_breaks_the_streak()
    {
        Assert.Equal(0, RunOf(6, 6, 10).Streak);
    }

    [Fact]
    public void Over_the_limit_still_scores()
    {
        var run = RunOf(6, 6);
        var before = run.Score;
        run.StartMap(6);

        run.Finish(10);

        Assert.Equal(before + Scoring.Points(6, 10, 2), run.Score);
    }

    [Fact]
    public void Bonus_comes_from_the_streak_before()
    {
        var run = RunOf(6, 6);
        run.StartMap(6);

        Assert.Equal(Scoring.Points(6, 8, 2), run.Finish(8).KeptPoints);
    }

    [Fact]
    public void Give_up_breaks_the_streak()
    {
        var run = RunOf(6, 6);
        run.StartMap(6);

        run.GiveUp();

        Assert.Equal(0, run.Streak);
    }

    [Fact]
    public void Give_up_scores_nothing()
    {
        var run = RunOf(6, 6);
        var before = run.Score;
        run.StartMap(6);

        run.GiveUp();

        Assert.Equal(before, run.Score);
    }

    [Fact]
    public void Better_retry_counts()
    {
        var run = RunOf(6, 6);
        run.StartMap(6);
        run.Finish(10);

        var retry = run.Finish(7);

        Assert.Equal((true, (int?)10, 7), (retry.Counted, retry.PreviousMoves, retry.KeptMoves));
    }

    [Fact]
    public void Better_retry_mends_the_streak()
    {
        var run = RunOf(6, 6);
        run.StartMap(6);
        run.Finish(10);

        run.Finish(7);

        Assert.Equal(3, run.Streak);
    }

    [Fact]
    public void Better_retry_replaces_the_points()
    {
        var run = RunOf(6, 6);
        var before = run.Score;
        run.StartMap(6);
        run.Finish(10);

        run.Finish(7);

        Assert.Equal(before + Scoring.Points(6, 7, 2), run.Score);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(11)]
    public void Same_or_worse_retry_changes_nothing(int retryMoves)
    {
        var run = RunOf(6, 8);
        var before = (run.Score, run.Streak, run.WindowAverage, run.RunningAverage);

        var retry = run.Finish(retryMoves);

        Assert.Equal((false, 8, before), (retry.Counted, retry.KeptMoves, (run.Score, run.Streak, run.WindowAverage, run.RunningAverage)));
    }

    [Fact]
    public void Worse_retry_reports_this_finishs_efficiency()
    {
        var run = RunOf(8);

        Assert.Equal(Scoring.Efficiency(6, 11), run.Finish(11).Efficiency);
    }

    [Fact]
    public void First_finish_has_no_previous_moves()
    {
        var run = NewRun();
        run.StartMap(6);

        Assert.Null(run.Finish(8).PreviousMoves);
    }

    [Fact]
    public void Finish_reports_the_limit_and_the_streak_before()
    {
        var run = RunOf(6, 6);
        run.StartMap(6);

        var score = run.Finish(10);

        Assert.Equal((6, 10, 9, 2, true), (score.Par, score.Moves, score.StreakLimit, score.StreakBefore, score.OverLimit));
    }

    [Fact]
    public void Give_up_after_a_finish_keeps_the_finish()
    {
        var run = RunOf(6, 6);
        var before = (run.Score, run.Streak);

        run.GiveUp();

        Assert.Equal(before, (run.Score, run.Streak));
    }

    [Fact]
    public void Finish_after_a_give_up_replaces_it()
    {
        var run = RunOf(6);
        run.StartMap(6);
        run.GiveUp();

        var score = run.Finish(6);

        Assert.Equal((true, 2), (score.Counted, run.Streak));
    }

    [Fact]
    public void Map_with_no_outcome_counts_as_given_up_at_the_next_start()
    {
        var run = RunOf(6, 6);
        run.StartMap(6);

        run.StartMap(6);

        Assert.Equal((0, 3), (run.Streak, run.Maps));
    }

    [Fact]
    public void Finish_before_any_map_throws()
    {
        Assert.Throws<InvalidOperationException>(() => NewRun().Finish(6));
    }

    [Fact]
    public void Give_up_before_any_map_throws()
    {
        Assert.Throws<InvalidOperationException>(() => NewRun().GiveUp());
    }

    [Fact]
    public void Open_map_without_an_outcome_is_not_counted()
    {
        var run = RunOf(6);
        run.StartMap(6);

        Assert.Equal((1, 1), (run.Maps, run.Streak));
    }

    [Fact]
    public void Window_is_null_until_full()
    {
        Assert.Null(RunOf(6, 8).WindowAverage);
    }

    [Fact]
    public void Running_average_covers_every_map_so_far()
    {
        Assert.Equal(8750, RunOf(6, 8).RunningAverage);
    }

    [Fact]
    public void Running_average_is_zero_with_no_maps()
    {
        Assert.Equal(0, NewRun().RunningAverage);
    }

    [Fact]
    public void Window_averages_the_last_n_rounded_down()
    {
        var run = RunOf(6, 8);
        run.StartMap(2);
        run.Finish(3);

        Assert.Equal(8055, run.WindowAverage);
    }

    [Fact]
    public void Oldest_map_drops_out_of_the_window()
    {
        Assert.Equal(7500, RunOf(6, 8, 8, 8).WindowAverage);
    }

    [Fact]
    public void Give_up_counts_as_zero_in_the_window()
    {
        var run = RunOf(6, 6, 6);
        run.StartMap(6);

        run.GiveUp();

        Assert.Equal(6666, run.WindowAverage);
    }

    [Fact]
    public void Give_up_can_complete_the_window()
    {
        var run = RunOf(6, 6);
        run.StartMap(6);

        run.GiveUp();

        Assert.Equal(6666, run.WindowAverage);
    }

    [Fact]
    public void Totals_include_the_current_maps_result()
    {
        var run = RunOf(6);
        run.StartMap(6);

        run.Finish(8);

        Assert.Equal((Scoring.Points(6, 6, 0) + Scoring.Points(6, 8, 1), 2), (run.Score, run.Streak));
    }

    [Fact]
    public void Best_is_the_current_values()
    {
        var run = RunOf(6, 8, 7);

        Assert.Equal(new InfiniteBest(run.Streak, run.Score, run.WindowAverage), run.Best);
    }

    [Fact]
    public void Start_map_below_par_one_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NewRun().StartMap(0));
    }
}
