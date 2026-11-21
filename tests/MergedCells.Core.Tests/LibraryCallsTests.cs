using System.Text.Json;
using AutoFixture;
using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class LibraryCallsTests
{
    private static readonly Fixture Fixture = new();

    private static readonly CustomLevelsConfig CustomLevels = new()
    {
        NewSide = 7,
        MinSide = 2,
        MaxSide = 30,
        MaxNameLength = 16,
        UndoLimit = 5,
        Bands =
        [
            new DifficultyBand { Name = "Easy", MinScore = 0 },
            new DifficultyBand { Name = "Medium", MinScore = 20 },
            new DifficultyBand { Name = "Hard", MinScore = 40 },
        ],
    };

    private static readonly LibraryConfig Library = new() { PageSize = 20, SizeSteps = [10, 20] };

    private static JsonElement Arguments<T>(ServerCall<T> call, Guid player)
        where T : class =>
        JsonDocument.Parse(ServerCalls.Json(writer => call.Arguments(writer, player))).RootElement;

    private static T? Read<T>(ServerCall<T> call, string body)
        where T : class =>
        ServerCalls.Read(new ServerReply(200, body), root => call.Read(root));

    private static string Text(JsonElement element, string key) => element.GetProperty(key).ToString();

    private static CustomLevel Level() => ShareCodeTests.PinnedLevel() with { Id = Fixture.Create<Guid>() };

    // Each call's function, and whether it needs the library's yes, a session, and the player's press
    public static TheoryData<string, string, ConsentKind?, bool, bool> Shapes()
    {
        var id = Fixture.Create<Guid>();
        var data = new TheoryData<string, string, ConsentKind?, bool, bool>();
        void Add<T>(string name, ServerCall<T> call)
            where T : class =>
            data.Add(name, call.Function, call.Needs, call.Session, call.Forced);
        Add("levels", LibraryCalls.Levels(new LibraryQuery(LibraryOrder.Newest), 0, Library, CustomLevels));
        Add("mine", LibraryCalls.Mine());
        Add("publish", LibraryCalls.Publish(Level(), 4, 30));
        Add("remove", LibraryCalls.Remove(id));
        Add("finish", LibraryCalls.Finish(id));
        Add("like", LibraryCalls.Like(id, true));
        Add("report", LibraryCalls.Report(id, ReportReason.Name));
        Add("block", LibraryCalls.Block(id));
        Add("unblock", LibraryCalls.Unblock());
        return data;
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void Call_has_its_function_consent_session_and_press(string name, string function, ConsentKind? needs, bool session, bool forced)
    {
        var expected = name switch
        {
            "levels" or "mine" => ("library_levels", (ConsentKind?)ConsentKind.Library, true, true),
            "remove" => ("library_remove", null, false, true),
            "finish" => ("library_finish", ConsentKind.Library, true, false),
            "unblock" => ("library_unblock", ConsentKind.Library, true, true),
            _ => ("library_" + name, ConsentKind.Library, true, true),
        };

        Assert.Equal(expected, (function, needs, session, forced));
    }

    [Fact]
    public void Levels_sends_the_order_filters_page_and_size()
    {
        var player = Fixture.Create<Guid>();
        var call = LibraryCalls.Levels(new LibraryQuery(LibraryOrder.Liked, Band: 1, SizeStep: 2), 3, Library, CustomLevels);

        var body = Arguments(call, player);

        Assert.Equal(
            (player.ToString("D"), "liked", "20", "39", "21", "30", "3", "20"),
            (Text(body, "player"), Text(body, "view"), Text(body, "min_score"), Text(body, "max_score"), Text(body, "min_side"),
                Text(body, "max_side"), Text(body, "page"), Text(body, "size")));
    }

    [Fact]
    public void Levels_sends_null_filters_for_any()
    {
        var body = Arguments(LibraryCalls.Levels(new LibraryQuery(LibraryOrder.Played), 0, Library, CustomLevels), Fixture.Create<Guid>());

        Assert.Equal(
            ("played", JsonValueKind.Null, JsonValueKind.Null, JsonValueKind.Null, JsonValueKind.Null),
            (Text(body, "view"), body.GetProperty("min_score").ValueKind, body.GetProperty("max_score").ValueKind,
                body.GetProperty("min_side").ValueKind, body.GetProperty("max_side").ValueKind));
    }

    [Fact]
    public void Mine_asks_for_the_mine_view()
    {
        Assert.Equal("mine", Text(Arguments(LibraryCalls.Mine(), Fixture.Create<Guid>()), "view"));
    }

    [Fact]
    public void Publish_sends_the_code_and_what_the_list_shows()
    {
        var level = Level();
        var player = Fixture.Create<Guid>();

        var body = Arguments(LibraryCalls.Publish(level, 4, 37), player);

        Assert.Equal(
            (player.ToString("D"), level.Id.ToString("D"), ShareCode.Write(level), level.Name, LevelFingerprint.Of(level.Map, 4),
                level.Map.Rows.ToString(), level.Map.Columns.ToString(), "4", "37"),
            (Text(body, "player"), Text(body, "level"), Text(body, "code"), Text(body, "name"), Text(body, "layout"), Text(body, "rows"),
                Text(body, "columns"), Text(body, "par"), Text(body, "score")));
    }

    [Theory]
    [InlineData(ReportReason.Name, "name")]
    [InlineData(ReportReason.Picture, "picture")]
    [InlineData(ReportReason.Other, "other")]
    public void Report_sends_the_level_and_reason(ReportReason reason, string word)
    {
        var level = Fixture.Create<Guid>();

        var body = Arguments(LibraryCalls.Report(level, reason), Fixture.Create<Guid>());

        Assert.Equal((level.ToString("D"), word), (Text(body, "level"), Text(body, "reason")));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Like_sends_the_level_and_whether_it_is_liked(bool liked)
    {
        var level = Fixture.Create<Guid>();

        var body = Arguments(LibraryCalls.Like(level, liked), Fixture.Create<Guid>());

        Assert.Equal((level.ToString("D"), liked), (Text(body, "level"), body.GetProperty("liked").GetBoolean()));
    }

    [Theory]
    [MemberData(nameof(LevelCalls))]
    public void Level_call_sends_the_player_and_level(string name)
    {
        var level = Fixture.Create<Guid>();
        var player = Fixture.Create<Guid>();
        var body = name switch
        {
            "remove" => Arguments(LibraryCalls.Remove(level), player),
            "finish" => Arguments(LibraryCalls.Finish(level), player),
            _ => Arguments(LibraryCalls.Block(level), player),
        };

        Assert.Equal((player.ToString("D"), level.ToString("D")), (Text(body, "player"), Text(body, "level")));
    }

    public static TheoryData<string> LevelCalls => ["remove", "finish", "block"];

    [Fact]
    public void Unblock_sends_only_the_player()
    {
        var player = Fixture.Create<Guid>();

        var body = Arguments(LibraryCalls.Unblock(), player);

        Assert.Equal([("player", player.ToString("D"))], body.EnumerateObject().Select(property => (property.Name, property.Value.ToString())));
    }

    private static string Row(Guid id, string code = "\"MC1-abc\"", string name = "\"Ring\"", string layout = "\"0123456789abcdef\"", string removed = "false") =>
        $$"""{"id":"{{id:D}}","code":{{code}},"name":{{name}},"layout":{{layout}},"rows":5,"columns":6,"par":7,"score":30,"finished":12,"likes":3,"mine":true,"hidden":false,"removed":{{removed}}}""";

    [Fact]
    public void Page_reads_its_rows_and_blocked_count()
    {
        var id = Fixture.Create<Guid>();

        var page = Read(LibraryCalls.Mine(), $$"""{"levels":[{{Row(id)}}],"blocked":2}""")!;

        Assert.Equal((new LibraryRow(id, "MC1-abc", "Ring", "0123456789abcdef", 5, 6, 7, 30, 12, 3, true, false, false), 2), (page.Rows.Single(), page.Blocked));
    }

    [Fact]
    public void Page_reads_a_tombstone_with_no_code_name_or_layout()
    {
        var id = Fixture.Create<Guid>();

        var row = Read(LibraryCalls.Mine(), $$"""{"levels":[{{Row(id, "null", "null", "null", "true")}}],"blocked":0}""")!.Rows.Single();

        Assert.Equal(((string?)null, (string?)null, (string?)null, true), (row.Code, row.Name, row.Layout, row.Removed));
    }

    public static TheoryData<string> BadPages()
    {
        var id = Fixture.Create<Guid>();
        return
        [
            """{"blocked":0}""",
            """{"levels":[],"blocked":-1}""",
            """{"levels":{},"blocked":0}""",
            """{"levels":[],"blocked":"0"}""",
            $$"""{"levels":[{{Row(id).Replace($"\"{id:D}\"", "\"not-a-guid\"")}}],"blocked":0}""",
            $$"""{"levels":[{{Row(id).Replace("\"likes\":3", "\"likes\":-3")}}],"blocked":0}""",
            $$"""{"levels":[{{Row(id).Replace("\"mine\":true", "\"mine\":1")}}],"blocked":0}""",
            $$"""{"levels":[{{Row(id).Replace("\"code\":\"MC1-abc\",", "")}}],"blocked":0}""",
            $$"""{"levels":[{{Row(id, code: "5")}}],"blocked":0}""",
            """{"levels":[1],"blocked":0}""",
        ];
    }

    [Theory]
    [MemberData(nameof(BadPages))]
    public void Page_out_of_shape_reads_as_null(string body)
    {
        Assert.Null(Read(LibraryCalls.Mine(), body));
    }

    [Theory]
    [InlineData("published", PublishOutcome.Published)]
    [InlineData("taken", PublishOutcome.Taken)]
    [InlineData("copy", PublishOutcome.Copy)]
    [InlineData("campaign", PublishOutcome.Campaign)]
    [InlineData("removed", PublishOutcome.Removed)]
    [InlineData("stopped", PublishOutcome.Stopped)]
    [InlineData("full", PublishOutcome.Full)]
    [InlineData("deleted", PublishOutcome.Deleted)]
    public void Publish_reads_its_outcome(string word, PublishOutcome outcome)
    {
        Assert.Equal(new PublishReply(outcome), Read(LibraryCalls.Publish(Level(), 4, 30), $$"""{"outcome":"{{word}}"}"""));
    }

    [Theory]
    [InlineData("""{"outcome":"maybe"}""")]
    [InlineData("""{"outcome":1}""")]
    [InlineData("""{}""")]
    public void Publish_out_of_shape_reads_as_null(string body)
    {
        Assert.Null(Read(LibraryCalls.Publish(Level(), 4, 30), body));
    }

    [Fact]
    public void Counts_read_an_unlisted_level()
    {
        Assert.Equal(new LevelCounts(false, false, 0, 0, false), Read(LibraryCalls.Finish(Fixture.Create<Guid>()), """{"listed":false}"""));
    }

    [Theory]
    [AutoData]
    public void Counts_read_a_listed_level(bool mine, ushort finished, ushort likes, bool liked)
    {
        var body = $$"""{"listed":true,"mine":{{(mine ? "true" : "false")}},"finished":{{finished}},"likes":{{likes}},"liked":{{(liked ? "true" : "false")}}}""";

        Assert.Equal(new LevelCounts(true, mine, finished, likes, liked), Read(LibraryCalls.Like(Fixture.Create<Guid>(), true), body));
    }

    [Theory]
    [InlineData("""{"listed":true}""")]
    [InlineData("""{"listed":true,"mine":false,"finished":-1,"likes":0,"liked":false}""")]
    [InlineData("""{"listed":"yes"}""")]
    public void Counts_out_of_shape_read_as_null(string body)
    {
        Assert.Null(Read(LibraryCalls.Finish(Fixture.Create<Guid>()), body));
    }

    [Fact]
    public void Report_reads_whether_it_was_kept()
    {
        Assert.Equal(new ReportReply(true), Read(LibraryCalls.Report(Fixture.Create<Guid>(), ReportReason.Other), """{"reported":true}"""));
    }

    [Fact]
    public void Block_reads_the_blocked_count()
    {
        Assert.Equal(new BlockReply(4), Read(LibraryCalls.Block(Fixture.Create<Guid>()), """{"blocked":4}"""));
    }

    [Fact]
    public void Remove_reads_whether_anything_went()
    {
        Assert.Equal(new RemoveReply(false), Read(LibraryCalls.Remove(Fixture.Create<Guid>()), """{"removed":false}"""));
    }

    [Fact]
    public void Publish_apply_records_published_and_shared()
    {
        var level = Level();
        var save = new SaveGame();

        var changed = LibraryCalls.Publish(level, 4, 30).Apply!(save, Fixture.Create<Guid>(), new PublishReply(PublishOutcome.Published));

        Assert.Equal((true, level.Edited, true), (changed, save.Published[level.Id], save.SharedLevels.Contains(level.Id)));
    }

    [Theory]
    [InlineData(PublishOutcome.Taken)]
    [InlineData(PublishOutcome.Copy)]
    [InlineData(PublishOutcome.Campaign)]
    [InlineData(PublishOutcome.Stopped)]
    [InlineData(PublishOutcome.Full)]
    [InlineData(PublishOutcome.Deleted)]
    public void Publish_apply_changes_nothing_for_a_refusal(PublishOutcome outcome)
    {
        var level = Level();
        var save = new SaveGame();
        save.RecordPublished(level.Id, DateTimeOffset.UnixEpoch);

        var changed = LibraryCalls.Publish(level, 4, 30).Apply!(save, Fixture.Create<Guid>(), new PublishReply(outcome));

        Assert.Equal((false, DateTimeOffset.UnixEpoch, false), (changed, save.Published[level.Id], save.SharedLevels.Contains(level.Id)));
    }

    // The developer removed it: the library no longer holds it, so the save stops saying it does
    [Fact]
    public void Publish_refused_as_removed_drops_published()
    {
        var level = Level();
        var save = new SaveGame();
        save.RecordPublished(level.Id, DateTimeOffset.UnixEpoch);

        LibraryCalls.Publish(level, 4, 30).Apply!(save, Fixture.Create<Guid>(), new PublishReply(PublishOutcome.Removed));

        Assert.False(save.Published.ContainsKey(level.Id));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Remove_apply_drops_published(bool removed)
    {
        var level = Fixture.Create<Guid>();
        var save = new SaveGame();
        save.RecordPublished(level, DateTimeOffset.UnixEpoch);

        LibraryCalls.Remove(level).Apply!(save, Fixture.Create<Guid>(), new RemoveReply(removed));

        Assert.False(save.Published.ContainsKey(level));
    }
}
