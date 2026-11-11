using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

// TestMaps.Example: Down goes to [1, 0], Down again to [3, 0], then Right wins on [3, 3]
public class PlaySessionTests
{
    private static readonly Cell Start = TestMaps.ExampleStart;

    private static PlaySession New() => new(TestMaps.Example());

    private static PlaySession MidSlideWithARememberedMove()
    {
        var session = New();
        session.Request(Direction.Down);
        session.Request(Direction.Down);
        return session;
    }

    private static PlaySession WinningSlideInFlight()
    {
        var session = New();
        session.Request(Direction.Down);
        session.FinishSlide();
        session.Request(Direction.Down);
        session.FinishSlide();
        session.Request(Direction.Right);
        return session;
    }

    private static PlaySession Finished()
    {
        var session = WinningSlideInFlight();
        session.FinishSlide();
        return session;
    }

    [Fact]
    public void Request_with_no_slide_in_flight_returns_its_slide_and_counts_it()
    {
        var session = New();

        var slide = session.Request(Direction.Down);

        PlayerSlide? expected = new PlayerSlide(Direction.Down, Start, new Cell(1, 0), new Cell(1, 0), false);
        Assert.Equal((expected, 1, expected), (slide, session.Moves, session.Current));
    }

    [Theory]
    [InlineData(Direction.Up)]
    [InlineData(Direction.Left)]
    public void No_op_request_returns_null_and_is_not_counted(Direction direction)
    {
        var session = New();

        var slide = session.Request(direction);

        Assert.Equal(((PlayerSlide?)null, 0, (PlayerSlide?)null), (slide, session.Moves, session.Current));
    }

    [Fact]
    public void Request_during_a_slide_returns_null()
    {
        var session = New();
        session.Request(Direction.Down);

        Assert.Null(session.Request(Direction.Down));
    }

    [Fact]
    public void Request_during_a_slide_starts_when_the_slide_finishes()
    {
        var session = MidSlideWithARememberedMove();

        var next = session.FinishSlide();

        PlayerSlide? expected = new PlayerSlide(Direction.Down, new Cell(1, 0), new Cell(3, 0), new Cell(3, 0), false);
        Assert.Equal((expected, 2), (next, session.Moves));
    }

    [Fact]
    public void Second_request_during_a_slide_is_dropped()
    {
        var session = MidSlideWithARememberedMove();
        session.Request(Direction.Right);
        session.FinishSlide();

        var after = session.FinishSlide();

        Assert.Equal(((PlayerSlide?)null, 2, new Cell(3, 0)), (after, session.Moves, session.Position));
    }

    [Fact]
    public void Remembered_no_op_is_dropped()
    {
        var session = New();
        session.Request(Direction.Down);
        session.Request(Direction.Left);

        var next = session.FinishSlide();

        Assert.Equal(((PlayerSlide?)null, 1, (PlayerSlide?)null), (next, session.Moves, session.Current));
    }

    [Fact]
    public void Winning_slide_in_flight_is_won_but_not_finished()
    {
        var session = WinningSlideInFlight();

        Assert.Equal((true, false, true), (session.IsWon, session.IsFinished, session.Current!.Value.Wins));
    }

    [Fact]
    public void Move_remembered_during_the_winning_slide_is_dropped()
    {
        var session = WinningSlideInFlight();
        session.Request(Direction.Up);

        var next = session.FinishSlide();

        Assert.Equal(
            ((PlayerSlide?)null, true, 3, TestMaps.ExampleEnd),
            (next, session.IsFinished, session.Moves, session.Position));
    }

    [Fact]
    public void FinishSlide_with_no_slide_in_flight_changes_nothing()
    {
        var session = New();

        var next = session.FinishSlide();

        Assert.Equal(((PlayerSlide?)null, Start, 0), (next, session.Position, session.Moves));
    }

    [Theory]
    [InlineData(Direction.Up)]
    [InlineData(Direction.Down)]
    [InlineData(Direction.Left)]
    [InlineData(Direction.Right)]
    public void Requests_after_the_win_return_null(Direction direction)
    {
        Assert.Null(Finished().Request(direction));
    }

    [Fact]
    public void Undo_with_no_slide_in_flight_goes_back_one_move()
    {
        var session = New();
        session.Request(Direction.Down);
        session.FinishSlide();

        var undone = session.Undo();

        Assert.Equal(((Direction?)Direction.Down, Start, 0), (undone, session.Position, session.Moves));
    }

    [Fact]
    public void Undo_during_a_slide_undoes_the_move_in_flight()
    {
        var session = New();
        session.Request(Direction.Down);
        session.FinishSlide();
        session.Request(Direction.Down);

        var undone = session.Undo();

        Assert.Equal(
            ((Direction?)Direction.Down, new Cell(1, 0), 1, (PlayerSlide?)null),
            (undone, session.Position, session.Moves, session.Current));
    }

    [Fact]
    public void Move_remembered_before_an_undo_never_starts()
    {
        var session = New();
        session.Request(Direction.Down);
        session.FinishSlide();
        session.Request(Direction.Down);
        session.Request(Direction.Right);
        session.Undo();
        session.Request(Direction.Down);

        var next = session.FinishSlide();

        Assert.Equal(((PlayerSlide?)null, new Cell(3, 0), 2), (next, session.Position, session.Moves));
    }

