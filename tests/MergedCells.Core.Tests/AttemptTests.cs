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
    public void Null_map_throws()
    {
        Assert.Throws<ArgumentNullException>(() => new Attempt(null!));
    }
}
