namespace MergedCells.Core.Tests;

// The end is at [4, 4], so each square's coordinates set how far it is from the end
public class LookaheadPlayersTests
{
    private static readonly Cell End = new(4, 4);
    private static readonly Cell Start = new(2, 2);

    private static int? ExtraMoves(MoveRule rule, int lookahead) =>
        LookaheadPlayers.ExtraMoves(MoveGraph.Build(Start, rule.Apply), End, lookahead);

    // [1, 3] and [3, 1] are both 4 from the end. The good one wins at once; the other needs two more moves
    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    public void Ties_go_to_the_first_direction(bool upIsGood, int expected)
    {
        var (good, other) = upIsGood ? (new Cell(1, 3), new Cell(3, 1)) : (new Cell(3, 1), new Cell(1, 3));
        var rule = new MoveRule()
            .Slide(Start, Direction.Up, new(1, 3))
            .Slide(Start, Direction.Down, new(3, 1))
            .Win(good, Direction.Right)
            .Slide(other, Direction.Right, new(2, 4))
            .Win(new(2, 4), Direction.Right);

        Assert.Equal(expected, ExtraMoves(rule, 1));
    }

    // Down comes first in direction order, but Right wins at once
    [Fact]
    public void A_sooner_win_beats_a_later_one()
    {
        var rule = new MoveRule()
            .Win(Start, Direction.Right)
            .Slide(Start, Direction.Down, new(3, 2))
            .Win(new(3, 2), Direction.Right);

        Assert.Equal(0, ExtraMoves(rule, 2));
    }

    // [3, 3] looks nearest, but its only move leads back to the start; Down then Right wins
    private static MoveRule BackToTheStart() => new MoveRule()
        .Slide(Start, Direction.Right, new(3, 3))
        .Slide(new(3, 3), Direction.Left, Start)
        .Slide(Start, Direction.Down, new(2, 1))
        .Win(new(2, 1), Direction.Right);

    [Fact]
    public void A_player_that_comes_back_to_a_square_never_finishes()
    {
        Assert.Null(ExtraMoves(BackToTheStart(), 1));
    }

    // The 1-move player never finishes, but the 2-move player sees Down then Right win
    [Fact]
    public void The_fewest_moves_of_any_player_count()
    {
        Assert.Equal(0, ExtraMoves(BackToTheStart(), 2));
    }

    // [3, 3] looks nearest, but leads into a pocket of two squares that only slide to each other. The 1-move player walks
    // in and gives up; the 2-move player sees Down then Right win
    [Theory]
    [InlineData(1, null)]
    [InlineData(2, 0)]
    public void A_dead_end_pocket_stops_only_the_players_that_walk_into_it(int lookahead, int? expected)
    {
        var rule = new MoveRule()
            .Slide(Start, Direction.Right, new(3, 3))
            .Slide(new(3, 3), Direction.Down, new(3, 4))
            .Slide(new(3, 4), Direction.Up, new(3, 3))
            .Slide(Start, Direction.Down, new(2, 1))
            .Win(new(2, 1), Direction.Right);

        Assert.Equal(expected, ExtraMoves(rule, lookahead));
    }

    // Right lands on [3, 3], 2 from the end, which has no moves; Down then Right lands on [2, 1], 5 away. The 2-move
    // player's line through [3, 3] ends there early, so it looks nearer, and the player is stuck. Down, Right, Right wins
    [Fact]
    public void A_line_ends_early_on_a_square_with_no_moves()
    {
        var rule = new MoveRule()
            .Slide(Start, Direction.Right, new(3, 3))
            .Slide(Start, Direction.Down, new(3, 1))
            .Slide(new(3, 1), Direction.Right, new(2, 1))
            .Win(new(2, 1), Direction.Right);

        Assert.Null(ExtraMoves(rule, 2));
    }

    // Right lands near the end but its next move lands far away; Down lands further but its next move lands beside the
    // end. The 2-move player judges each line by its last landing, so it goes Down, and wins in par
    [Fact]
    public void A_line_is_judged_by_where_it_last_lands()
    {
        var rule = new MoveRule()
            .Slide(Start, Direction.Right, new(3, 3))
            .Slide(new(3, 3), Direction.Down, new(0, 0))
            .Slide(Start, Direction.Down, new(2, 1))
            .Slide(new(2, 1), Direction.Right, new(3, 4))
            .Win(new(3, 4), Direction.Down);

        Assert.Equal(0, ExtraMoves(rule, 2));
    }
}
