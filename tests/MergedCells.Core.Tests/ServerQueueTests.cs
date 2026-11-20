using System.Text;
using System.Text.Json;
using AutoFixture;

namespace MergedCells.Core.Tests;

public class ServerQueueTests
{
    private static readonly Fixture Fixture = new();

    private static readonly StarsConfig Stars = new()
    {
        TwoStarMinExtraMoves = 2,
        TwoStarExtraParPercent = 50,
        OneStarMinExtraMoves = 4,
        OneStarExtraParPercent = 100,
    };

    private static readonly ServerConfig Config = new()
    {
        Url = "https://example.test",
        PublishableKey = "k",
        TimeoutSeconds = 15,
        RetrySeconds = 60,
        RefreshMarginSeconds = 60,
        ScoresPerRequest = 200,
        RecordsPerRequest = 3,
        RecordBytesPerRequest = 1_000_000,
    };

    // The example map at par 3, under three IDs
    private static readonly Level[] Levels = [new(1, "One", 1, TestMaps.Example(), 3), new(2, "Two", 1, TestMaps.Example(), 3), new(3, "Three", 1, TestMaps.Example(), 3)];

    private static readonly string Layout = LevelFingerprint.Of(Levels[0]);

    // A queue over a real save, a records "file" held as lines with byte offsets, and a clock the test moves
    private sealed class Rig
    {
        public Rig(ServerConfig? config = null)
        {
            Queue = new ServerQueue(
                config ?? Config,
                Save,
                Levels,
                (offset, max) =>
                {
                    var from = Lines.Count > 0 && offset > Lines[^1].End ? 0 : offset;
                    return Lines.Where(line => line.End > from).Take(max).ToArray();
                },
                () =>
                {
                    Writes++;
                    return Writable;
                },
                () => MadePlayer,
                () => Now,
                Log.Add);
            Queue.DeletionConfirmed += id => Confirmed.Add(id);
            Queue.RoundEnded += finished => Rounds.Add(finished);
        }

        public SaveGame Save { get; } = new();

        public ServerQueue Queue { get; }

        public List<RecordLine> Lines { get; } = [];

        public List<string> Log { get; } = [];

        public List<Guid> Confirmed { get; } = [];

        public List<bool> Rounds { get; } = [];

        public Guid MadePlayer { get; } = Fixture.Create<Guid>();

        public DateTimeOffset Now { get; set; } = new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);

        public int Writes { get; private set; }

        public bool Writable { get; set; } = true;

        public Guid Player => Save.Player!.Value;

        public void Leaderboards() => Consents.Answer(Save, ConsentWordings.Leaderboards, true, () => Fixture.Create<Guid>());

        public void PlayData() => Consents.Answer(Save, ConsentWordings.PlayData, true, () => Fixture.Create<Guid>());

        public void Best(int level, int moves) => Save.RecordFinish(Levels[level - 1], moves, Stars);

        public RecordLine AddLine(string text, PlayRecord? record)
        {
            var end = (Lines.Count > 0 ? Lines[^1].End : 0) + Encoding.UTF8.GetByteCount(text) + 1;
            var line = new RecordLine(end, text, record);
            Lines.Add(line);
            return line;
        }

        public PlayRecord AddRecord(Guid? player = null, int consent = 1)
        {
            var record = PlayRecords.Record(PlayRecords.Infinite(TestMaps.Example()) with { Player = player ?? Player, Consent = consent });
            AddLine(consent >= 1 ? PlayRecordFile.Write(record) : "{\"version\":1}", record);
            return record;
        }

        public ServerRequest Start()
        {
            Queue.Wake();
            return Queue.Next() ?? throw new InvalidOperationException("No request.");
        }

        // Answers the outstanding request and returns the next one, or null when the round is over
        public ServerRequest? Answer(int status, string body)
        {
            Queue.Reply(new ServerReply(status, body));
            return Queue.Next();
        }

