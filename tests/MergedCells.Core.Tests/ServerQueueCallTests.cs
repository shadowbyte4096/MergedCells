using System.Text.Json;
using AutoFixture;
using static MergedCells.Core.Tests.ServerQueueTests;

namespace MergedCells.Core.Tests;

public class ServerQueueCallTests
{
    private static readonly Fixture Fixture = new();

    private const string Ok = """{"ok":true}""";

    private sealed record Probe(bool Ok);

    // A stand-in for a library call: needs the library's yes and a session unless told otherwise
    private static ServerCall<Probe> Call(
        ConsentKind? needs = ConsentKind.Library, bool session = true, bool forced = true, Func<SaveGame, Guid, Probe, bool>? apply = null) =>
        new(
            "probe",
            needs,
            (writer, player) => writer.WriteString("player", player.ToString("D")),
            root => root.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True ? new Probe(true) : null)
        {
            Session = session,
            Forced = forced,
            Apply = apply,
        };

    // Records the level as shared, standing in for a call's save change
    private static Func<SaveGame, Guid, Probe, bool> Shares(Guid level) => (save, _, _) => save.RecordShared(level);

    private static Rig Joined()
    {
        var rig = new Rig();
        rig.Library();
        return rig;
    }

    // A failed round, so the retry gap is running with no round active
    private static Rig AfterAFailedRound()
    {
        var rig = Joined();
        rig.Queue.Send(Call());
        rig.Start();
        rig.SignedIn();
        rig.Answer(503, "");
        return rig;
    }

    [Fact]
    public void Call_goes_after_pending_deletions_and_before_the_scores()
    {
        var rig = Joined();
        rig.Leaderboards();
        rig.Best(1, 4);
        rig.Save.AddPendingDeletion(Fixture.Create<Guid>());
        rig.Queue.Send(Call());

        var paths = new[] { rig.Start(), rig.Answer(200, """{"deleted":true}"""), rig.SignedIn(), rig.Answer(200, Ok) }.Select(request => request?.Path);

        Assert.Equal(["rest/v1/rpc/delete_player", "auth/v1/signup", "rest/v1/rpc/probe", "rest/v1/rpc/submit_scores"], paths);
    }

    [Fact]
    public void Spread_fetch_still_goes_after_the_scores_with_a_call_waiting()
    {
        var rig = Joined();
        rig.Leaderboards();
        rig.Best(1, 4);
        rig.Queue.Fetch(1, Layout);
        rig.Queue.Send(Call());

        var paths = new[] { rig.Start(), rig.SignedIn(), rig.Answer(200, Ok), rig.Answer(200, Accepted(1)) }.Select(request => request?.Path);

        Assert.Equal(["auth/v1/signup", "rest/v1/rpc/probe", "rest/v1/rpc/submit_scores", "rest/v1/rpc/best_moves_spread"], paths);
    }

    [Fact]
    public void Call_needing_consent_is_not_sent_without_it()
    {
        var rig = new Rig();
        rig.Save.Player = Fixture.Create<Guid>();
        var call = rig.Queue.Send(Call());

        var next = rig.Queue.Next();

        Assert.Equal((CallState.Unavailable, (ServerRequest?)null), (call.State, next));
    }

    [Fact]
    public void Call_with_no_consent_and_no_session_goes_with_the_key_alone()
    {
        var rig = new Rig();
        var player = Fixture.Create<Guid>();
        rig.Save.Player = player;
        rig.Queue.Send(Call(needs: null, session: false));

        var request = rig.Queue.Next()!;

        Assert.Equal(("rest/v1/rpc/probe", (string?)null, player.ToString("D")), (request.Path, request.Bearer, Body(request).GetProperty("player").GetString()));
    }

    [Fact]
    public void Call_with_no_player_and_no_consent_needed_is_unavailable()
    {
        var rig = new Rig();
        var call = rig.Queue.Send(Call(needs: null, session: false));

        var next = rig.Queue.Next();

        Assert.Equal((CallState.Unavailable, (ServerRequest?)null), (call.State, next));
    }

    [Fact]
    public void Call_makes_a_player_when_given_and_none()
    {
        var rig = Joined();
        rig.Save.Player = null;
        rig.Queue.Send(Call());

        rig.Start();

        Assert.Equal(rig.MadePlayer, rig.Save.Player);
    }

    [Fact]
    public void Call_stops_the_queue_when_the_new_player_cannot_be_saved()
    {
        var rig = Joined();
        rig.Save.Player = null;
        rig.Writable = false;
        var call = rig.Queue.Send(Call());

        rig.Queue.Next();

        Assert.Equal((true, CallState.Off), (rig.Queue.IsStopped, call.State));
    }

    [Fact]
    public void Cancelled_call_is_not_sent()
    {
        var rig = Joined();
        var call = rig.Queue.Send(Call(session: false));
        call.Cancel();

        Assert.Null(rig.Queue.Next());
    }

    [Fact]
    public void Cancelled_call_already_sent_still_applies_its_save_change()
    {
        var rig = Joined();
        var level = Fixture.Create<Guid>();
        var call = rig.Queue.Send(Call(apply: Shares(level)));
        rig.Start();
        rig.SignedIn();
        call.Cancel();

        rig.Answer(200, Ok);

        Assert.Equal((true, CallState.Unavailable), (rig.Save.SharedLevels.Contains(level), call.State));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(429)]
    [InlineData(503)]
    public void Status_fails_the_round(int status)
    {
        var rig = Joined();
        var call = rig.Queue.Send(Call());
        rig.Start();
        rig.SignedIn();

        rig.Answer(status, "");

        Assert.Equal((false, CallState.Unavailable), (rig.Rounds.Last(), call.State));
    }

