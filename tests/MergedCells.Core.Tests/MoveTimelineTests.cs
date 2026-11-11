namespace MergedCells.Core.Tests;

// The game's own numbers: 15 squares a second capped at 0.4 s, the trap's 0.2, 0.1 and 0.2, and the blink's 0.12 and 0.12
public class MoveTimelineTests
{
    private const double Tolerance = 1e-9;

    private static SlideConfig Config(double teleportShrink = 0.12) => new()
    {
        SquaresPerSecond = 15,
        MaxSeconds = 0.4,
        TrapShrinkSeconds = 0.2,
        TrapPauseSeconds = 0.1,
        TrapGrowSeconds = 0.2,
        TeleportShrinkSeconds = teleportShrink,
        TeleportGrowSeconds = 0.12,
    };

    // Runs as (from row, from column, to row, to column); the slide ends on `to`
    private static PlayerSlide Slide((int Row, int Column) to, params (int FromRow, int FromColumn, int ToRow, int ToColumn)[] runs) =>
        new(Direction.Right, runs.Select(run => new SlideRun(new(run.FromRow, run.FromColumn), new(run.ToRow, run.ToColumn))).ToArray(), new(to.Row, to.Column), false);

    private static MoveTimeline Timeline(PlayerSlide slide) => new(slide, Config());

    private static readonly PlayerSlide Plain = Slide((0, 3), (0, 0, 0, 3));

    // A trap at [0, 3] sends the player back to [0, 0]
    private static readonly PlayerSlide Trapped = Slide((0, 0), (0, 0, 0, 3));

    // Into [0, 2], out of [2, 1]: 8 squares, so the 0.4 s cap, 0.1 s and 0.3 s
    private static readonly PlayerSlide OneJump = Slide((2, 7), (0, 0, 0, 2), (2, 1, 2, 7));

    private static void AssertFrame((int Row, int Column) from, (int Row, int Column) to, double progress, double scale, bool sliding, MoveFrame frame)
    {
        Assert.Equal((new Cell(from.Row, from.Column), new Cell(to.Row, to.Column), sliding), (frame.From, frame.To, frame.Sliding));
        Assert.Equal(progress, frame.Progress, Tolerance);
        Assert.Equal(scale, frame.Scale, Tolerance);
    }

    [Fact]
    public void Plain_slide_takes_its_slide_time()
    {
        var timeline = Timeline(Plain);

        Assert.Equal((0.0, 0.2, 0.2), (timeline.RunStart(0), timeline.RunEnd(0), timeline.Seconds), new TupleTolerance());
    }

    [Fact]
    public void Plain_slide_mid_way_is_half_way_along_its_run()
    {
        AssertFrame((0, 0), (0, 3), 0.5, 1, true, Timeline(Plain).At(0.1));
    }

    [Fact]
    public void Plain_slide_after_its_time_is_on_its_stop()
    {
        AssertFrame((0, 3), (0, 3), 1, 1, false, Timeline(Plain).At(0.3));
    }

    [Fact]
    public void Trapped_slide_adds_the_trap_s_return()
    {
        Assert.Equal(0.7, Timeline(Trapped).Seconds, Tolerance);
    }

    // Chunk 15's frames: shrinking on the trap, hidden, then growing on the start
    [Theory]
    [InlineData(0.3, 0, 3, 0.75)]
    [InlineData(0.45, 0, 0, 0)]
    [InlineData(0.6, 0, 0, 0.75)]
    public void Trapped_slide_shrinks_on_the_trap_and_grows_on_the_start(double elapsed, int row, int column, double scale)
    {
        AssertFrame((row, column), (row, column), 1, scale, false, Timeline(Trapped).At(elapsed));
    }

    [Fact]
    public void Jump_puts_the_blink_between_the_runs()
    {
        var timeline = Timeline(OneJump);

        Assert.Equal(
            (0.0, 0.1, 0.34, 0.64, 0.64),
            (timeline.RunStart(0), timeline.RunEnd(0), timeline.RunStart(1), timeline.RunEnd(1), timeline.Seconds),
            new TupleTolerance());
    }

    [Fact]
    public void Jump_shrinks_the_player_on_the_teleporter()
    {
        AssertFrame((0, 2), (0, 2), 1, 0.75, false, Timeline(OneJump).At(0.16));
    }

    [Fact]
    public void Jump_grows_the_player_on_the_partner()
    {
        AssertFrame((2, 1), (2, 1), 1, 0.75, false, Timeline(OneJump).At(0.28));
    }

