using System.Text.Json;
using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class CustomLevelFileTests
{
    private const string Pinned =
        """
        {
          "version": 2,
          "id": "0f8e2c1a-4b6d-4c1e-9a3f-2d7b5e8c9a10",
          "name": "Long Way Round",
          "edited": "2026-11-17T10:22:31Z",
          "imported": null,
          "regions": [
            [1, 1, 2],
            [3, 3, 4]
          ],
          "tiles": null,
          "teleporters": null,
          "start": [0, 0],
          "end": [1, 2]
        }

        """;

    private static string PinnedText => Pinned.ReplaceLineEndings("\n");

    // A bent block (A), a wall, a trap and one teleporter pair
    private static Map EveryTwist() => Map.Create(
        TestMaps.Grid("AABC", "DABC", "DEEZ"),
        new(0, 0),
        new(2, 3),
        new Tile[,] { { 0, 0, 0, 0 }, { Tile.Wall, 0, 0, 0 }, { 0, Tile.Trap, 0, 0 } },
        [TestMaps.Pair(0, 2, 1, 3)]);

    [Theory]
    [AutoData]
    public void Round_trip_keeps_every_part(Guid id, string name, DateTime when)
    {
        var edited = new DateTimeOffset(when.Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, TimeSpan.Zero);
        var level = new CustomLevel(id, name, EveryTwist(), edited);

        var read = CustomLevelFile.Parse(CustomLevelFile.Write(level));

        Assert.Equal((id, name, edited), (read.Id, read.Name, read.Edited));
        Assert.Equal(LevelFingerprint.Of(level.Map, 1), LevelFingerprint.Of(read.Map, 1));
        Assert.Equal(level.Map.Teleporters, read.Map.Teleporters);
    }

    [Fact]
    public void Layout_is_pinned()
    {
        var level = new CustomLevel(
            Guid.Parse("0f8e2c1a-4b6d-4c1e-9a3f-2d7b5e8c9a10"),
            "Long Way Round",
            Map.Create(new[,] { { 1, 1, 2 }, { 3, 3, 4 } }, new(0, 0), new(1, 2)),
            new DateTimeOffset(2026, 11, 17, 10, 22, 31, TimeSpan.Zero));

        Assert.Equal(PinnedText, CustomLevelFile.Write(level));
    }

    [Theory]
    [AutoData]
    public void Imported_time_round_trips(DateTime when)
    {
        var imported = new DateTimeOffset(when.Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, TimeSpan.Zero);
        var text = PinnedText.Replace("\"imported\": null", $"\"imported\": \"{imported.UtcDateTime:yyyy-MM-dd'T'HH:mm:ss'Z'}\"");

        Assert.Equal(imported, CustomLevelFile.Parse(text).Imported);
    }

    // Chunk 21c's files, before anything could be imported
    private static string Version1Text => PinnedText.Replace("\"version\": 2,", "\"version\": 1,").Replace("  \"imported\": null,\n", "");

    [Fact]
    public void Version_1_file_reads_as_the_player_s_own()
    {
        Assert.False(CustomLevelFile.Parse(Version1Text).IsImported);
    }

    [Fact]
    public void Newer_version_throws_newer_even_with_unknown_keys()
    {
        Assert.Throws<NewerCustomLevelException>(() => CustomLevelFile.Parse("""{"version": 3, "whatever": 1}"""));
    }

    public static TheoryData<string> BrokenTexts => new()
    {
        PinnedText.Replace("\"version\": 2,", "\"version\": 2,\n  \"extra\": 1,"),
        PinnedText.Replace("\"name\": \"Long Way Round\",", "\"name\": \"Long Way Round\",\n  \"name\": \"Again\","),
        PinnedText.Replace("\"version\": 2,", "\"version\": 0,"),
        PinnedText.Replace("\"version\": 2,\n", ""),
        PinnedText.Replace("0f8e2c1a-4b6d-4c1e-9a3f-2d7b5e8c9a10", "not-a-guid"),
        PinnedText.Replace("0f8e2c1a-4b6d-4c1e-9a3f-2d7b5e8c9a10", "00000000-0000-0000-0000-000000000000"),
        PinnedText.Replace("0f8e2c1a-4b6d-4c1e-9a3f-2d7b5e8c9a10", "0F8E2C1A-4B6D-4C1E-9A3F-2D7B5E8C9A10"),
        PinnedText.Replace("\"Long Way Round\"", "\"  \""),
        PinnedText.Replace("\"Long Way Round\"", "\" Long Way Round\""),
        PinnedText.Replace("2026-11-17T10:22:31Z", "2026-11-17T10:22:31+00:00"),
        PinnedText.Replace("\"edited\":", "\"par\": 3,\n  \"edited\":"),
        PinnedText.Replace("\"edited\":", "\"chapter\": 1,\n  \"edited\":"),
        PinnedText.Replace("\"edited\":", "\"tutorial\": {\"lines\": [\"Hi\"]},\n  \"edited\":"),
        PinnedText.Replace("\"tiles\": null", "\"tiles\": [[0, 2, 0], [0, 0, 0]]"),
        PinnedText.Replace("Long Way Round", CustomLevelNamesTests.NameOfBytes(513)),
        PinnedText.Replace("Long Way Round", "Long\\u202EWay"),
        Version1Text.Replace("\"edited\":", "\"imported\": null,\n  \"edited\":"),
        PinnedText.Replace("  \"imported\": null,\n", ""),
        PinnedText.Replace("\"imported\": null", "\"imported\": \"2026-11-17T10:22:31+00:00\""),
        PinnedText.Replace("\"imported\": null", "\"imported\": 5"),
    };

    [Theory]
    [MemberData(nameof(BrokenTexts))]
    public void Broken_files_are_rejected(string text)
    {
        Assert.Throws<JsonException>(() => CustomLevelFile.Parse(text));
    }

    [Fact]
    public void Broken_map_throws_invalid_map()
    {
        Assert.Throws<InvalidMapException>(() => CustomLevelFile.Parse(PinnedText.Replace("[3, 3, 4]", "[3, 3, 2]")));
    }
}