        public ServerRequest? SignedIn(string access = "a1", string refresh = "r1") => Answer(200, Session(access, refresh));
    }

    private static string Session(string access, string refresh, int seconds = 3600) =>
        $$$"""{"access_token":"{{{access}}}","token_type":"bearer","expires_in":{{{seconds}}},"refresh_token":"{{{refresh}}}","user":{"id":"u","is_anonymous":true}}""";

    private static string Accepted(int count) => $$"""{"accepted":{{count}},"unknown":[],"belowPar":[]}""";

    private static string Stored(int count) => $$"""{"stored":{{count}},"duplicates":0,"skipped":0}""";

    private static string SpreadReply(params (int Moves, int Players)[] counts) =>
        "{\"counts\":[" + string.Join(",", counts.Select(count => $"[{count.Moves},{count.Players}]")) + "],\"more\":0}";

    private static JsonElement Body(ServerRequest request) => JsonDocument.Parse(request.Body).RootElement;

    private static int[] ScoreLevels(ServerRequest request) =>
        Body(request).GetProperty("scores").EnumerateArray().Select(score => score.GetProperty("level").GetInt32()).ToArray();

    private static string RecordIds(ServerRequest request) =>
        string.Join(",", Body(request).GetProperty("records").EnumerateArray().Select(record => record.GetProperty("id").GetString()));

    private static string Ids(params PlayRecord[] records) => string.Join(",", records.Select(record => record.Start.Id.ToString("D")));

    [Fact]
    public void Nothing_is_sent_while_neither_kind_is_given()
    {
        var rig = new Rig();
        rig.Best(1, 4);
        rig.Save.Player = Fixture.Create<Guid>();
        rig.AddRecord();

        rig.Queue.Wake();

        Assert.Null(rig.Queue.Next());
    }

    [Fact]
    public void Server_off_sends_nothing()
    {
        var rig = new Rig(Config with { Url = "", PublishableKey = "" });
        rig.Leaderboards();
        rig.Best(1, 4);

        rig.Queue.Wake();

        Assert.Null(rig.Queue.Next());
    }

    [Fact]
    public void Nothing_is_sent_before_a_wake()
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.Best(1, 4);

        Assert.Null(rig.Queue.Next());
    }

    [Fact]
    public void Pending_deletion_goes_first_without_a_bearer()
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.Best(1, 4);
        var old = Fixture.Create<Guid>();
        rig.Save.AddPendingDeletion(old);

        var request = rig.Start();

        Assert.Equal(("rest/v1/rpc/delete_player", (string?)null, old.ToString("D")), (request.Path, request.Bearer, Body(request).GetProperty("player").GetString()));
    }

    [Fact]
    public void Confirmed_deletion_leaves_the_save_and_raises_the_event()
    {
        var rig = new Rig();
        var old = Fixture.Create<Guid>();
        rig.Save.AddPendingDeletion(old);
        rig.Start();

        rig.Answer(200, """{"deleted":true}""");

        Assert.Equal((0, old, 1), (rig.Save.PendingDeletions.Count, rig.Confirmed.Single(), rig.Writes));
    }

    [Fact]
    public void Scores_need_a_session_first_and_sign_in_without_one()
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.Best(1, 4);

        var first = rig.Start();
        var second = rig.SignedIn("a1", "r1");

        Assert.Equal(
            ("auth/v1/signup", "rest/v1/rpc/submit_scores", "a1", "r1"),
            (first.Path, second!.Path, second.Bearer, rig.Save.SessionToken));
    }

    [Fact]
    public void Stored_session_is_refreshed_not_replaced()
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.Best(1, 4);
        rig.Save.SessionToken = "r0";

        var first = rig.Start();
        var second = rig.SignedIn("a1", "r1");

        Assert.Equal(
            ("auth/v1/token?grant_type=refresh_token", "r0", "a1", "r1"),
            (first.Path, Body(first).GetProperty("refresh_token").GetString(), second!.Bearer, rig.Save.SessionToken));
    }

    // The app killed mid-refresh leaves an old token that the server refuses: sign in anew rather than loop on it
    [Fact]
    public void Refused_refresh_signs_in_anew()
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.Best(1, 4);
        rig.Save.SessionToken = "r0";
        rig.Start();

        var next = rig.Answer(400, """{"code":400,"error_code":"refresh_token_already_used"}""");

        Assert.Equal(("auth/v1/signup", "r0"), (next!.Path, rig.Save.SessionToken));
    }

    [Fact]
    public void Expiring_access_token_is_renewed_before_sending()
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.Best(1, 4);
        rig.Start();
        rig.Answer(200, Session("a1", "r1", seconds: 100));
        rig.Answer(200, Accepted(1));
        rig.Best(2, 4);
        rig.Now = rig.Now.AddSeconds(41);

        var next = rig.Start();

        Assert.Equal("auth/v1/token?grant_type=refresh_token", next.Path);
    }

    [Fact]
    public void Function_401_gets_a_new_token_and_tries_once_more()
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.Best(1, 4);
        rig.Start();
        rig.SignedIn("a1", "r1");

        var renew = rig.Answer(401, """{"message":"JWT expired"}""");
        var again = rig.SignedIn("a2", "r2");

        Assert.Equal(
            ("auth/v1/token?grant_type=refresh_token", "rest/v1/rpc/submit_scores", "a2"),
            (renew!.Path, again!.Path, again.Bearer));
    }

    [Fact]
    public void Second_401_fails_the_round()
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.Best(1, 4);
        rig.Start();
        rig.SignedIn();
        rig.Answer(401, "{}");
        rig.SignedIn("a2", "r2");

        var next = rig.Answer(401, "{}");

        Assert.Equal(((ServerRequest?)null, false), (next, rig.Rounds.Single()));
    }

    [Fact]
    public void Scores_only_while_leaderboards_are_given()
    {
        var rig = new Rig();
        rig.PlayData();
        rig.Best(1, 4);

        rig.Queue.Wake();

        Assert.Null(rig.Queue.Next());
    }

    [Fact]
    public void Score_body_holds_each_best_with_its_layout()
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.Best(1, 4);
        rig.Best(3, 5);
        rig.Start();

        var request = rig.SignedIn()!;

        var scores = Body(request).GetProperty("scores");
        Assert.Equal(
            (rig.Player.ToString("D"), "1,3", LevelFingerprint.Of(Levels[0]), "4,5"),
            (Body(request).GetProperty("player").GetString(),
                string.Join(",", ScoreLevels(request)),
                scores[0].GetProperty("layout").GetString(),
                string.Join(",", scores.EnumerateArray().Select(score => score.GetProperty("moves").GetInt32()))));
    }

    // A best saved against an edited level is no best for the level as it is now
    [Fact]
    public void Only_bests_that_match_the_level_are_sent()
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.Best(1, 4);
        rig.Save.RecordFinish(new Level(2, "Two", 1, TestMaps.Example(), 4), 5, Stars);
        rig.Start();

        Assert.Equal([1], ScoreLevels(rig.SignedIn()!));
    }

    [Fact]
    public void Scores_go_in_batches()
    {
        var rig = new Rig(Config with { ScoresPerRequest = 2 });
        rig.Leaderboards();
        rig.Best(1, 4);
        rig.Best(2, 4);
        rig.Best(3, 4);
        rig.Start();
        var first = rig.SignedIn()!;

        var second = rig.Answer(200, Accepted(2))!;

        Assert.Equal(("1,2", "3"), (string.Join(",", ScoreLevels(first)), string.Join(",", ScoreLevels(second))));
    }

    [Fact]
    public void Confirmed_scores_are_not_sent_again_this_run()
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.Best(1, 4);
        rig.Start();
        rig.SignedIn();
        rig.Answer(200, Accepted(1));
        rig.Best(2, 4);

        Assert.Equal([2], ScoreLevels(rig.Start()));
    }

    [Fact]
    public void A_better_best_is_sent_again()
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.Best(1, 5);
        rig.Start();
        rig.SignedIn();
        rig.Answer(200, Accepted(1));
        rig.Best(1, 3);

        Assert.Equal(3, Body(rig.Start()).GetProperty("scores")[0].GetProperty("moves").GetInt32());
    }

    [Fact]
    public void Round_with_everything_confirmed_ends_without_a_request()
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.Best(1, 4);
        rig.Start();
        rig.SignedIn();
        rig.Answer(200, Accepted(1));

        rig.Queue.Wake();

        Assert.Equal(((ServerRequest?)null, "True,True"), (rig.Queue.Next(), string.Join(",", rig.Rounds)));
    }

    // After "Delete my data" with leaderboards still on, the bests go up under a new ID
    [Fact]
    public void Leaderboards_without_a_player_make_one_and_write_the_save_first()
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.Save.Player = null;
        rig.Best(1, 4);

        rig.Start();

        Assert.Equal(((Guid?)rig.MadePlayer, 1), (rig.Save.Player, rig.Writes));
    }

    [Fact]
    public void No_player_is_made_when_there_is_no_best()
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.Save.Player = null;

        rig.Queue.Wake();

        Assert.Equal(((ServerRequest?)null, (Guid?)null), (rig.Queue.Next(), rig.Save.Player));
    }

    [Fact]
    public void Records_only_while_play_data_is_given()
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.AddRecord();

        rig.Queue.Wake();

        Assert.Null(rig.Queue.Next());
    }

    [Fact]
    public void Records_go_up_with_their_lines_as_they_are()
    {
        var rig = new Rig();
        rig.PlayData();
        var record = rig.AddRecord();
        rig.Start();

        var request = rig.SignedIn()!;

        Assert.Equal(
            ("rest/v1/rpc/upload_records", rig.Player.ToString("D"), rig.Lines[0].Text),
            (request.Path, Body(request).GetProperty("player").GetString(), Body(request).GetProperty("records")[0].GetRawText()));
    }

    [Fact]
    public void Ineligible_lines_are_passed_over()
    {
        var rig = new Rig();
        rig.PlayData();
        rig.AddRecord(player: Fixture.Create<Guid>());
        rig.AddRecord(consent: 0);
        rig.AddLine("not json", null);
        var kept = rig.AddRecord();
        rig.Start();

        var request = rig.SignedIn()!;
        rig.Answer(200, Stored(1));

        Assert.Equal((Ids(kept), rig.Lines[^1].End), (RecordIds(request), rig.Save.RecordsSent));
    }

    [Fact]
    public void Records_batch_by_count()
    {
        var rig = new Rig(Config with { RecordsPerRequest = 2 });
        rig.PlayData();
        var records = new[] { rig.AddRecord(), rig.AddRecord(), rig.AddRecord() };
        rig.Start();
        var first = rig.SignedIn()!;

        var second = rig.Answer(200, Stored(2))!;

        Assert.Equal((Ids(records[0], records[1]), Ids(records[2])), (RecordIds(first), RecordIds(second)));
    }

    [Fact]
    public void Records_batch_by_bytes()
    {
        var rig = new Rig();
        rig.PlayData();
        var records = new[] { rig.AddRecord(), rig.AddRecord(), rig.AddRecord() };
        var twoLines = rig.Lines.Take(2).Sum(line => Encoding.UTF8.GetByteCount(line.Text));
        rig = WithLines(Config with { RecordBytesPerRequest = twoLines }, rig);
        rig.Start();
        var first = rig.SignedIn()!;

        var second = rig.Answer(200, Stored(2))!;

        Assert.Equal((Ids(records[0], records[1]), Ids(records[2])), (RecordIds(first), RecordIds(second)));
    }

    // A record bigger than the byte limit can't wait for a batch it fits in
    [Fact]
    public void Oversized_record_goes_alone()
    {
        var rig = new Rig(Config with { RecordBytesPerRequest = 10 });
        rig.PlayData();
        var records = new[] { rig.AddRecord(), rig.AddRecord() };
        rig.Start();
        var first = rig.SignedIn()!;

        var second = rig.Answer(200, Stored(1))!;

        Assert.Equal((Ids(records[0]), Ids(records[1])), (RecordIds(first), RecordIds(second)));
    }

    [Fact]
    public void Stretch_with_no_eligible_line_moves_the_offset_without_a_request()
    {
        var rig = new Rig();
        rig.PlayData();
        rig.AddRecord(player: Fixture.Create<Guid>());
        rig.AddLine("not json", null);

        rig.Queue.Wake();

        Assert.Equal(((ServerRequest?)null, rig.Lines[^1].End), (rig.Queue.Next(), rig.Save.RecordsSent));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(500)]
    [InlineData(429)]
    public void Records_offset_moves_only_on_a_2xx(int status)
    {
        var rig = new Rig();
        rig.PlayData();
        rig.AddRecord();
        rig.Start();
        rig.SignedIn();

        rig.Answer(status, "");

        Assert.Equal((0L, false), (rig.Save.RecordsSent, rig.Rounds.Single()));
    }

    [Fact]
    public void Records_offset_moves_past_the_batch_on_a_2xx()
    {
        var rig = new Rig();
        rig.PlayData();
        rig.AddRecord();
        rig.AddRecord();
        rig.Start();
        rig.SignedIn();

        rig.Answer(200, Stored(2));

        Assert.Equal(rig.Lines[^1].End, rig.Save.RecordsSent);
    }

    // A reply after "Delete my data" belongs to the deleted ID
    [Fact]
    public void Reply_for_a_deleted_player_changes_nothing()
    {
        var rig = new Rig();
        rig.PlayData();
        rig.AddRecord();
        rig.Start();
        rig.SignedIn();
        var old = rig.Player;
        DataDeletion.Run(rig.Save, () => true, () => null);

        var next = rig.Answer(200, Stored(1));

        Assert.Equal((0L, "rest/v1/rpc/delete_player", old.ToString("D")), (rig.Save.RecordsSent, next!.Path, Body(next).GetProperty("player").GetString()));
    }

    [Fact]
    public void Score_reply_for_a_deleted_player_confirms_nothing()
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.Best(1, 4);
        rig.Start();
        rig.SignedIn();
        DataDeletion.Run(rig.Save, () => true, () => null);
        rig.Answer(200, Accepted(1));
        rig.Answer(200, """{"deleted":true}""");

        var next = rig.SignedIn("a2", "r2");

        Assert.Equal(("rest/v1/rpc/submit_scores", rig.MadePlayer.ToString("D")), (next!.Path, Body(next).GetProperty("player").GetString()));
    }

    // A captive portal answers 200 with its own page
    [Fact]
    public void Unreadable_2xx_fails_the_round()
    {
        var rig = new Rig();
        rig.PlayData();
        rig.AddRecord();
        rig.Start();
        rig.SignedIn();

        var next = rig.Answer(200, "<html>Sign in to the wifi</html>");

        Assert.Equal(((ServerRequest?)null, 0L, false), (next, rig.Save.RecordsSent, rig.Rounds.Single()));
    }

    [Fact]
    public void Failed_sign_in_fails_the_round()
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.Best(1, 4);
        rig.Start();

        var next = rig.Answer(422, """{"code":422,"error_code":"anonymous_provider_disabled"}""");

        Assert.Equal(((ServerRequest?)null, (string?)null, false), (next, rig.Save.SessionToken, rig.Rounds.Single()));
    }

    [Fact]
    public void Failed_round_ignores_wakes_until_the_retry_gap_passes()
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.Best(1, 4);
        rig.Start();
        rig.Answer(0, "");
        rig.Now = rig.Now.AddSeconds(Config.RetrySeconds - 1);
        rig.Queue.Wake();
        var during = rig.Queue.Next();
        rig.Now = rig.Now.AddSeconds(1);

        rig.Queue.Wake();

        Assert.Equal(((ServerRequest?)null, "auth/v1/signup"), (during, rig.Queue.Next()?.Path));
    }

    [Fact]
    public void Forced_wake_skips_the_gap()
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.Best(1, 4);
        rig.Start();
        rig.Answer(0, "");

        rig.Queue.Wake(force: true);

        Assert.Equal("auth/v1/signup", rig.Queue.Next()?.Path);
    }

    [Fact]
    public void Work_added_during_a_round_goes_in_the_same_round()
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.Best(1, 4);
        rig.Start();
        rig.SignedIn();
        rig.Best(2, 4);

        var next = rig.Answer(200, Accepted(1))!;

        Assert.Equal([2], ScoreLevels(next));
    }

    // "Delete my data" pressed while an offline round is in flight still gets its own try
    [Fact]
    public void Forced_wake_during_a_round_that_fails_runs_another()
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.Best(1, 4);
        rig.Start();
        rig.Queue.Wake(force: true);

        var next = rig.Answer(0, "");

        Assert.Equal("auth/v1/signup", next?.Path);
    }

    [Fact]
    public void Plain_wake_during_a_round_that_fails_waits_for_the_gap()
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.Best(1, 4);
        rig.Start();
        rig.Queue.Wake();

        Assert.Null(rig.Answer(0, ""));
    }

    [Fact]
    public void Next_while_a_request_is_unanswered_throws()
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.Best(1, 4);
        rig.Start();

        Assert.Throws<InvalidOperationException>(() => rig.Queue.Next());
    }

    [Fact]
    public void Reply_with_none_outstanding_throws()
    {
        Assert.Throws<InvalidOperationException>(() => new Rig().Queue.Reply(new ServerReply(200, "{}")));
    }

    // A second rig whose lines are the first's, under another config
    private static Rig WithLines(ServerConfig config, Rig from)
    {
        var rig = new Rig(config);
        rig.PlayData();
        rig.Save.Player = from.Player;
        foreach (var line in from.Lines)
        {
            rig.Lines.Add(line);
        }
        return rig;
    }

    // A run that can't keep its session or player ID would send data nobody can delete: the next run wouldn't know it
    [Fact]
    public void Session_that_cannot_be_saved_stops_sending_for_the_run()
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.Best(1, 4);
        rig.Start();
        rig.Writable = false;

        var next = rig.SignedIn();
        rig.Queue.Wake(force: true);

        Assert.Equal(((ServerRequest?)null, (ServerRequest?)null, false), (next, rig.Queue.Next(), rig.Rounds.Single()));
    }

    [Fact]
    public void New_player_that_cannot_be_saved_sends_nothing()
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.Save.Player = null;
        rig.Best(1, 4);
        rig.Writable = false;

        rig.Queue.Wake();

        Assert.Null(rig.Queue.Next());
    }

    // A renewal answered after "Delete my data" would carry the old sign-in over to the new player ID
    [Fact]
    public void Session_reply_after_a_deletion_is_ignored()
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.Best(1, 4);
        rig.Save.SessionToken = "r0";
        rig.Start();
        DataDeletion.Run(rig.Save, () => true, () => null);

        var next = rig.SignedIn("a1", "r1");
        rig.Answer(200, """{"deleted":true}""");

        Assert.Equal(("rest/v1/rpc/delete_player", (string?)null), (next!.Path, rig.Save.SessionToken));
    }

    [Fact]
    public void New_player_after_a_deletion_signs_in_anew()
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.Best(1, 4);
        rig.Save.SessionToken = "r0";
        rig.Start();
        DataDeletion.Run(rig.Save, () => true, () => null);
        rig.SignedIn("a1", "r1");

        var next = rig.Answer(200, """{"deleted":true}""");

        Assert.Equal("auth/v1/signup", next?.Path);
    }

    [Fact]
    public void Fetch_goes_after_the_scores_and_before_the_records()
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.PlayData();
        rig.Best(1, 4);
        rig.AddRecord();
        rig.Queue.Fetch(1, Layout);

        var paths = new[] { rig.Start(), rig.SignedIn(), rig.Answer(200, Accepted(1)), rig.Answer(200, SpreadReply((4, 1))) }.Select(request => request?.Path);

        Assert.Equal(["auth/v1/signup", "rest/v1/rpc/submit_scores", "rest/v1/rpc/best_moves_spread", "rest/v1/rpc/upload_records"], paths);
    }

    [Fact]
    public void Fetch_answers_with_the_counts()
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.Best(1, 4);
        var fetch = rig.Queue.Fetch(1, Layout);
        rig.Start();
        rig.SignedIn();
        rig.Answer(200, Accepted(1));

        rig.Answer(200, SpreadReply((4, 2)));

        Assert.Equal((SpreadFetchState.Answered, new MoveCount(4, 2)), (fetch.State, fetch.Counts!.Counts.Single()));
    }

    [Fact]
    public void Fetch_sends_the_level_and_layout()
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.Queue.Fetch(1, Layout);
        rig.Start();

        var request = rig.SignedIn()!;

        Assert.Equal((1, Layout), (Body(request).GetProperty("level").GetInt32(), Body(request).GetProperty("layout").GetString()));
    }

    [Fact]
    public void Fetch_needs_a_session_first()
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.Queue.Fetch(1, Layout);

        var paths = new[] { rig.Start(), rig.SignedIn() }.Select(request => request?.Path);

        Assert.Equal(["auth/v1/signup", "rest/v1/rpc/best_moves_spread"], paths);
    }

    [Fact]
    public void Fetch_without_leaderboards_is_unavailable_and_the_round_goes_on()
    {
        var rig = new Rig();
        rig.PlayData();
        rig.AddRecord();
        var fetch = rig.Queue.Fetch(1, Layout);
        rig.Start();

        var next = rig.SignedIn();

        Assert.Equal((SpreadFetchState.Unavailable, "rest/v1/rpc/upload_records"), (fetch.State, next?.Path));
    }

    [Fact]
    public void Fetch_without_a_player_is_unavailable()
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.Save.Player = null;
        var fetch = rig.Queue.Fetch(1, Layout);

        var next = rig.Queue.Next();

        Assert.Equal((SpreadFetchState.Unavailable, (ServerRequest?)null), (fetch.State, next));
    }

    [Fact]
    public void Fetch_in_the_retry_gap_is_unavailable_at_once()
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.Best(1, 4);
        rig.Start();
        rig.Answer(503, "");
        rig.Now = rig.Now.AddSeconds(30);

        Assert.Equal(SpreadFetchState.Unavailable, rig.Queue.Fetch(1, Layout).State);
    }

    [Fact]
    public void Fetch_after_the_retry_gap_is_sent()
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.Best(1, 4);
        rig.Start();
        rig.Answer(503, "");
        rig.Now = rig.Now.AddSeconds(61);
        rig.Queue.Fetch(1, Layout);
        rig.Queue.Next();
        rig.SignedIn();

        var next = rig.Answer(200, Accepted(1));

        Assert.Equal("rest/v1/rpc/best_moves_spread", next?.Path);
    }

    [Fact]
    public void Queue_that_could_not_keep_a_session_is_stopped()
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.Best(1, 4);
        rig.Writable = false;
        rig.Start();

        rig.SignedIn();

        Assert.True(rig.Queue.IsStopped);
    }

    [Fact]
    public void Fetch_after_the_queue_stopped_is_off()
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.Best(1, 4);
        rig.Writable = false;
        rig.Start();
        rig.SignedIn();

        Assert.Equal(SpreadFetchState.Off, rig.Queue.Fetch(1, Layout).State);
    }

    [Fact]
    public void Server_off_fetch_is_off()
    {
        var rig = new Rig(Config with { Url = "", PublishableKey = "" });
        rig.Leaderboards();

        Assert.Equal(SpreadFetchState.Off, rig.Queue.Fetch(1, Layout).State);
    }

    [Fact]
    public void Newer_fetch_replaces_a_waiting_one()
    {
        var rig = new Rig();
        rig.Leaderboards();
        var first = rig.Queue.Fetch(1, Layout);
        var second = rig.Queue.Fetch(1, Layout);
        rig.Start();
        rig.SignedIn();

        rig.Answer(200, SpreadReply((4, 1)));

        Assert.Equal((SpreadFetchState.Unavailable, SpreadFetchState.Answered), (first.State, second.State));
    }

    [Fact]
    public void Refused_token_on_a_fetch_renews_and_retries_once()
    {
        var rig = new Rig();
        rig.Leaderboards();
        var fetch = rig.Queue.Fetch(1, Layout);
        rig.Start();
        rig.SignedIn();

        var paths = new[] { rig.Answer(401, ""), rig.SignedIn("a2", "r2"), rig.Answer(200, SpreadReply((4, 1))) }.Select(request => request?.Path);

        Assert.Equal(["auth/v1/token?grant_type=refresh_token", "rest/v1/rpc/best_moves_spread", null], paths);
        Assert.Equal(SpreadFetchState.Answered, fetch.State);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(503)]
    [InlineData(429)]
    public void Fetch_with_no_reply_fails_the_round_and_sends_no_records(int status)
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.PlayData();
        rig.AddRecord();
        var fetch = rig.Queue.Fetch(1, Layout);
        rig.Start();
        rig.SignedIn();

        var next = rig.Answer(status, "");

        Assert.Equal((SpreadFetchState.Unavailable, false, (ServerRequest?)null), (fetch.State, rig.Rounds.Last(), next));
    }

    [Theory]
    [InlineData(404, """{"message":"x"}""")]
    [InlineData(200, "<html>")]
    public void Fetch_refused_or_unreadable_goes_on_to_the_records(int status, string body)
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.PlayData();
        rig.AddRecord();
        var fetch = rig.Queue.Fetch(1, Layout);
        rig.Start();
        rig.SignedIn();

        var next = rig.Answer(status, body);

        Assert.Equal((SpreadFetchState.Unavailable, "rest/v1/rpc/upload_records"), (fetch.State, next?.Path));
    }

    [Fact]
    public void Fetch_reply_after_a_deletion_still_answers()
    {
        var rig = new Rig();
        rig.Leaderboards();
        var fetch = rig.Queue.Fetch(1, Layout);
        rig.Start();
        rig.SignedIn();
        rig.Save.Player = Fixture.Create<Guid>();

        rig.Answer(200, SpreadReply((4, 1)));

        Assert.Equal(SpreadFetchState.Answered, fetch.State);
    }

    [Fact]
    public void Failed_round_makes_a_waiting_fetch_unavailable()
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.Best(1, 4);
        var fetch = rig.Queue.Fetch(1, Layout);
        rig.Start();
        rig.SignedIn();

        rig.Answer(503, "");

        Assert.Equal(SpreadFetchState.Unavailable, fetch.State);
    }

    // Chunk 33's rule: a reply landing after "Delete my data" never decides how the round ends, which the deletion line reads
    [Fact]
    public void Spread_failure_after_a_deletion_leaves_the_round_going()
    {
        var rig = new Rig();
        rig.Leaderboards();
        var fetch = rig.Queue.Fetch(1, Layout);
        rig.Start();
        rig.SignedIn();
        DataDeletion.Run(rig.Save, () => true, () => null);

        var next = rig.Answer(503, "");

        Assert.Equal((SpreadFetchState.Unavailable, "rest/v1/rpc/delete_player", 0), (fetch.State, next?.Path, rig.Rounds.Count));
    }

    [Fact]
    public void Cancelled_fetch_is_not_sent()
    {
        var rig = new Rig();
        rig.Leaderboards();
        var fetch = rig.Queue.Fetch(1, Layout);

        fetch.Cancel();

        Assert.Equal((SpreadFetchState.Unavailable, (ServerRequest?)null), (fetch.State, rig.Queue.Next()));
    }

    [Fact]
    public void Fetch_waiting_when_the_queue_stops_is_off()
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.Best(1, 4);
        rig.Writable = false;
        var fetch = rig.Queue.Fetch(1, Layout);
        rig.Start();

        rig.SignedIn();

        Assert.Equal(SpreadFetchState.Off, fetch.State);
    }

    [Fact]
    public void Unreadable_spread_reply_is_logged_as_unreadable()
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.Queue.Fetch(1, Layout);
        rig.Start();
        rig.SignedIn();

        rig.Answer(200, "<html>");

        Assert.Contains("spread: the reply didn't read (200); the round goes on", rig.Log);
    }

    // A forced wake can start a round inside the retry gap; a card finishing during it still gets its spread
    [Fact]
    public void Fetch_in_the_retry_gap_during_a_round_is_kept()
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.Best(1, 4);
        rig.Start();
        rig.Answer(503, "");
        rig.Now = rig.Now.AddSeconds(30);
        rig.Queue.Wake(force: true);
        rig.Queue.Next();
        var fetch = rig.Queue.Fetch(1, Layout);
        rig.SignedIn();

        var next = rig.Answer(200, Accepted(1));

        Assert.Equal((SpreadFetchState.Waiting, "rest/v1/rpc/best_moves_spread"), (fetch.State, next?.Path));
    }

    [Fact]
    public void Fetch_made_mid_round_goes_after_the_scores()
    {
        var rig = new Rig();
        rig.Leaderboards();
        rig.PlayData();
        rig.Best(1, 4);
        rig.AddRecord();
        rig.Start();
        rig.SignedIn();
        rig.Queue.Fetch(1, Layout);

        var paths = new[] { rig.Answer(200, Accepted(1)), rig.Answer(200, SpreadReply((4, 1))) }.Select(request => request?.Path);

        Assert.Equal(["rest/v1/rpc/best_moves_spread", "rest/v1/rpc/upload_records"], paths);
    }

    [Fact]
    public void Refused_token_with_a_newer_fetch_waiting_drops_the_older()
    {
        var rig = new Rig();
        rig.Leaderboards();
        var first = rig.Queue.Fetch(1, Layout);
        rig.Start();
        rig.SignedIn();
        var second = rig.Queue.Fetch(1, Layout);
        rig.Answer(401, "");
        rig.SignedIn("a2", "r2");

        rig.Answer(200, SpreadReply((4, 1)));

        Assert.Equal((SpreadFetchState.Unavailable, SpreadFetchState.Answered), (first.State, second.State));
    }

    [Fact]
    public void Second_refused_token_on_a_fetch_fails_the_round()
    {
        var rig = new Rig();
        rig.Leaderboards();
        var fetch = rig.Queue.Fetch(1, Layout);
        rig.Start();
        rig.SignedIn();
        rig.Answer(401, "");
        rig.SignedIn("a2", "r2");

        rig.Answer(401, "");

        Assert.Equal((SpreadFetchState.Unavailable, false), (fetch.State, rig.Rounds.Last()));
    }
}