    [Fact]
    public void After_a_jump_the_player_slides_along_the_next_run()
    {
        AssertFrame((2, 1), (2, 7), 0.5, 1, true, Timeline(OneJump).At(0.49));
    }

    [Fact]
    public void Wait_moves_every_later_time()
    {
        var timeline = Timeline(OneJump).WithWait(0, 0.5);

        Assert.Equal((0.72, 1.02, 1.02), (timeline.RunStart(1), timeline.RunEnd(1), timeline.Seconds), new TupleTolerance());
    }

    [Fact]
    public void Player_is_hidden_while_the_camera_catches_up()
    {
        Assert.Equal(0, Timeline(OneJump).WithWait(0, 0.5).At(0.3).Scale, Tolerance);
    }

    [Fact]
    public void With_wait_leaves_the_original_unchanged()
    {
        var timeline = Timeline(OneJump);

        _ = timeline.WithWait(0, 0.5);

        Assert.Equal(0.64, timeline.Seconds, Tolerance);
    }

    // 1, 2 and 3 squares: 6 squares, so the cap, a sixth, a third and a half of 0.4 s
    private static readonly PlayerSlide TwoJumps = Slide((3, 3), (0, 0, 0, 1), (1, 0, 1, 2), (3, 0, 3, 3));

    [Fact]
    public void Two_jumps_put_a_blink_after_each_of_the_first_two_runs()
    {
        var timeline = Timeline(TwoJumps);

        Assert.Equal(
            (0.4 / 6 + 0.24, 0.4 / 2 + 0.24, 0.4 / 2 + 0.48, 0.88),
            (timeline.RunStart(1), timeline.RunEnd(1), timeline.RunStart(2), timeline.Seconds),
            new TupleTolerance());
    }

    [Fact]
    public void Wait_on_the_second_jump_moves_only_what_follows_it()
    {
        var before = Timeline(TwoJumps);
        var after = before.WithWait(1, 0.5);

        Assert.Equal(
            (before.RunStart(1), before.RunEnd(1), before.RunStart(2) + 0.38, before.Seconds + 0.38),
            (after.RunStart(1), after.RunEnd(1), after.RunStart(2), after.Seconds),
            new TupleTolerance());
    }

    // Out of [2, 4] beside the edge: the last run has no squares, so the move ends as the player grows out
    [Fact]
    public void Exit_next_to_the_edge_ends_on_the_exit()
    {
        var timeline = Timeline(Slide((2, 4), (0, 0, 0, 2), (2, 4, 2, 4)));

        Assert.Equal((timeline.RunStart(1), timeline.RunEnd(1)), (timeline.Seconds, timeline.Seconds), new TupleTolerance());
        AssertFrame((2, 4), (2, 4), 1, 1, false, timeline.At(timeline.Seconds));
    }

    // Right into [1, 3], out of [1, 0], and on to [1, 1], where the move began: it still plays
    [Fact]
    public void Round_trip_takes_its_slide_time_and_the_blink()
    {
        var timeline = Timeline(Slide((1, 1), (1, 1, 1, 3), (1, 0, 1, 1)));

        Assert.Equal(0.44, timeline.Seconds, Tolerance);
        AssertFrame((1, 1), (1, 1), 1, 1, false, timeline.At(0.44));
    }

    // Out of [2, 1] and into a trap at [2, 3], back to the start at [0, 0]
    [Fact]
    public void Trap_after_a_jump_returns_the_player_after_the_last_run()
    {
        var timeline = Timeline(Slide((0, 0), (0, 0, 0, 2), (2, 1, 2, 3)));

        Assert.Equal(timeline.RunEnd(1) + 0.5, timeline.Seconds, Tolerance);
        AssertFrame((2, 3), (2, 3), 1, 0.75, false, timeline.At(timeline.RunEnd(1) + 0.1));
    }

    [Fact]
    public void Plain_slide_has_no_events()
    {
        Assert.Empty(Timeline(Plain).Events);
    }

    [Fact]
    public void Trapped_slide_is_caught_at_the_end_of_its_run()
    {
        Assert.Equal([new MoveEvent(MoveEventKind.Catch, 0, 0.2)], Timeline(Trapped).Events, new EventTolerance());
    }

    [Fact]
    public void Jump_events_fall_on_the_runs_times()
    {
        var timeline = Timeline(OneJump);

        Assert.Equal(
            [new MoveEvent(MoveEventKind.ReachTeleporter, 0, timeline.RunEnd(0)), new MoveEvent(MoveEventKind.BeginRun, 1, timeline.RunStart(1))],
            timeline.Events,
            new EventTolerance());
    }

