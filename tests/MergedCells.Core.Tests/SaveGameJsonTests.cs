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

        Assert.Contains("\"version\": 1", text, StringComparison.Ordinal);
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
    [InlineData("2.0", 2)]
    [InlineData("2e0", 2)]
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
        var newer = 2 + Math.Abs(version % 1000);

        var exception = Assert.Throws<NewerSaveException>(() => SaveGame.Parse(Valid.Replace("\"version\":1", $"\"version\":{newer}")));

        Assert.Equal(newer, exception.Version);
    }

    [Fact]
    public void Newer_version_wins_over_other_problems()
    {
        Assert.Throws<NewerSaveException>(() => SaveGame.Parse("""{"version":2,"somethingNew":true}"""));
    }

    [Fact]
    public void Parse_accepts_a_leading_byte_order_mark()
    {
        Assert.Equal(Appearance.Light, SaveGame.Parse((char)0xFEFF + Valid).Appearance);
    }
}
