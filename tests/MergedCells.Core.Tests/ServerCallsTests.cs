using System.Text.Json;
using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class ServerCallsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);

    private static JsonElement Body(ServerRequest request) => JsonDocument.Parse(request.Body).RootElement;

    public static TheoryData<string, string, string?, string> Requests()
    {
        var player = Guid.NewGuid();
        var layout = Guid.NewGuid().ToString("N");
        return new TheoryData<string, string, string?, string>
        {
            { "sign in", ServerCalls.SignIn().Path, ServerCalls.SignIn().Bearer, "auth/v1/signup" },
            { "refresh", ServerCalls.Refresh("r").Path, ServerCalls.Refresh("r").Bearer, "auth/v1/token?grant_type=refresh_token" },
            { "scores", ServerCalls.SubmitScores(player, [], "t").Path, ServerCalls.SubmitScores(player, [], "t").Bearer, "rest/v1/rpc/submit_scores" },
            { "records", ServerCalls.UploadRecords(player, [], "t").Path, ServerCalls.UploadRecords(player, [], "t").Bearer, "rest/v1/rpc/upload_records" },
            { "delete", ServerCalls.DeletePlayer(player).Path, ServerCalls.DeletePlayer(player).Bearer, "rest/v1/rpc/delete_player" },
            { "spread", ServerCalls.BestMovesSpread(1, layout, "t").Path, ServerCalls.BestMovesSpread(1, layout, "t").Bearer, "rest/v1/rpc/best_moves_spread" },
        };
    }

    // Every function call but the delete carries a session; the delete goes after the session is dropped
    [Theory]
    [MemberData(nameof(Requests))]
    public void Each_request_has_its_path_and_bearer(string name, string path, string? bearer, string expectedPath)
    {
        var needsSession = name is "scores" or "records" or "spread";

        Assert.Equal((expectedPath, needsSession ? "t" : null), (path, bearer));
    }

    [Fact]
    public void Sign_in_sends_an_empty_object()
    {
        Assert.Equal("{}", ServerCalls.SignIn().Body);
    }

    [Theory]
    [AutoData]
    public void Refresh_sends_the_refresh_token(string token)
    {
        Assert.Equal(token, Body(ServerCalls.Refresh(token)).GetProperty("refresh_token").GetString());
    }

    [Theory]
    [AutoData]
    public void Url_joins_the_base_and_the_path(Guid player)
    {
        Assert.Equal("https://example.test/rest/v1/rpc/delete_player", ServerCalls.DeletePlayer(player).Url("https://example.test"));
    }

    [Theory]
    [AutoData]
    public void Headers_carry_the_key_and_the_session_as_a_bearer(string key, string token, Guid player)
    {
        Assert.Equal(
            [$"apikey: {key}", "Content-Type: application/json", $"Authorization: Bearer {token}"],
            ServerCalls.SubmitScores(player, [], token).Headers(key));
    }

    // The publishable key is never sent as a bearer
    [Theory]
    [AutoData]
    public void Headers_without_a_session_have_no_authorization(string key, Guid player)
    {
        Assert.Equal([$"apikey: {key}", "Content-Type: application/json"], ServerCalls.DeletePlayer(player).Headers(key));
    }

    [Theory]
    [AutoData]
    public void Score_body_lists_each_score(Guid player, int level, int moves)
    {
        var body = Body(ServerCalls.SubmitScores(player, [new LevelScore(level, "c28a875eb682c187", moves)], "t"));

        var score = body.GetProperty("scores")[0];
        Assert.Equal(
            (player.ToString("D"), level, "c28a875eb682c187", moves),
            (body.GetProperty("player").GetString(), score.GetProperty("level").GetInt32(), score.GetProperty("layout").GetString(), score.GetProperty("moves").GetInt32()));
    }

    [Theory]
    [AutoData]
    public void Upload_body_embeds_each_line_unchanged(Guid player)
    {
        var lines = new[]
        {
            PlayRecordFile.Write(PlayRecords.Record(PlayRecords.Infinite(TestMaps.Example()))),
            PlayRecordFile.Write(PlayRecords.Record(PlayRecords.Campaign(TestMaps.Example()))),
        };

        var body = Body(ServerCalls.UploadRecords(player, lines, "t"));

        Assert.Equal(
            (player.ToString("D"), lines[0], lines[1]),
            (body.GetProperty("player").GetString(), body.GetProperty("records")[0].GetRawText(), body.GetProperty("records")[1].GetRawText()));
    }

    [Theory]
    [AutoData]
    public void Delete_body_names_the_player(Guid player)
    {
        Assert.Equal(player.ToString("D"), Body(ServerCalls.DeletePlayer(player)).GetProperty("player").GetString());
    }

    [Theory]
    [AutoData]
    public void Session_reply_reads_its_tokens_and_expiry(string access, string refresh, ushort seconds)
    {
        var reply = new ServerReply(200, $$$"""{"access_token":"{{{access}}}","token_type":"bearer","expires_in":{{{seconds + 1}}},"refresh_token":"{{{refresh}}}","user":{"id":"x","is_anonymous":true}}""");

        Assert.Equal(new ServerSession(access, refresh, Now.AddSeconds(seconds + 1)), ServerCalls.ReadSession(reply, Now));
    }

    [Fact]
    public void Score_reply_reads_its_counts()
    {
        var reply = new ServerReply(200, """{"accepted":2,"unknown":[999999],"belowPar":[4]}""");

        var read = ServerCalls.ReadScores(reply)!;
        Assert.Equal((2, "999999", "4", false), (read.Accepted, string.Join(",", read.Unknown), string.Join(",", read.BelowPar), read.PlayerDeleted));
    }

    [Fact]
    public void Score_reply_for_a_deleted_player_reads()
    {
        Assert.Equal((0, true), ServerCalls.ReadScores(new ServerReply(200, """{"accepted":0,"playerDeleted":true}""")) is { } read ? (read.Accepted, read.PlayerDeleted) : (-1, false));
    }

    [Fact]
    public void Upload_reply_reads_its_counts()
    {
        Assert.Equal(new UploadReply(3, 1, 2, false), ServerCalls.ReadUpload(new ServerReply(201, """{"stored":3,"duplicates":1,"skipped":2}""")));
    }

    [Fact]
    public void Upload_reply_for_a_deleted_player_reads()
    {
        Assert.Equal(new UploadReply(0, 0, 0, true), ServerCalls.ReadUpload(new ServerReply(200, """{"stored":0,"playerDeleted":true}""")));
    }

    [Theory]
    [InlineData("""{"deleted":true}""", true)]
    [InlineData("""{"deleted":false}""", false)]
    public void Delete_reply_reads_whether_the_player_was_held(string body, bool deleted)
    {
        Assert.Equal(deleted, ServerCalls.ReadDeleted(new ServerReply(200, body)));
    }

    // A captive portal or proxy answers 200 with its own page; nothing may read as a success then
    public static TheoryData<int, string> Unreadable => new()
    {
        { 200, "<html>Sign in to the wifi</html>" },
        { 200, "{}" },
        { 200, "" },
        { 200, "[]" },
        { 200, """{"accepted":"1","stored":"1","deleted":"true","access_token":1}""" },
        { 500, """{"accepted":1,"stored":1,"deleted":true,"access_token":"a","refresh_token":"r","expires_in":3600}""" },
        { 0, "" },
    };

    [Theory]
    [MemberData(nameof(Unreadable))]
    public void Unreadable_replies_read_as_nothing(int status, string body)
    {
        var reply = new ServerReply(status, body);

        Assert.Equal(
            ((ServerSession?)null, (ScoreReply?)null, (UploadReply?)null, (bool?)null),
            (ServerCalls.ReadSession(reply, Now), ServerCalls.ReadScores(reply), ServerCalls.ReadUpload(reply), ServerCalls.ReadDeleted(reply)));
    }

    [Theory]
    [InlineData(199, false)]
    [InlineData(200, true)]
    [InlineData(204, true)]
    [InlineData(299, true)]
    [InlineData(300, false)]
    [InlineData(0, false)]
    public void Only_2xx_is_ok(int status, bool ok)
    {
        Assert.Equal(ok, new ServerReply(status, "").IsOk);
    }

    [Theory]
    [AutoData]
    public void Spread_body_names_the_level_and_layout(int level, string layout)
    {
        var body = Body(ServerCalls.BestMovesSpread(level, layout, "t"));

        Assert.Equal((level, layout), (body.GetProperty("level").GetInt32(), body.GetProperty("layout").GetString()));
    }

    [Fact]
    public void Spread_reply_reads_its_counts_and_more()
    {
        var read = ServerCalls.ReadSpread(new ServerReply(200, """{"counts":[[6,88],[7,47]],"more":3}"""));

        Assert.Equivalent(new SpreadCounts([new MoveCount(6, 88), new MoveCount(7, 47)], 3), read, strict: true);
    }

    [Fact]
    public void Empty_spread_reply_reads_as_no_counts()
    {
        var read = ServerCalls.ReadSpread(new ServerReply(200, """{"counts":[],"more":0}"""));

        Assert.Equal((0, 0), (read!.Counts.Count, read.More));
    }

    public static TheoryData<int, string> UnreadableSpreads => new()
    {
        { 404, """{"counts":[[6,1]],"more":0}""" },
        { 200, "<html>" },
        { 200, """{"counts":{},"more":0}""" },
        { 200, """{"counts":[[6,1,2]],"more":0}""" },
        { 200, """{"counts":[[0,1]],"more":0}""" },
        { 200, """{"counts":[[6,0]],"more":0}""" },
        { 200, """{"counts":[[7,1],[6,1]],"more":0}""" },
        { 200, """{"counts":[[6,1],[6,2]],"more":0}""" },
        { 200, """{"counts":[[6,1]],"more":-1}""" },
        { 200, """{"counts":[[6,1]]}""" },
        { 200, """{"counts":[["6",1]],"more":0}""" },
        { 200, """{"more":0}""" },
        { 200, """{"counts":[[6,2147483647],[7,1]],"more":0}""" },
        { 200, """{"counts":[[6,2147483647]],"more":1}""" },
    };

    [Theory]
    [MemberData(nameof(UnreadableSpreads))]
    public void Unreadable_spread_replies_give_null(int status, string body)
    {
        Assert.Null(ServerCalls.ReadSpread(new ServerReply(status, body)));
    }
}