    // 10, 1 and 19 squares: the middle run lasts 0.4 / 30 s, less than a 60 fps frame, so one frame can pass both its
    // start and its end. Its own start must still come before its teleporter
    [Fact]
    public void Each_teleporter_is_reached_after_the_run_that_leads_to_it_begins()
    {
        var timeline = Timeline(Slide((2, 19), (0, 0, 0, 10), (1, 0, 1, 1), (2, 0, 2, 19)));

        Assert.Equal(
            [(MoveEventKind.ReachTeleporter, 0), (MoveEventKind.BeginRun, 1), (MoveEventKind.ReachTeleporter, 1), (MoveEventKind.BeginRun, 2)],
            timeline.Events.Select(e => (e.Kind, e.Index)));
    }

    [Fact]
    public void Wait_moves_the_events_after_its_jump()
    {
        var timeline = Timeline(OneJump).WithWait(0, 0.5);

        Assert.Equal(
            [new MoveEvent(MoveEventKind.ReachTeleporter, 0, 0.1), new MoveEvent(MoveEventKind.BeginRun, 1, 0.72)],
            timeline.Events,
            new EventTolerance());
    }

    [Fact]
    public void Before_the_start_is_the_first_run_s_start()
    {
        AssertFrame((0, 0), (0, 3), 0, 1, true, Timeline(Plain).At(-1));
    }

    [Fact]
    public void Past_the_end_is_on_To_at_full_size()
    {
        AssertFrame((0, 0), (0, 0), 1, 1, false, Timeline(Trapped).At(5));
    }

    // A config whose blink times are out of range still plays slides that don't jump
    [Fact]
    public void Slide_without_a_jump_ignores_the_blink_times()
    {
        Assert.Null(Record.Exception(() => new MoveTimeline(Plain, Config(teleportShrink: 0))));
    }

    [Fact]
    public void Slide_with_a_jump_checks_the_blink_times()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MoveTimeline(OneJump, Config(teleportShrink: 0)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void Wait_on_a_jump_that_doesn_t_exist_throws(int jump)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Timeline(OneJump).WithWait(jump, 0));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Bad_wait_throws(double wait)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Timeline(OneJump).WithWait(0, wait));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void Run_that_doesn_t_exist_throws(int run)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Timeline(OneJump).RunStart(run));
    }

    [Fact]
    public void No_config_throws()
    {
        Assert.Throws<ArgumentNullException>(() => new MoveTimeline(Plain, null!));
    }

    private sealed class EventTolerance : IEqualityComparer<MoveEvent>
    {
        public bool Equals(MoveEvent x, MoveEvent y) => (x.Kind, x.Index) == (y.Kind, y.Index) && Math.Abs(x.At - y.At) <= Tolerance;

        public int GetHashCode(MoveEvent obj) => HashCode.Combine(obj.Kind, obj.Index);
    }

    private sealed class TupleTolerance :
        IEqualityComparer<(double, double, double)>,
        IEqualityComparer<(double, double)>,
        IEqualityComparer<(double, double, double, double)>,
        IEqualityComparer<(double, double, double, double, double)>
    {
        private static bool Near(double a, double b) => Math.Abs(a - b) <= Tolerance;

        public bool Equals((double, double) x, (double, double) y) => Near(x.Item1, y.Item1) && Near(x.Item2, y.Item2);

        public bool Equals((double, double, double) x, (double, double, double) y) =>
            Near(x.Item1, y.Item1) && Near(x.Item2, y.Item2) && Near(x.Item3, y.Item3);

        public bool Equals((double, double, double, double) x, (double, double, double, double) y) =>
            Near(x.Item1, y.Item1) && Near(x.Item2, y.Item2) && Near(x.Item3, y.Item3) && Near(x.Item4, y.Item4);

        public bool Equals((double, double, double, double, double) x, (double, double, double, double, double) y) =>
            Near(x.Item1, y.Item1) && Near(x.Item2, y.Item2) && Near(x.Item3, y.Item3) && Near(x.Item4, y.Item4) && Near(x.Item5, y.Item5);

        public int GetHashCode((double, double) obj) => 0;

        public int GetHashCode((double, double, double) obj) => 0;

        public int GetHashCode((double, double, double, double) obj) => 0;

        public int GetHashCode((double, double, double, double, double) obj) => 0;
    }
}
