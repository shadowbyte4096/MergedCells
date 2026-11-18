using System.Text.Json;

namespace MergedCells.Core.Tests;

public class PlayRecordFileTests
{
    // The section 7 example as an infinite record; each invalid case below breaks one thing in it
    private const string Valid =
        """{"version":1,"id":"0f8e2c1a-4b6d-4c1e-9a3f-2d7b5e8c9a10","player":"7d3a1c5e-2b4f-4a6e-8c9d-0e1f2a3b4c5d","started":"2026-11-18T10:22:31Z","build":"playtest-57","platform":"Android","mode":"infinite","level":null,"seenBefore":null,"infinite":{"difficulty":7,"side":4,"walls":true,"traps":false,"teleporters":false,"irregular":false},"regions":[[1,1,2,3],[4,5,2,3],[4,5,6,6],[7,7,7,8]],"tiles":null,"teleporters":null,"start":[0,0],"end":[3,3],"par":3,"metricsVersion":1,"nearOptimalExtraMoves":2,"bruteForceLookahead":3,"metrics":{"awayMoves":1,"retreat":2,"greedyGap":null,"decoys":3,"nearOptimalRoutes":40,"bruteForceExtraMoves":2},"score":63,"outcome":"won","moves":7,"undos":2,"restarts":1,"activeMs":45210,"actions":[{"at":1200,"do":"right"},{"at":3000,"do":"undo"},{"at":4000,"do":"restart"}]}""";

    private const string Choice = """{"difficulty":7,"side":4,"walls":true,"traps":false,"teleporters":false,"irregular":false}""";

    private static readonly string CampaignValid = Valid
        .Replace("\"mode\":\"infinite\",\"level\":null,\"seenBefore\":null,\"infinite\":" + Choice, "\"mode\":\"campaign\",\"level\":23,\"seenBefore\":true,\"infinite\":null");

    private static PlayRecord Pinned() =>
        new(
            new PlayRecordStart(
                Guid.Parse("0f8e2c1a-4b6d-4c1e-9a3f-2d7b5e8c9a10"),
                Guid.Parse("7d3a1c5e-2b4f-4a6e-8c9d-0e1f2a3b4c5d"),
                new DateTimeOffset(2026, 11, 18, 10, 22, 31, TimeSpan.Zero),
                "playtest-57",
                "Android",
                DifficultyMode.Infinite,
                null,
                null,
                new InfiniteChoice(7, 4, true, false, false, false),
                TestMaps.Example(),
                3,
                1,
                2,
                3,
                new DifficultyMetrics(3, 1, 2, null, 3, 40, 2),
                63),
            PlayOutcome.Won,
            7,
            2,
            1,
            45210,
            [new PlayAction(1200, PlayActionKind.Right), new PlayAction(3000, PlayActionKind.Undo), new PlayAction(4000, PlayActionKind.Restart)]);

    public static TheoryData<string> Cases => ["plain", "traps", "teleporters", "campaign", "null metrics"];