    [Theory]
    [InlineData(404, "")]
    [InlineData(200, "{}")]
    [InlineData(200, "<html>portal</html>")]
    public void Refusal_fails_only_the_call(int status, string body)
    {
        var rig = Joined();
        rig.Leaderboards();
        rig.Best(1, 4);
        var call = rig.Queue.Send(Call());
        rig.Start();
        rig.SignedIn();

        var next = rig.Answer(status, body);

        Assert.Equal((CallState.Unavailable, "rest/v1/rpc/submit_scores"), (call.State, next?.Path));
    }

    [Fact]
    public void Unauthorised_call_renews_and_is_sent_again()
    {
        var rig = Joined();
        rig.Queue.Send(Call());
        rig.Start();
        rig.SignedIn("a1", "r1");
        rig.Answer(401, "");

        var again = rig.Answer(200, Session("a2", "r2"))!;

        Assert.Equal(("rest/v1/rpc/probe", "a2"), (again.Path, again.Bearer));
    }

    // "Delete my data" can land between a request and its reply (Review Focus 1)
    [Fact]
    public void Reply_after_the_player_changed_is_unavailable_and_changes_nothing()
    {
        var rig = Joined();
        var level = Fixture.Create<Guid>();
        var call = rig.Queue.Send(Call(apply: Shares(level)));
        rig.Start();
        rig.SignedIn();
        rig.Save.Player = null;

        rig.Answer(200, Ok);

        Assert.Equal((CallState.Unavailable, false), (call.State, rig.Save.SharedLevels.Contains(level)));
    }

    // A call still queued when "Delete my data" drops the player must not go out under the next ID: a queued publish
    // would put the level back in the library the player just asked to empty
    [Fact]
    public void Call_queued_before_the_player_changed_is_not_sent()
    {
        var rig = Joined();
        var level = Fixture.Create<Guid>();
        var call = rig.Queue.Send(Call(apply: Shares(level)));
        rig.Save.Player = Fixture.Create<Guid>();

        var next = rig.Queue.Next();

        Assert.Equal((CallState.Unavailable, (string?)null), (call.State, next?.Path == "rest/v1/rpc/probe" ? next.Path : null));
    }

    [Fact]
    public void Call_sent_with_no_player_goes_under_the_one_made_for_it()
    {
        var rig = Joined();
        rig.Save.Player = null;
        var call = rig.Queue.Send(Call(session: false));

        var request = rig.Queue.Next()!;

        Assert.Equal((CallState.Waiting, rig.MadePlayer.ToString("D")), (call.State, Body(request).GetProperty("player").GetString()));
    }

    [Fact]
    public void Send_is_off_when_the_server_is_off()
    {
        var rig = new Rig(Config with { Url = "" });
        rig.Library();

        Assert.Equal(CallState.Off, rig.Queue.Send(Call()).State);
    }

    [Fact]
    public void Send_is_off_when_stopped()
    {
        var rig = Joined();
        rig.Save.Player = null;
        rig.Writable = false;
        rig.Queue.Send(Call());
        rig.Queue.Next();

        Assert.Equal(CallState.Off, rig.Queue.Send(Call()).State);
    }

    [Fact]
    public void Unforced_call_is_unavailable_at_once_in_the_retry_gap()
    {
        var rig = AfterAFailedRound();

        Assert.Equal(CallState.Unavailable, rig.Queue.Send(Call(forced: false)).State);
    }

    [Fact]
    public void Forced_call_goes_in_the_retry_gap()
    {
        var rig = AfterAFailedRound();
        rig.Queue.Send(Call());

        Assert.Equal("rest/v1/rpc/probe", rig.Queue.Next()?.Path);
    }

    [Fact]
    public void Failed_round_makes_waiting_calls_unavailable()
    {
        var rig = Joined();
        rig.Queue.Send(Call());
        var waiting = rig.Queue.Send(Call());
        rig.Start();
        rig.SignedIn();

        rig.Answer(503, "");

        Assert.Equal(CallState.Unavailable, waiting.State);
    }

    [Fact]
    public void Call_answers_with_its_reply()
    {
        var rig = Joined();
        var call = rig.Queue.Send(Call());
        rig.Start();
        rig.SignedIn();

        rig.Answer(200, Ok);

        Assert.Equal((CallState.Answered, new Probe(true)), (call.State, call.Answer));
    }

    [Fact]
    public void Answered_call_applies_its_save_change_and_writes_once()
    {
        var rig = Joined();
        var level = Fixture.Create<Guid>();
        rig.Queue.Send(Call(apply: Shares(level)));
        rig.Start();
        rig.SignedIn();
        var writes = rig.Writes;

        rig.Answer(200, Ok);

        Assert.Equal((true, writes + 1), (rig.Save.SharedLevels.Contains(level), rig.Writes));
    }

    [Fact]
    public void Off_handle_is_off()
    {
        Assert.Equal(CallState.Off, CallHandle<Probe>.Off().State);
    }
}
