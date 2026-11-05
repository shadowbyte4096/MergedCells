namespace MergedCells.Core.Tests;

public class AttemptTests
{
    private static Attempt WonExample()
    {
        var attempt = new Attempt(TestMaps.Example());
        attempt.Move(Direction.Down);
        attempt.Move(Direction.Down);
        attempt.Move(Direction.Right);
        return attempt;
    }

    [Fact]
    public void New_attempt_is_at_the_start_with_no_moves()
    {
        var attempt = new Attempt(TestMaps.Example());

        Assert.Equal((TestMaps.ExampleStart, 0, false), (attempt.Position, attempt.Moves, attempt.IsWon));
    }

    [Fact]
    public void Move_that_changes_position_is_counted()
    {
        var attempt = new Attempt(TestMaps.Example());

        var outcome = attempt.Move(Direction.Down);

        Assert.Equal((MoveOutcome.Moved, new Cell(1, 0), 1), (outcome, attempt.Position, attempt.Moves));
    }

    [Theory]
    [InlineData(Direction.Up)]
    [InlineData(Direction.Left)]
    public void No_op_move_is_ignored_and_not_counted(Direction direction)
    {
        var attempt = new Attempt(TestMaps.Example());

        var outcome = attempt.Move(direction);

        Assert.Equal((MoveOutcome.Ignored, TestMaps.ExampleStart, 0), (outcome, attempt.Position, attempt.Moves));
    }

    [Fact]
    public void Winning_move_returns_Won_and_sets_IsWon()
    {
        var attempt = new Attempt(TestMaps.Example());
        attempt.Move(Direction.Down);
        attempt.Move(Direction.Down);

        var outcome = attempt.Move(Direction.Right);

        Assert.Equal((MoveOutcome.Won, true, TestMaps.ExampleEnd), (outcome, attempt.IsWon, attempt.Position));
    }

    [Fact]
    public void Example_takes_three_counted_moves()
    {
        Assert.Equal(3, WonExample().Moves);
    }

    [Theory]
    [InlineData(Direction.Up)]
    [InlineData(Direction.Down)]
    [InlineData(Direction.Left)]
    [InlineData(Direction.Right)]
    public void Moves_after_a_win_are_ignored(Direction direction)
    {
        var attempt = WonExample();

        var outcome = attempt.Move(direction);

        Assert.Equal((MoveOutcome.Ignored, TestMaps.ExampleEnd, 3), (outcome, attempt.Position, attempt.Moves));
    }

    [Fact]
    public void Undo_with_no_moves_returns_null_and_changes_nothing()
    {
        var attempt = new Attempt(TestMaps.Example());

        var undone = attempt.Undo();

        Assert.Equal(((Direction?)null, TestMaps.ExampleStart, 0), (undone, attempt.Position, attempt.Moves));
    }

    [Fact]
    public void Undo_returns_the_last_move_and_restores_position_and_count()
    {
        var attempt = new Attempt(TestMaps.Example());
        attempt.Move(Direction.Down);
        attempt.Move(Direction.Down);

        var undone = attempt.Undo();

        Assert.Equal(((Direction?)Direction.Down, new Cell(1, 0), 1), (undone, attempt.Position, attempt.Moves));
    }

    [Fact]
    public void Undo_steps_back_in_reverse_order_to_the_start()
    {
        var attempt = WonExample();

        var undone = new[] { attempt.Undo(), attempt.Undo(), attempt.Undo() };

        Assert.Equal(new Direction?[] { Direction.Right, Direction.Down, Direction.Down }, undone);
        Assert.Equal((TestMaps.ExampleStart, 0, false), (attempt.Position, attempt.Moves, attempt.IsWon));
    }

    [Fact]
    public void Ignored_moves_leave_nothing_to_undo()
    {
        var attempt = new Attempt(TestMaps.Example());
        attempt.Move(Direction.Up);

        Assert.Null(attempt.Undo());
    }

    [Fact]
    public void Undoing_the_winning_move_clears_IsWon()
    {
        var attempt = WonExample();

        var undone = attempt.Undo();

        Assert.Equal(
            ((Direction?)Direction.Right, false, new Cell(3, 0), 2),
            (undone, attempt.IsWon, attempt.Position, attempt.Moves));
    }

    [Fact]
    public void Move_after_undoing_a_win_can_win_again()
    {
        var attempt = WonExample();
        attempt.Undo();

        Assert.Equal(MoveOutcome.Won, attempt.Move(Direction.Right));
    }

    [Fact]
    public void Move_after_undo_continues_from_the_restored_square()
    {
        var attempt = new Attempt(TestMaps.Example());
        attempt.Move(Direction.Down);
        attempt.Move(Direction.Down);
        attempt.Undo();

        var outcome = attempt.Move(Direction.Right);

        Assert.Equal((MoveOutcome.Moved, new Cell(1, 1), 2), (outcome, attempt.Position, attempt.Moves));
    }

    [Fact]
    public void Null_map_throws()
    {
        Assert.Throws<ArgumentNullException>(() => new Attempt(null!));
    }
}