    private static PlayRecord Case(string name) => name switch
    {
        "plain" => PlayRecords.Record(PlayRecords.Infinite(TestMaps.Example())),
        "traps" => PlayRecords.Record(PlayRecords.Infinite(TestMaps.TrappedLane())),
        "teleporters" => PlayRecords.Record(PlayRecords.Infinite(TestMaps.CoveringTrapsBesideAPair())),
        "campaign" => PlayRecords.Record(PlayRecords.Campaign(TestMaps.Example())),
        _ => PlayRecords.Record(PlayRecords.Infinite(TestMaps.Example(), PlayRecords.Metrics(3, nullGreedy: true, nullBruteForce: true))),
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Record_round_trips(string name)
    {
        var line = PlayRecordFile.Write(Case(name));

        Assert.Equal(line, PlayRecordFile.Write(PlayRecordFile.Parse(line)));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Written_record_is_one_line(string name)
    {
        Assert.DoesNotContain('\n', PlayRecordFile.Write(Case(name)));
    }

    // Records are kept for the refit, so their layout must not drift: the exact text is the point
    [Fact]
    public void Written_record_is_pinned()
    {
        Assert.Equal(Valid, PlayRecordFile.Write(Pinned()));
    }

    [Fact]
    public void Pinned_line_reads_back()
    {
        Assert.Equal(Valid, PlayRecordFile.Write(PlayRecordFile.Parse(Valid)));
    }

    [Fact]
    public void Campaign_line_reads_its_level()
    {
        var start = PlayRecordFile.Parse(CampaignValid).Start;

        Assert.Equal((DifficultyMode.Campaign, (int?)23, (bool?)true, (InfiniteChoice?)null), (start.Mode, start.Level, start.SeenBefore, start.Infinite));
    }

    public static TheoryData<string> InvalidTexts => new()
    {
        Valid[..^1] + ",\"extra\":1}",
        Valid.Replace(",\"build\":\"playtest-57\"", ""),
        Valid.Replace(",\"tiles\":null", ""),
        Valid.Replace(",\"level\":null", ""),
        Valid.Replace("\"moves\":7", "\"moves\":7,\"moves\":7"),
        Valid.Replace("\"version\":1", "\"version\":\"1\""),
        Valid.Replace("0f8e2c1a-4b6d-4c1e-9a3f-2d7b5e8c9a10", "0F8E2C1A-4B6D-4C1E-9A3F-2D7B5E8C9A10"),
        Valid.Replace("0f8e2c1a-4b6d-4c1e-9a3f-2d7b5e8c9a10", "00000000-0000-0000-0000-000000000000"),
        Valid.Replace("7d3a1c5e-2b4f-4a6e-8c9d-0e1f2a3b4c5d", "x"),
        Valid.Replace("2026-11-18T10:22:31Z", "2026-11-18 10:22:31"),
        Valid.Replace("\"build\":\"playtest-57\"", "\"build\":null"),
        Valid.Replace("\"platform\":\"Android\"", "\"platform\":null"),
        Valid.Replace("\"mode\":\"infinite\"", "\"mode\":\"versus\""),
        Valid.Replace("\"mode\":\"infinite\"", "\"mode\":\"Infinite\""),
        Valid.Replace("\"outcome\":\"won\"", "\"outcome\":\"lost\""),
        Valid.Replace("\"do\":\"right\"", "\"do\":\"jump\""),
        Valid.Replace("\"moves\":7", "\"moves\":-1"),
        Valid.Replace("\"undos\":2", "\"undos\":-1"),
        Valid.Replace("\"restarts\":1", "\"restarts\":-1"),
        Valid.Replace("\"activeMs\":45210", "\"activeMs\":-1"),
        Valid.Replace("\"at\":1200", "\"at\":-1"),
        Valid.Replace("\"decoys\":3", "\"decoys\":-1"),
        Valid.Replace("\"nearOptimalRoutes\":40", "\"nearOptimalRoutes\":-1"),
        Valid.Replace("\"greedyGap\":null", "\"greedyGap\":-1"),
        Valid.Replace("\"score\":63", "\"score\":101"),
        Valid.Replace("\"score\":63", "\"score\":-1"),
        Valid.Replace("\"par\":3", "\"par\":0"),
        Valid.Replace("\"metricsVersion\":1", "\"metricsVersion\":0"),
        Valid.Replace("\"nearOptimalExtraMoves\":2", "\"nearOptimalExtraMoves\":0"),
        Valid.Replace("\"bruteForceLookahead\":3", "\"bruteForceLookahead\":0"),
        Valid.Replace("\"difficulty\":7", "\"difficulty\":11"),
        Valid.Replace("\"side\":4", "\"side\":1"),
        Valid.Replace("\"infinite\":" + Choice, "\"infinite\":null"),
        Valid.Replace("\"level\":null", "\"level\":5"),
        Valid.Replace("\"actions\":[", "\"actions\":[null,"),
        Valid.Replace("\"metrics\":{\"awayMoves\":1,\"retreat\":2,\"greedyGap\":null,\"decoys\":3,\"nearOptimalRoutes\":40,\"bruteForceExtraMoves\":2}", "\"metrics\":null"),
        CampaignValid.Replace("\"level\":23", "\"level\":null"),
        CampaignValid.Replace("\"level\":23", "\"level\":0"),
        CampaignValid.Replace("\"seenBefore\":true", "\"seenBefore\":null"),
        CampaignValid.Replace("\"infinite\":null", "\"infinite\":" + Choice),
    };

    [Theory]
    [MemberData(nameof(InvalidTexts))]
    public void Invalid_record_throws_JsonException(string line)
    {
        Assert.ThrowsAny<JsonException>(() => PlayRecordFile.Parse(line));
    }

    [Fact]
    public void Bad_map_throws_InvalidMapException()
    {
        Assert.Throws<InvalidMapException>(() => PlayRecordFile.Parse(Valid.Replace("[7,7,7,8]", "[7,7,7,7]")));
    }

    [Fact]
    public void Newer_version_throws_NewerPlayRecordException()
    {
        var line = Valid.Replace("\"version\":1", $"\"version\":{PlayRecordFile.CurrentVersion + 1}")[..^1] + ",\"somethingNew\":true}";

        Assert.Equal(PlayRecordFile.CurrentVersion + 1, Assert.Throws<NewerPlayRecordException>(() => PlayRecordFile.Parse(line)).Version);
    }
}
