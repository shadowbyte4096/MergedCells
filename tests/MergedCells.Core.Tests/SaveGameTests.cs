using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class SaveGameTests
{
    private static readonly StarsConfig Shipped = new()
    {
        TwoStarMinExtraMoves = 2,
        TwoStarExtraParPercent = 50,
        OneStarMinExtraMoves = 4,
        OneStarExtraParPercent = 100,
    };

    private static int Positive(int value) => Math.Abs(value % 1000) + 1;

    private static Level LevelWith(int id, int par = 3, Map? map = null) =>
        new(id, $"Level {id}", 1, map ?? TestMaps.Example(), par);

    // The example with one region reshaped, so its fingerprint differs
    private static Map Reshaped()
    {
        var regions = TestMaps.ExampleRegions();
        regions[0, 1] = 2;
        return Map.Create(regions, TestMaps.ExampleStart, TestMaps.ExampleEnd);
    }

    [Theory]
    [AutoData]
    public void New_save_has_no_bests_and_follows_the_system_appearance(int id)
    {
        var save = new SaveGame();

        Assert.Equal((Appearance.System, (int?)null), (save.Appearance, save.BestMoves(LevelWith(Positive(id)))));
    }

    [Theory]
    [AutoData]
    public void First_finish_is_a_new_best(int id, int extra)
    {
        var save = new SaveGame();
        var level = LevelWith(Positive(id));
        var moves = 3 + Math.Abs(extra % 10);

        var finish = save.RecordFinish(level, moves, Shipped);

        Assert.Equal(
            ((int?)null, (int?)null, true, (int?)moves),
            (finish.PreviousBest, finish.PreviousStars, finish.IsNewBest, save.BestMoves(level)));
    }

    [Fact]
    public void Better_finish_replaces_the_best()
    {
        var save = new SaveGame();
        var level = LevelWith(4);
        save.RecordFinish(level, 6, Shipped);

        var finish = save.RecordFinish(level, 4, Shipped);

        Assert.Equal(
            ((int?)6, (int?)Stars.Earned(6, 3, Shipped), true, (int?)4),
            (finish.PreviousBest, finish.PreviousStars, finish.IsNewBest, save.BestMoves(level)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    public void Worse_or_equal_finish_keeps_the_best(int extra)
    {
        var save = new SaveGame();
        var level = LevelWith(4);
        save.RecordFinish(level, 4, Shipped);

        var finish = save.RecordFinish(level, 4 + extra, Shipped);

        Assert.Equal((false, (int?)4), (finish.IsNewBest, save.BestMoves(level)));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(20)]
    public void Finish_reports_its_own_stars(int moves)
    {
        var finish = new SaveGame().RecordFinish(LevelWith(4), moves, Shipped);

        Assert.Equal(Stars.Earned(moves, 3, Shipped), finish.Stars);
    }

    [Fact]
    public void Best_from_a_changed_layout_is_ignored()
    {
        var save = new SaveGame();
        save.RecordFinish(LevelWith(4), 5, Shipped);
        var changed = LevelWith(4, map: Reshaped());

        Assert.Equal(((int?)null, (int?)null), (save.BestMoves(changed), save.Stars(changed, Shipped)));
    }

    [Fact]
    public void Best_from_a_changed_par_is_ignored()
    {
        var save = new SaveGame();
        save.RecordFinish(LevelWith(4), 5, Shipped);
        var changed = LevelWith(4, par: 4);

        Assert.Equal(((int?)null, (int?)null), (save.BestMoves(changed), save.Stars(changed, Shipped)));
    }

    [Fact]
    public void Finish_on_a_changed_level_replaces_the_stale_best()
    {
        var save = new SaveGame();
        var original = LevelWith(4);
        var changed = LevelWith(4, map: Reshaped());
        save.RecordFinish(original, 5, Shipped);

        var finish = save.RecordFinish(changed, 8, Shipped);

        Assert.Equal(
            ((int?)null, (int?)8, (int?)null),
            (finish.PreviousBest, save.BestMoves(changed), save.BestMoves(original)));
    }

    [Theory]
    [AutoData]
    public void Other_levels_keep_their_bests(int firstId, int offset)
    {
        var save = new SaveGame();
        var first = LevelWith(Positive(firstId));
        var second = LevelWith(Positive(firstId) + Positive(offset));
        save.RecordFinish(first, 7, Shipped);

        save.RecordFinish(second, 3, Shipped);

        Assert.Equal((int?)7, save.BestMoves(first));
    }

    [Theory]
    [AutoData]
    public void Clearing_progress_forgets_every_best(int firstId, int offset)
    {
        var save = new SaveGame();
        var first = LevelWith(Positive(firstId));
        var second = LevelWith(Positive(firstId) + Positive(offset));
        save.RecordFinish(first, 7, Shipped);
        save.RecordFinish(second, 3, Shipped);

        save.ClearProgress();

        Assert.Equal((0, (int?)null, (int?)null), (save.LevelCount, save.BestMoves(first), save.BestMoves(second)));
    }

    // Appearance is a setting, not progress
    [Theory]
    [InlineData(Appearance.Light)]
    [InlineData(Appearance.Dark)]
    public void Clearing_progress_keeps_the_appearance(Appearance appearance)
    {
        var save = new SaveGame { Appearance = appearance };

        save.ClearProgress();

        Assert.Equal(appearance, save.Appearance);
    }

    // The levels shared from this device aren't progress: they keep a shared level from coming back as imported
    [Theory]
    [AutoData]
    public void Clearing_progress_keeps_shared_levels(Guid id)
    {
        var save = new SaveGame();
        save.RecordShared(id);

        save.ClearProgress();

        Assert.Equal([id], save.SharedLevels);
    }

    // The answers, the last infinite choice and the player ID are settings and preferences, not progress
    [Theory]
    [AutoData]
    public void Clearing_progress_keeps_answers_choice_and_player(bool yes, int wording, Guid player, bool walls)
    {
        var choice = new InfiniteChoice(4, 12, walls, false, true, false);
        var answer = new Consent(yes, Math.Abs(wording % 100));
        var save = new SaveGame { Infinite = choice, Player = player };
        save.SetConsent(ConsentKind.PlayData, answer);
        save.SetConsent(ConsentKind.Leaderboards, answer);

        save.ClearProgress();

        Assert.Equal(
            (answer, answer, choice, (Guid?)player),
            (save.ConsentTo(ConsentKind.PlayData), save.ConsentTo(ConsentKind.Leaderboards), save.Infinite, save.Player));
    }

    [Theory]
    [AutoData]
    public void Clearing_progress_keeps_the_server_section(string token, Guid pending, uint sent)
    {
        var save = new SaveGame { SessionToken = token, RecordsSent = sent };
        save.AddPendingDeletion(pending);

        save.ClearProgress();

        Assert.Equal((token, true, (long)sent), (save.SessionToken, save.PendingDeletions.Contains(pending), save.RecordsSent));
    }

    [Fact]
    public void Empty_session_token_is_refused()
    {
        Assert.Throws<ArgumentException>(() => new SaveGame { SessionToken = "" });
    }

    [Fact]
    public void Negative_records_sent_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SaveGame { RecordsSent = -1 });
    }

    [Fact]
    public void All_zeros_pending_deletion_is_refused()
    {
        Assert.Throws<ArgumentException>(() => new SaveGame().AddPendingDeletion(Guid.Empty));
    }

    [Theory]
    [AutoData]
    public void Consent_wording_cannot_be_negative(bool yes, int wording)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Consent(yes, -Math.Abs(wording % 100) - 1));
    }

    // A choice Parse would refuse would make the next load a broken save
    [Theory]
    [InlineData(0, 10)]
    [InlineData(11, 10)]
    [InlineData(3, 1)]
    public void Infinite_cannot_be_a_choice_the_save_would_refuse(int difficulty, int side)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SaveGame { Infinite = new InfiniteChoice(difficulty, side, false, false, false, false) });
    }

    [Fact]
    public void Player_cannot_be_empty()
    {
        Assert.Throws<ArgumentException>(() => new SaveGame { Player = Guid.Empty });
    }

    [Fact]
    public void Cleared_save_writes_no_levels()
    {
        var save = new SaveGame();
        save.RecordFinish(LevelWith(4), 3, Shipped);

        save.ClearProgress();

        Assert.Equal(0, SaveGame.Parse(save.ToJson()).LevelCount);
    }

    public static TheoryData<int, int, int, int> Configs => new()
    {
        { 2, 50, 4, 100 },
        { 0, 0, 1, 0 },
    };

    [Theory]
    [MemberData(nameof(Configs))]
    public void Stars_use_the_thresholds_given(int twoStarMin, int twoStarPercent, int oneStarMin, int oneStarPercent)
    {
        var config = new StarsConfig
        {
            TwoStarMinExtraMoves = twoStarMin,
            TwoStarExtraParPercent = twoStarPercent,
            OneStarMinExtraMoves = oneStarMin,
            OneStarExtraParPercent = oneStarPercent,
        };
        var save = new SaveGame();
        var level = LevelWith(4);
        save.RecordFinish(level, 4, Shipped);

        Assert.Equal(Stars.Earned(4, 3, config), save.Stars(level, config));
    }

    [Theory]
    [AutoData]
    public void Unplayed_level_has_no_stars(int id)
    {
        Assert.Null(new SaveGame().Stars(LevelWith(Positive(id)), Shipped));
    }

    [Fact]
    public void Finish_below_par_throws_and_records_nothing()
    {
        var save = new SaveGame();
        var level = LevelWith(4);

        Assert.Throws<ArgumentOutOfRangeException>(() => save.RecordFinish(level, 2, Shipped));
        Assert.Null(save.BestMoves(level));
    }
}
