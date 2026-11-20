using System.Text.Json;
using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class SaveGameJsonTests
{
    // Level 4 is the section 7 example map with par 3, whose fingerprint LevelFingerprintTests pins.
    // Each invalid case below breaks one thing in it.
    internal const string Valid =
        """{"version":1,"settings":{"appearance":"light"},"levels":{"4":{"bestMoves":7,"layout":"c28a875eb682c187"}}}""";

    private const string Record = """{"bestMoves":7,"layout":"c28a875eb682c187"}""";

    private const string SharedId = "0f8e2c1a-4b6d-4c1e-9a3f-2d7b5e8c9a10";

    // Version 2 adds the levels shared from this device
    internal static string Valid2 => Valid.Replace("\"version\":1", "\"version\":2")[..^1] + $",\"sharedLevels\":[\"{SharedId}\"]}}";

    private const string Choice =
        """{"difficulty":4,"side":12,"walls":true,"traps":false,"teleporters":true,"irregular":false}""";

    private const string PlaytesterId = "7d3a1c5e-2b4f-4a6e-8c9d-0e1f2a3b4c5d";

    // Version 3 adds the play-data switch, the last infinite choice and the playtester ID
    internal static string Valid3 =>
        Valid2.Replace("\"version\":2", "\"version\":3").Replace("{\"appearance\":\"light\"}", "{\"appearance\":\"light\",\"recordPlayData\":true}")[..^1]
        + $",\"infinite\":{Choice},\"playtester\":\"{PlaytesterId}\"}}";

    private const string PlayDataYes = """{"answer":"yes","wording":1}""";

    private const string Consents4 = $$"""{"playData":{{PlayDataYes}},"leaderboards":null}""";

    // Version 4 replaces the play-data switch with the consents, and the playtester ID with the player ID
    internal static string Valid4 =>
        Valid3.Replace("\"version\":3", "\"version\":4").Replace(",\"recordPlayData\":true", "")
            .Replace($",\"playtester\":\"{PlaytesterId}\"", "")[..^1]
        + $",\"consents\":{Consents4},\"player\":\"{PlaytesterId}\"}}";

    private const string ServerSection = $$"""{"session":"tok-abc","pendingDeletions":["{{SharedId}}"],"recordsSent":120}""";

    // Version 5 adds the server section: the session, the IDs still to delete on the server, and how far the records are sent
    internal static string Valid5 => Valid4.Replace("\"version\":4", "\"version\":5")[..^1] + ",\"server\":" + ServerSection + "}";

    private static (Consent? PlayData, Consent? Leaderboards, Guid? Player) Answers(SaveGame save) =>
        (save.ConsentTo(ConsentKind.PlayData), save.ConsentTo(ConsentKind.Leaderboards), save.Player);

    [Fact]
    public void Version_2_save_reads_as_never_asked_with_no_choice_or_player()
    {
        var save = SaveGame.Parse(Valid2);

        Assert.Equal(((Consent?)null, (Consent?)null, (Guid?)null, (InfiniteChoice?)null), (save.ConsentTo(ConsentKind.PlayData), save.ConsentTo(ConsentKind.Leaderboards), save.Player, save.Infinite));
    }

    // Chunk 22's switch said records stay on the phone: wording 0, which no build asks with now, so the player is asked again
    [Fact]
    public void Version_3_save_with_recording_on_reads_as_yes_to_wording_zero()
    {
        var save = SaveGame.Parse(Valid3);

        Assert.Equal((new Consent(true, 0), (Consent?)null, (Guid?)Guid.Parse(PlaytesterId)), Answers(save));
    }

    [Fact]
    public void Version_3_save_with_recording_off_reads_as_never_asked()
    {
        var save = SaveGame.Parse(Valid3.Replace("\"recordPlayData\":true", "\"recordPlayData\":false"));

        Assert.Equal(((Consent?)null, (Consent?)null, (Guid?)Guid.Parse(PlaytesterId)), Answers(save));
    }

    [Fact]
    public void Version_3_save_with_recording_on_and_no_playtester_reads_as_yes_with_no_player()
    {
        var save = SaveGame.Parse(Valid3.Replace($"\"{PlaytesterId}\"", "null"));

        Assert.Equal((new Consent(true, 0), (Consent?)null, (Guid?)null), Answers(save));
    }

    [Fact]
    public void Version_3_save_keeps_its_infinite_choice()
    {
        Assert.Equal(new InfiniteChoice(4, 12, true, false, true, false), SaveGame.Parse(Valid3).Infinite);
    }

    [Fact]
    public void Version_4_save_reads_its_new_keys()
    {
        var save = SaveGame.Parse(Valid4);

        Assert.Equal((new Consent(true, 1), (Consent?)null, (Guid?)Guid.Parse(PlaytesterId)), Answers(save));
    }

    [Theory]
    [AutoData]
    public void Version_4_save_round_trips_its_new_keys(bool playData, int playDataWording, bool leaderboards, int leaderboardsWording, Guid player)
    {
        var save = new SaveGame { Player = player };
        save.SetConsent(ConsentKind.PlayData, new Consent(playData, Math.Abs(playDataWording % 100)));
        save.SetConsent(ConsentKind.Leaderboards, new Consent(leaderboards, Math.Abs(leaderboardsWording % 100)));

        var read = SaveGame.Parse(save.ToJson());

        Assert.Equal(Answers(save), Answers(read));
    }

    [Theory]
    [AutoData]
    public void Infinite_choice_round_trips(int difficulty, int side, bool walls, bool traps, bool teleporters, bool irregular)
    {
        var choice = new InfiniteChoice(Positive(difficulty) % 10 + 1, Positive(side) + 1, walls, traps, teleporters, irregular);

        Assert.Equal(choice, SaveGame.Parse(new SaveGame { Infinite = choice }.ToJson()).Infinite);
    }

    [Fact]
    public void New_save_round_trips_never_asked_with_no_choice_or_player()
    {
        var read = SaveGame.Parse(new SaveGame().ToJson());

        Assert.Equal(((Consent?)null, (Consent?)null, (Guid?)null, (InfiniteChoice?)null), (read.ConsentTo(ConsentKind.PlayData), read.ConsentTo(ConsentKind.Leaderboards), read.Player, read.Infinite));
    }

    private static (string? Session, string Deletions, long Sent) Server(SaveGame save) =>
        (save.SessionToken, string.Join(",", save.PendingDeletions.Order()), save.RecordsSent);

    [Fact]
    public void Version_5_save_reads_its_server_section()
    {
        var save = SaveGame.Parse(Valid5);

        Assert.Equal(("tok-abc", SharedId, 120L), Server(save));
    }

    [Theory]
    [AutoData]
    public void Server_section_round_trips(string token, Guid a, Guid b, uint sent)
    {
        var save = new SaveGame { SessionToken = token, RecordsSent = sent };
        save.AddPendingDeletion(a);
        save.AddPendingDeletion(b);

        Assert.Equal(Server(save), Server(SaveGame.Parse(save.ToJson())));
    }

    [Fact]
    public void Version_4_save_reads_as_no_session_no_deletions_and_nothing_sent()
    {
        Assert.Equal(((string?)null, "", 0L), Server(SaveGame.Parse(Valid4)));
    }

    [Theory]
    [AutoData]
    public void Pending_deletions_are_written_in_ascending_order(Guid a, Guid b, Guid c)
    {
        var save = new SaveGame();
        save.AddPendingDeletion(a);
        save.AddPendingDeletion(b);
        save.AddPendingDeletion(c);

        var written = JsonDocument.Parse(save.ToJson()).RootElement.GetProperty("server").GetProperty("pendingDeletions")
            .EnumerateArray().Select(id => id.GetString()!).ToArray();
        Assert.Equal(written.Order(StringComparer.Ordinal), written);
    }

    [Fact]
    public void Written_save_is_version_5_without_the_version_3_keys()
    {
        var root = JsonDocument.Parse(new SaveGame().ToJson()).RootElement;

        Assert.Equal(
            (5, false, false, JsonValueKind.Null, JsonValueKind.Null, JsonValueKind.Null),
            (root.GetProperty("version").GetInt32(),
                root.TryGetProperty("playtester", out _),
                root.GetProperty("settings").TryGetProperty("recordPlayData", out _),
                root.GetProperty("consents").GetProperty("playData").ValueKind,
                root.GetProperty("consents").GetProperty("leaderboards").ValueKind,
                root.GetProperty("player").ValueKind));
    }

    [Fact]
    public void Version_1_save_has_no_shared_levels()
    {
        Assert.Empty(SaveGame.Parse(Valid).SharedLevels);
    }

    [Fact]
    public void Version_2_save_reads_its_shared_levels()
    {
        Assert.Equal([Guid.Parse(SharedId)], SaveGame.Parse(Valid2).SharedLevels);
    }

    [Theory]
    [AutoData]
    public void Shared_levels_round_trip_in_ascending_order(Guid a, Guid b, Guid c)
    {
        var save = new SaveGame();
        foreach (var id in new[] { a, b, c })
        {
            save.RecordShared(id);
        }

        var text = save.ToJson();

        var expected = new[] { a, b, c }.Select(id => id.ToString("D")).Order(StringComparer.Ordinal).ToArray();
        var written = JsonDocument.Parse(text).RootElement.GetProperty("sharedLevels").EnumerateArray().Select(id => id.GetString()).ToArray();
        Assert.Equal(expected, written);
        Assert.Equal(save.SharedLevels.Order(), SaveGame.Parse(text).SharedLevels.Order());
    }

    [Theory]
    [AutoData]
    public void Recording_a_shared_level_twice_returns_false(Guid id)
    {
        var save = new SaveGame();
        save.RecordShared(id);

        Assert.False(save.RecordShared(id));
    }

    [Theory]
    [AutoData]
    public void Recording_a_new_shared_level_returns_true(Guid id)
    {
        Assert.True(new SaveGame().RecordShared(id));
    }

    private static readonly StarsConfig Shipped = new()
    {
        TwoStarMinExtraMoves = 2,
        TwoStarExtraParPercent = 50,
        OneStarMinExtraMoves = 4,
        OneStarExtraParPercent = 100,
    };

    private static int Positive(int value) => Math.Abs(value % 1000) + 1;

    private static Level LevelWith(int id) => new(id, $"Level {id}", 1, TestMaps.Example(), 3);

    [Fact]
    public void Parse_reads_a_hand_written_save()
    {
        var save = SaveGame.Parse(Valid);

        Assert.Equal((Appearance.Light, (int?)7), (save.Appearance, save.BestMoves(LevelWith(4))));
    }

    [Theory]
    [AutoData]
    public void Round_trip_keeps_bests_and_appearance(Appearance appearance, int a, int b, int c)
    {
        var levels = new[] { LevelWith(Positive(a)), LevelWith(Positive(a) + Positive(b)), LevelWith(Positive(a) + Positive(b) + Positive(c)) };
        var save = new SaveGame { Appearance = appearance };
        for (var i = 0; i < levels.Length; i++)
        {
            save.RecordFinish(levels[i], 3 + i, Shipped);
        }

        var read = SaveGame.Parse(save.ToJson());

        Assert.Equal(appearance, read.Appearance);
        Assert.Equal(new int?[] { 3, 4, 5 }, levels.Select(level => read.BestMoves(level)));
    }

    [Fact]
    public void Parse_counts_the_levels_it_read()
    {
        Assert.Equal(1, SaveGame.Parse(Valid).LevelCount);
    }

    [Fact]
    public void Round_trip_keeps_a_matching_record()
    {
        var read = SaveGame.Parse(SaveGame.Parse(Valid).ToJson());

        Assert.Equal((int?)7, read.BestMoves(LevelWith(4)));
    }

    [Fact]
    public void Written_save_lists_levels_in_id_order()
    {
        var save = new SaveGame();
        foreach (var id in new[] { 9, 2, 5 })
        {
            save.RecordFinish(LevelWith(id), 4, Shipped);
        }

        var text = save.ToJson();

        Assert.True(text.IndexOf("\"2\"", StringComparison.Ordinal) < text.IndexOf("\"5\"", StringComparison.Ordinal));
        Assert.True(text.IndexOf("\"5\"", StringComparison.Ordinal) < text.IndexOf("\"9\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Written_save_names_its_version_and_appearance()
    {
        var text = new SaveGame { Appearance = Appearance.Dark }.ToJson();

        Assert.Contains($"\"version\": {SaveGame.CurrentVersion}", text, StringComparison.Ordinal);
        Assert.Contains("\"appearance\": \"dark\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Best_below_par_counts_as_no_best()
    {
        var save = SaveGame.Parse(Valid.Replace("\"bestMoves\":7", "\"bestMoves\":2"));

        Assert.Equal(((int?)null, (int?)null), (save.BestMoves(LevelWith(4)), save.Stars(LevelWith(4), Shipped)));
    }

    public static TheoryData<string> InvalidTexts => new()
    {
        Valid.Replace("\"version\":1,", ""),
        Valid.Replace("\"version\":1", "\"version\":\"1\""),
        Valid.Replace("\"version\":1", "\"version\":1.5"),
        Valid.Replace("\"version\":1", "\"version\":0"),
        Valid.Replace("\"version\":1", "\"version\":-1"),
        Valid.Replace("\"settings\":{\"appearance\":\"light\"},", ""),
        Valid.Replace("{\"appearance\":\"light\"}", "null"),
        Valid.Replace("{\"appearance\":\"light\"}", "{}"),
        Valid.Replace("\"appearance\":\"light\"", "\"appearance\":\"light\",\"extra\":1"),
        Valid.Replace("\"light\"", "null"),
        Valid.Replace("\"light\"", "\"purple\""),
        Valid.Replace("\"light\"", "1"),
        Valid.Replace("\"light\"", "\"2\""),
        Valid.Replace("\"light\"", "\"5\""),
        Valid.Replace("\"light\"", "\"light, dark\""),
        Valid.Replace(",\"levels\":{\"4\":" + Record + "}", ""),
        Valid.Replace("{\"4\":" + Record + "}", "null"),
        Valid.Replace("\"4\":", "\"abc\":"),
        Valid.Replace("\"4\":", "\"0\":"),
        Valid.Replace("\"4\":", "\"-2\":"),
        Valid.Replace(Record, "null"),
        Valid.Replace("\"bestMoves\":7,", ""),
        Valid.Replace("\"bestMoves\":7", "\"bestMoves\":0"),
        Valid.Replace("\"bestMoves\":7", "\"bestMoves\":\"7\""),
        Valid.Replace(",\"layout\":\"c28a875eb682c187\"", ""),
        Valid.Replace("\"c28a875eb682c187\"", "null"),
        Valid.Replace("c28a875eb682c187", "C28A875EB682C187"),
        Valid.Replace("c28a875eb682c187", "c28a875eb682c18"),
        Valid.Replace("c28a875eb682c187", "c28a875eb682c1870"),
        Valid.Replace("c28a875eb682c187", "g28a875eb682c187"),
        Valid.Replace("\"bestMoves\":7", "\"bestMoves\":7,\"extra\":1"),
        Valid[..^1] + ",\"extra\":1}",
        "null",
        "[]",
        Valid.Replace("\"version\":1", "\"version\":1,\"version\":1"),
        Valid.Replace("\"appearance\":\"light\"", "\"appearance\":\"dark\",\"appearance\":\"light\""),
        Valid.Replace("\"4\":" + Record, "\"4\":" + Record + ",\"4\":" + Record),
        Valid.Replace("\"4\":" + Record, "\"04\":" + Record + ",\"4\":" + Record),
        Valid.Replace("\"bestMoves\":7", "\"bestMoves\":9,\"bestMoves\":7"),
        Valid2.Replace($",\"sharedLevels\":[\"{SharedId}\"]", ""),
        Valid2.Replace($"[\"{SharedId}\"]", "null"),
        Valid2.Replace($"[\"{SharedId}\"]", "[null]"),
        Valid2.Replace(SharedId, "not-a-guid"),
        Valid2.Replace(SharedId, SharedId.ToUpperInvariant()),
        Valid2.Replace(SharedId, "00000000-0000-0000-0000-000000000000"),
        Valid2.Replace($"\"{SharedId}\"", $"\"{SharedId}\",\"{SharedId}\""),
        Valid[..^1] + ",\"sharedLevels\":[]}",
        Valid3.Replace(",\"recordPlayData\":true", ""),
        Valid3.Replace(",\"infinite\":" + Choice, ""),
        Valid3.Replace($",\"playtester\":\"{PlaytesterId}\"", ""),
        Valid2.Replace("{\"appearance\":\"light\"}", "{\"appearance\":\"light\",\"recordPlayData\":false}"),
        Valid2[..^1] + ",\"infinite\":null}",
        Valid2[..^1] + ",\"playtester\":null}",
        Valid3.Replace("\"recordPlayData\":true", "\"recordPlayData\":\"true\""),
        Valid3.Replace("\"recordPlayData\":true", "\"recordPlayData\":null"),
        Valid3.Replace("\"difficulty\":4", "\"difficulty\":0"),
        Valid3.Replace("\"difficulty\":4", "\"difficulty\":11"),
        Valid3.Replace("\"side\":12", "\"side\":1"),
        Valid3.Replace("\"irregular\":false", "\"irregular\":false,\"extra\":1"),
        Valid3.Replace(",\"irregular\":false", ""),
        Valid3.Replace(PlaytesterId, PlaytesterId.ToUpperInvariant()),
        Valid3.Replace(PlaytesterId, "00000000-0000-0000-0000-000000000000"),
        Valid3.Replace(PlaytesterId, "x"),
        Valid3[..^1] + ",\"consents\":{\"playData\":null,\"leaderboards\":null}}",
        Valid3[..^1] + ",\"player\":null}",
        Valid4.Replace(",\"consents\":" + Consents4, ""),
        Valid4.Replace($",\"player\":\"{PlaytesterId}\"", ""),
        Valid4.Replace(Consents4, "null"),
        Valid4.Replace(Consents4, "[]"),
        Valid4.Replace(",\"leaderboards\":null", ""),
        Valid4.Replace($"\"playData\":{PlayDataYes},", ""),
        Valid4.Replace("\"leaderboards\":null", "\"leaderboards\":null,\"ads\":null"),
        Valid4.Replace("\"leaderboards\":null", "\"leaderboards\":null,\"leaderboards\":null"),
        Valid4.Replace("\"answer\":\"yes\"", "\"answer\":\"maybe\""),
        Valid4.Replace("\"answer\":\"yes\"", "\"answer\":\"Yes\""),
        Valid4.Replace("\"answer\":\"yes\"", "\"answer\":true"),
        Valid4.Replace("\"answer\":\"yes\"", "\"answer\":null"),
        Valid4.Replace("\"answer\":\"yes\",", ""),
        Valid4.Replace("\"wording\":1", "\"wording\":-1"),
        Valid4.Replace("\"wording\":1", "\"wording\":\"1\""),
        Valid4.Replace("\"wording\":1", "\"wording\":1.5"),
        Valid4.Replace("\"wording\":1", "\"wording\":null"),
        Valid4.Replace(",\"wording\":1", ""),
        Valid4.Replace("\"wording\":1", "\"wording\":1,\"at\":2"),
        Valid4.Replace(PlaytesterId, PlaytesterId.ToUpperInvariant()),
        Valid4.Replace(PlaytesterId, "00000000-0000-0000-0000-000000000000"),
        Valid4.Replace(PlaytesterId, "x"),
        Valid4.Replace("{\"appearance\":\"light\"}", "{\"appearance\":\"light\",\"recordPlayData\":true}"),
        Valid4[..^1] + ",\"playtester\":null}",
        Valid4[..^1] + ",\"server\":" + ServerSection + "}",
        Valid5.Replace(",\"server\":" + ServerSection, ""),
        Valid5.Replace(ServerSection, "null"),
        Valid5.Replace("\"session\":\"tok-abc\",", ""),
        Valid5.Replace("\"tok-abc\"", "\"\""),
        Valid5.Replace("\"tok-abc\"", "1"),
        Valid5.Replace($"[\"{SharedId}\"]", "null"),
        Valid5.Replace($"[\"{SharedId}\"]", "[null]"),
        Valid5.Replace($"[\"{SharedId}\"]", "[\"not-a-guid\"]"),
        Valid5.Replace($"[\"{SharedId}\"]", $"[\"{SharedId.ToUpperInvariant()}\"]"),
        Valid5.Replace($"[\"{SharedId}\"]", "[\"00000000-0000-0000-0000-000000000000\"]"),
        Valid5.Replace($"[\"{SharedId}\"]", $"[\"{SharedId}\",\"{SharedId}\"]"),
        Valid5.Replace(",\"pendingDeletions\":[\"" + SharedId + "\"]", ""),
        Valid5.Replace("\"recordsSent\":120", "\"recordsSent\":-1"),
        Valid5.Replace("\"recordsSent\":120", "\"recordsSent\":\"120\""),
        Valid5.Replace("\"recordsSent\":120", "\"recordsSent\":1.5"),
        Valid5.Replace(",\"recordsSent\":120", ""),
        Valid5.Replace("\"recordsSent\":120", "\"recordsSent\":120,\"extra\":1"),
    };

    [Theory]
    [MemberData(nameof(InvalidTexts))]
    public void Invalid_save_throws_JsonException(string text)
    {
        Assert.ThrowsAny<JsonException>(() => SaveGame.Parse(text));
    }

    // A case that broke the JSON itself would throw without testing the rule it was written for
    [Theory]
    [MemberData(nameof(InvalidTexts))]
    public void Every_invalid_case_is_still_JSON(string text)
    {
        Assert.Null(Xunit.Record.Exception(() => JsonDocument.Parse(text).Dispose()));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("")]
    public void Text_that_is_not_JSON_throws_JsonException(string text)
    {
        Assert.ThrowsAny<JsonException>(() => SaveGame.Parse(text));
    }

    [Fact]
    public void A_repeated_key_is_named_in_the_error()
    {
        var text = Valid.Replace("\"bestMoves\":7", "\"bestMoves\":9,\"bestMoves\":7");

        Assert.Contains("\"bestMoves\"", Assert.Throws<JsonException>(() => SaveGame.Parse(text)).Message, StringComparison.Ordinal);
    }

    // A dictionary keyed by number reads "04" as 4; alone it clashes with nothing
    [Fact]
    public void A_level_key_with_a_leading_zero_reads_as_its_number()
    {
        var save = SaveGame.Parse(Valid.Replace("\"4\":", "\"04\":"));

        Assert.Equal((int?)7, save.BestMoves(LevelWith(4)));
    }

    // No build writes these, so whatever did is newer: the file must be left alone, not set aside as broken
    [Theory]
    [InlineData("6.0", 6)]
    [InlineData("6e0", 6)]
    [InlineData("99999999999", int.MaxValue)]
    public void A_newer_version_in_an_odd_form_is_still_newer(string version, int expected)
    {
        var exception = Assert.Throws<NewerSaveException>(() => SaveGame.Parse(Valid.Replace("\"version\":1", $"\"version\":{version}")));

        Assert.Equal(expected, exception.Version);
    }

    // Bests for levels this build doesn't have, or whose layout has since changed, must outlive a write by this build
    [Fact]
    public void Round_trip_keeps_stale_and_unknown_records()
    {
        var save = SaveGame.Parse(Valid.Replace("c28a875eb682c187", "0000000000000000").Replace("\"4\":", "\"900\":"));
        save.RecordFinish(LevelWith(4), 5, Shipped);

        var written = save.ToJson();

        Assert.Equal(2, SaveGame.Parse(written).LevelCount);
        Assert.Contains("\"0000000000000000\"", written, StringComparison.Ordinal);
    }

    [Theory]
    [AutoData]
    public void Newer_version_throws_NewerSaveException_with_its_version(int version)
    {
        var newer = SaveGame.CurrentVersion + 1 + Math.Abs(version % 1000);

        var exception = Assert.Throws<NewerSaveException>(() => SaveGame.Parse(Valid.Replace("\"version\":1", $"\"version\":{newer}")));

        Assert.Equal(newer, exception.Version);
    }

    [Fact]
    public void Newer_version_wins_over_other_problems()
    {
        Assert.Throws<NewerSaveException>(() => SaveGame.Parse($$"""{"version":{{SaveGame.CurrentVersion + 1}},"somethingNew":true}"""));
    }

    [Fact]
    public void Parse_accepts_a_leading_byte_order_mark()
    {
        Assert.Equal(Appearance.Light, SaveGame.Parse((char)0xFEFF + Valid).Appearance);
    }
}