    [Fact]
    public void Undo_with_nothing_to_undo_returns_null()
    {
        Assert.Null(New().Undo());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Undo_once_won_changes_nothing(bool finishWinningSlide)
    {
        var session = finishWinningSlide ? Finished() : WinningSlideInFlight();

        var undone = session.Undo();

        Assert.Equal(((Direction?)null, TestMaps.ExampleEnd, 3), (undone, session.Position, session.Moves));
    }

    [Fact]
    public void Request_after_undo_starts_at_once()
    {
        var session = New();
        session.Request(Direction.Down);
        session.Undo();

        Assert.NotNull(session.Request(Direction.Down));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Restart_returns_to_the_start(bool won)
    {
        var session = won ? Finished() : MidSlideWithARememberedMove();

        session.Restart();

        Assert.Equal(
            (Start, 0, (PlayerSlide?)null, false, false),
            (session.Position, session.Moves, session.Current, session.IsWon, session.IsFinished));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Restart_leaves_nothing_to_undo(bool won)
    {
        var session = won ? Finished() : MidSlideWithARememberedMove();

        session.Restart();

        Assert.Null(session.Undo());
    }

    [Fact]
    public void Move_remembered_before_a_restart_never_starts()
    {
        var session = MidSlideWithARememberedMove();
        session.Restart();
        session.Request(Direction.Down);

        var next = session.FinishSlide();

        Assert.Equal(((PlayerSlide?)null, new Cell(1, 0), 1), (next, session.Position, session.Moves));
    }

    [Fact]
    public void Null_map_throws()
    {
        Assert.Throws<ArgumentNullException>(() => new PlaySession(null!));
    }

    private static PlaySession Limited(int limit) => new(TestMaps.Example(), new PlayRules(limit));

    // Down to [1, 0], then Down to [3, 0], both slides finished
    private static PlaySession TwoMovesMade(int limit)
    {
        var session = Limited(limit);
        session.Request(Direction.Down);
        session.FinishSlide();
        session.Request(Direction.Down);
        session.FinishSlide();
        return session;
    }

    [Fact]
    public void UndosLeft_is_null_without_rules()
    {
        Assert.Null(New().UndosLeft);
    }

    [Theory]
    [AutoData]
    public void UndosLeft_starts_at_the_limit(int limit)
    {
        Assert.Equal(limit, Limited(limit).UndosLeft);
    }

    // AutoFixture ints are positive, so the limits below cover the two moves undone
    [Theory]
    [AutoData]
    public void Each_undone_move_uses_one(int spare)
    {
        var session = TwoMovesMade(spare + 2);

        session.Undo();
        session.Undo();

        Assert.Equal((spare, Start), (session.UndosLeft, session.Position));
    }

    [Fact]
    public void Undo_with_none_left_does_nothing()
    {
        var session = TwoMovesMade(1);
        session.Undo();

        var undone = session.Undo();

        Assert.Equal(((Direction?)null, new Cell(1, 0), 1, 0), (undone, session.Position, session.Moves, session.UndosLeft));
    }

    [Theory]
    [AutoData]
    public void Undo_with_nothing_to_undo_uses_none(int limit)
    {
        var session = Limited(limit);

        session.Undo();

        Assert.Equal(limit, session.UndosLeft);
    }

    [Theory]
    [AutoData]
    public void Undoing_the_move_in_flight_uses_one(int spare)
    {
        var session = Limited(spare + 1);
        session.Request(Direction.Down);

        var undone = session.Undo();

        Assert.Equal((Direction.Down, spare), (undone, session.UndosLeft));
    }

    [Theory]
    [AutoData]
    public void Undo_once_won_uses_none(int limit)
    {
        var session = TwoMovesMade(limit);
        session.Request(Direction.Right);

        var undone = session.Undo();

        Assert.Equal(((Direction?)null, limit), (undone, session.UndosLeft));
    }

    [Theory]
    [AutoData]
    public void Restart_refills_the_undos(int spare)
    {
        var session = TwoMovesMade(spare + 2);
        session.Undo();
        session.Undo();

        session.Restart();

        Assert.Equal(spare + 2, session.UndosLeft);
    }

    [Fact]
    public void A_limit_of_zero_refuses_every_undo()
    {
        var session = Limited(0);
        session.Request(Direction.Down);
        session.FinishSlide();

        var undone = session.Undo();

        Assert.Equal(((Direction?)null, 1), (undone, session.Moves));
    }

    // On [0, 1], with a trap at [0, 3] further along the lane
    private static PlaySession OnTheLaneBesideTheStart()
    {
        var session = new PlaySession(TestMaps.TrappedLane());
        session.Request(Direction.Right);
        session.FinishSlide();
        return session;
    }

    [Fact]
    public void Trapped_move_gives_a_slide_into_the_trap_ending_on_the_start()
    {
        var session = OnTheLaneBesideTheStart();

        var slide = session.Request(Direction.Right);

        Assert.Equal(new PlayerSlide(Direction.Right, new(0, 1), new(0, 3), Start, false), slide);
    }

    [Fact]
    public void Trap_beside_the_start_gives_a_counted_one_square_slide()
    {
        var session = new PlaySession(TestMaps.TrapBesideTheStart());

        var slide = session.Request(Direction.Right);

        Assert.Equal((new PlayerSlide(Direction.Right, Start, new(0, 1), Start, false), 1), (slide, session.Moves));
    }

    [Fact]
    public void Move_remembered_during_a_trapped_slide_starts_from_the_start()
    {
        var session = OnTheLaneBesideTheStart();
        session.Request(Direction.Right);
        session.Request(Direction.Down);

        var next = session.FinishSlide();

        Assert.Equal(new PlayerSlide(Direction.Down, Start, new(1, 0), new(1, 0), false), next);
    }
}
