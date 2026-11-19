using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class ConsentsTests
{
    private static readonly ConsentWording Current = ConsentWordings.PlayData with { Version = 1, Offered = true };

    private static Consent? Answer(bool? yes, int wording) => yes is { } answer ? new Consent(answer, wording) : null;

    [Theory]
    [InlineData(null, 0, false)]
    [InlineData(true, 1, true)]
    [InlineData(true, 0, false)]
    [InlineData(true, 2, false)]
    [InlineData(false, 1, false)]
    public void Is_given_only_for_yes_at_the_current_version(bool? yes, int wording, bool expected)
    {
        Assert.Equal(expected, Consents.IsGiven(Answer(yes, wording), Current));
    }

    // A yes to newer wording (a save from a newer build) can't be assumed to cover this build's uses either
    [Theory]
    [InlineData(null, 0, true)]
    [InlineData(true, 0, true)]
    [InlineData(true, 2, true)]
    [InlineData(true, 1, false)]
    [InlineData(false, 0, false)]
    [InlineData(false, 1, false)]
    public void Needs_asking_when_never_asked_or_yes_to_other_wording(bool? yes, int wording, bool expected)
    {
        Assert.Equal(expected, Consents.NeedsAsking(Answer(yes, wording), Current));
    }

    [Fact]
    public void Next_to_ask_skips_kinds_not_offered()
    {
        var leaderboards = ConsentWordings.Leaderboards with { Offered = true };

        Assert.Equal(leaderboards, Consents.NextToAsk(new SaveGame(), [Current with { Offered = false }, leaderboards]));
    }

    [Fact]
    public void Next_to_ask_is_play_data_first()
    {
        Assert.Equal(Current, Consents.NextToAsk(new SaveGame(), [Current, ConsentWordings.Leaderboards with { Offered = true }]));
    }

    [Fact]
    public void Next_to_ask_is_the_next_kind_once_play_data_is_answered()
    {
        var save = new SaveGame();
        save.SetConsent(ConsentKind.PlayData, new Consent(false, 1));
        var leaderboards = ConsentWordings.Leaderboards with { Offered = true };

        Assert.Equal(leaderboards, Consents.NextToAsk(save, [Current, leaderboards]));
    }

    [Fact]
    public void Next_to_ask_is_null_when_every_offered_kind_is_answered()
    {
        var save = new SaveGame();
        save.SetConsent(ConsentKind.PlayData, new Consent(false, 1));

        Assert.Null(Consents.NextToAsk(save, [Current, ConsentWordings.Leaderboards with { Offered = false }]));
    }

    [Fact]
    public void Next_to_ask_asks_again_after_a_yes_to_older_wording()
    {
        var save = new SaveGame();
        save.SetConsent(ConsentKind.PlayData, new Consent(true, 0));

        Assert.Equal(Current, Consents.NextToAsk(save, [Current]));
    }

    [Theory]
    [AutoData]
    public void Yes_makes_a_player_when_there_is_none(Guid id)
    {
        var save = new SaveGame();

        Consents.Answer(save, Current, true, () => id);

        Assert.Equal((new Consent(true, 1), (Guid?)id), (save.ConsentTo(ConsentKind.PlayData), save.Player));
    }

    [Theory]
    [AutoData]
    public void Yes_keeps_an_existing_player(Guid kept, Guid other)
    {
        var save = new SaveGame { Player = kept };

        Consents.Answer(save, Current, true, () => other);

        Assert.Equal(kept, save.Player);
    }

    [Theory]
    [AutoData]
    public void No_makes_no_player(Guid id)
    {
        var save = new SaveGame();

        Consents.Answer(save, Current, false, () => id);

        Assert.Equal((new Consent(false, 1), (Guid?)null), (save.ConsentTo(ConsentKind.PlayData), save.Player));
    }

    [Theory]
    [AutoData]
    public void Answer_is_stored_for_its_own_kind(Guid id)
    {
        var save = new SaveGame();

        Consents.Answer(save, ConsentWordings.Leaderboards with { Version = 3 }, true, () => id);

        Assert.Equal(((Consent?)null, new Consent(true, 3)), (save.ConsentTo(ConsentKind.PlayData), save.ConsentTo(ConsentKind.Leaderboards)));
    }

    [Theory]
    [AutoData]
    public void Record_is_kept_while_its_player_is_the_saves(Guid player)
    {
        var save = Recording(player);

        Assert.True(Consents.MayKeep(Start(player), save));
    }

    // A visit still open when "Delete my data" ran must not write under the deleted ID
    [Theory]
    [AutoData]
    public void Record_from_a_deleted_player_is_not_kept(Guid deleted, Guid fresh)
    {
        var save = Recording(fresh);

        Assert.False(Consents.MayKeep(Start(deleted), save));
    }

    [Theory]
    [AutoData]
    public void Record_is_not_kept_after_the_player_id_is_dropped(Guid deleted)
    {
        var save = Recording(deleted);
        save.Player = null;

        Assert.False(Consents.MayKeep(Start(deleted), save));
    }

    [Theory]
    [AutoData]
    public void Record_is_not_kept_after_play_data_is_turned_off(Guid player)
    {
        var save = Recording(player);
        save.SetConsent(ConsentKind.PlayData, new Consent(false, ConsentWordings.PlayData.Version));

        Assert.False(Consents.MayKeep(Start(player), save));
    }

    [Fact]
    public void May_record_once_play_data_is_given()
    {
        var save = new SaveGame();
        save.SetConsent(ConsentKind.PlayData, new Consent(true, ConsentWordings.PlayData.Version));

        Assert.True(Consents.MayRecord(save));
    }

    [Fact]
    public void May_not_record_on_a_yes_to_older_wording()
    {
        var save = new SaveGame();
        save.SetConsent(ConsentKind.PlayData, new Consent(true, ConsentWordings.PlayData.Version - 1));

        Assert.False(Consents.MayRecord(save));
    }

    public static TheoryData<ConsentKind> Kinds => [.. Enum.GetValues<ConsentKind>()];

    // Validity, not content: version 0 is chunk 22's wording, which nobody can be asked with again
    [Theory]
    [MemberData(nameof(Kinds))]
    public void Shipped_wording_versions_are_at_least_one(ConsentKind kind)
    {
        Assert.True(ConsentWordings.For(kind).Version >= 1);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void Each_kind_has_its_own_wording(ConsentKind kind)
    {
        Assert.Equal(kind, ConsentWordings.For(kind).Kind);
    }

    private static SaveGame Recording(Guid player)
    {
        var save = new SaveGame { Player = player };
        save.SetConsent(ConsentKind.PlayData, new Consent(true, ConsentWordings.PlayData.Version));
        return save;
    }

    private static PlayRecordStart Start(Guid player) => PlayRecords.Infinite(TestMaps.Example()) with { Player = player };
}
