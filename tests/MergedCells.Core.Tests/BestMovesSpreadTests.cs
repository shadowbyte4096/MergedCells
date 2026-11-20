namespace MergedCells.Core.Tests;

// The numbers here are the behaviour (shares, rounding and the line's wording), so they're written out rather than generated
public class BestMovesSpreadTests
{
    // The mockup's spread at par 6: 214 players
    private static readonly (int, int)[] Mockup = [(6, 88), (7, 47), (8, 32), (9, 19), (10, 11), (11, 9), (14, 8)];

    private static SpreadCounts Counts(int more, params (int Moves, int Players)[] counts) =>
        new(counts.Select(count => new MoveCount(count.Moves, count.Players)).ToArray(), more);

    private static BestMovesSpread Spread(int best, params (int Moves, int Players)[] counts) =>
        BestMovesSpread.Of(Counts(0, counts), SpreadAxis.Of(6, best, 6));

    [Theory]
    [InlineData(6, 6, 0)]
    [InlineData(6, 8, 2)]
    [InlineData(3, 40, 5)]
    public void Axis_marks_the_best_s_column(int par, int best, int column)
    {
        Assert.Equal(column, SpreadAxis.Of(par, best, 6).BestColumn);
    }

    [Fact]
    public void Axis_labels_the_last_column_with_a_plus()
    {
        var axis = SpreadAxis.Of(6, 6, 6);

        Assert.Equal(["6", "7", "8", "9", "10", "11+"], Enumerable.Range(0, 6).Select(axis.Label));
    }

    [Theory]
    [InlineData(0, 1, 6)]
    [InlineData(6, 5, 6)]
    [InlineData(6, 6, 1)]
    public void Axis_refuses_a_bad_par_best_or_column_count(int par, int best, int columns)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SpreadAxis.Of(par, best, columns));
    }

    [Fact]
    public void Shares_of_the_mockup_s_spread()
    {
        Assert.Equal(["41%", "22%", "15%", "9%", "5%", "8%"], Spread(8, Mockup).Columns.Select(column => column.Share));
    }

    [Fact]
    public void Shares_round_half_up()
    {
        Assert.Equal(["13%", "88%"], Spread(6, (6, 1), (7, 7)).Columns.Take(2).Select(column => column.Share));
    }

    [Fact]
    public void Non_empty_column_under_half_a_percent_shows_under_one_percent()
    {
        Assert.Equal(["100%", "<1%", "0%"], Spread(6, (6, 999), (7, 1)).Columns.Take(3).Select(column => column.Share));
    }

    [Fact]
    public void Last_column_sums_every_move_count_above_it_and_more()
    {
        var spread = BestMovesSpread.Of(Counts(5, (11, 2), (12, 3), (30, 4)), SpreadAxis.Of(6, 11, 6));

        Assert.Equal(14, spread.Columns[^1].Players);
    }

    [Fact]
    public void Heights_are_against_the_largest_column()
    {
        Assert.Equal([1f, 0.25f, 0f, 0f, 0f, 0f], Spread(6, (6, 40), (7, 10)).Columns.Select(column => column.Height));
    }

    [Fact]
    public void Counts_below_par_are_ignored()
    {
        Assert.Equal(3, Spread(6, (5, 7), (6, 3)).Players);
    }

    [Fact]
    public void Line_at_par_matches_par()
    {
        Assert.Equal("Matched par, like 41% of players", Spread(6, Mockup).Line);
    }

    [Fact]
    public void Line_over_par_counts_players_behind_rounded_down()
    {
        Assert.Equal("Better than 21% of players", Spread(8, Mockup).Line);
    }

    [Fact]
    public void Line_behind_under_one_percent()
    {
        Assert.Equal("Better than <1% of players", Spread(7, (6, 999), (7, 1), (9, 1)).Line);
    }

    [Fact]
    public void Line_counts_more_as_behind()
    {
        var spread = BestMovesSpread.Of(Counts(2, (6, 1), (40, 1)), SpreadAxis.Of(6, 40, 6));

        Assert.Equal("Better than 50% of players", spread.Line);
    }

    [Fact]
    public void Line_with_nobody_behind_says_how_many_found_par()
    {
        Assert.Equal("40% of players found par", Spread(9, (6, 4), (9, 6)).Line);
    }

    [Fact]
    public void Line_with_nobody_at_par_or_behind()
    {
        Assert.Equal("Nobody has found par yet", Spread(8, (8, 5)).Line);
    }

    [Theory]
    [InlineData(0, true, "")]
    [InlineData(1, true, "1 player")]
    [InlineData(2, false, "2 players")]
    public void First_at_totals_up_to_one(int players, bool first, string text)
    {
        var spread = players == 0 ? Spread(6) : Spread(6, (6, players));

        Assert.Equal((first, text, first, players), (spread.IsFirst, spread.PlayersText, spread.Line is null, spread.Players));
    }

    [Fact]
    public void Title_counts_players_with_a_thousands_separator()
    {
        Assert.Equal("1,086 players", Spread(6, (6, 1086)).PlayersText);
    }
}
