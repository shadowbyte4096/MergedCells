using AutoFixture;
using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class ShareCodeTests
{
    // Computed independently in PowerShell in the planning session: the ID's 16 bytes, 1794910951 s as e7 d5 f0 d7 06,
    // 0e and "Long Way Round", rows 02 and columns 03, 00 pairs, 00 for no tiles, then 68 08 (start 0 and end 5 in 3 bits
    // each, joins 1 0 0 0 0 1 0), and CRC efbc46fd least significant byte first
    internal const string PinnedCode = "MC1-D44sGkttTB6aPy17XoyaEOfV8NcGDkxvbmcgV2F5IFJvdW5kAgMAAGgI_Ua87w";

    private readonly Fixture fixture = new();

    internal static CustomLevel PinnedLevel() => new(
        Guid.Parse("0f8e2c1a-4b6d-4c1e-9a3f-2d7b5e8c9a10"),
        "Long Way Round",
        Map.Create(new[,] { { 1, 1, 2 }, { 3, 3, 4 } }, new(0, 0), new(1, 2)),
        new DateTimeOffset(2026, 11, 17, 10, 22, 31, TimeSpan.Zero));

    private static DateTimeOffset Seconds(DateTime when) =>
        new(when.Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, TimeSpan.Zero);

    // A bent block (A), a wall, a trap and one teleporter pair
    private static Map EveryTwist() => Map.Create(
        TestMaps.Grid("AABC", "DABC", "DEEZ"),
        new(0, 0),
        new(2, 3),
        new Tile[,] { { 0, 0, 0, 0 }, { Tile.Wall, 0, 0, 0 }, { 0, Tile.Trap, 0, 0 } },
        [TestMaps.Pair(0, 2, 1, 3)]);

    // The editor's largest board at its most expensive: every square single, a tile grid, and all 45 pairs
    internal static Map Worst30()
    {
        var regions = new int[30, 30];
        var tiles = new Tile[30, 30];
        var pairCells = Enumerable.Range(0, 90).Select(k => new Cell(1 + k / 30 * 2, k % 30)).ToHashSet();
        for (var row = 0; row < 30; row++)
        {
            for (var column = 0; column < 30; column++)
            {
                regions[row, column] = row * 30 + column + 1;
                var cell = new Cell(row, column);
                if (cell != new Cell(0, 0) && cell != new Cell(29, 29) && !pairCells.Contains(cell))
                {
                    tiles[row, column] = (row + column) % 2 == 0 ? Tile.Wall : Tile.Trap;
                }
            }
        }
        var cells = pairCells.OrderBy(cell => cell.Row).ThenBy(cell => cell.Column).ToArray();
        var pairs = Enumerable.Range(0, 45).Select(i => new TeleporterPair(cells[2 * i], cells[2 * i + 1])).ToArray();
        return Map.Create(regions, new(0, 0), new(29, 29), tiles, pairs);
    }

    // A 30×30 of single squares with this many teleporter pairs; maps allow any number, the editor at most 45
    private static Map WithPairs(int count)
    {
        var regions = new int[30, 30];
        for (var row = 0; row < 30; row++)
        {
            for (var column = 0; column < 30; column++)
            {
                regions[row, column] = row * 30 + column + 1;
            }
        }
        var cells = Enumerable.Range(0, 2 * count).Select(k => new Cell(1 + k / 30, k % 30)).ToArray();
        var pairs = Enumerable.Range(0, count).Select(i => new TeleporterPair(cells[2 * i], cells[2 * i + 1])).ToArray();
        return Map.Create(regions, new(0, 0), new(29, 29), teleporters: pairs);
    }

    public static TheoryData<string, bool> Writable => new()
    {
        { "45 pairs", true },
        { "46 pairs", false },
        { "edited before 1970", false },
    };

    private static CustomLevel WritableCase(string kind) => kind switch
    {
        "45 pairs" => PinnedLevel() with { Map = WithPairs(45) },
        "46 pairs" => PinnedLevel() with { Map = WithPairs(46) },
        _ => PinnedLevel() with { Edited = DateTimeOffset.UnixEpoch.AddSeconds(-1) },
    };

    // Share is offered only for a level a code can hold; a hand-edited file can hold more than the editor makes
    [Theory]
    [MemberData(nameof(Writable))]
    public void Can_write_only_what_a_code_holds(string kind, bool expected)
    {
        Assert.Equal(expected, ShareCode.CanWrite(WritableCase(kind)));
    }

    [Fact]
    public void Writing_more_than_45_pairs_throws()
    {
        Assert.Throws<ArgumentException>(() => ShareCode.Write(WritableCase("46 pairs")));
    }

    public static TheoryData<string> RoundTripCases => new() { "every twist", "one by five", "worst 30", "emoji name" };

    private static (string Name, Map Map) Case(string kind) => kind switch
    {
        "every twist" => ("Long Way Round", EveryTwist()),
        "one by five" => ("Slide", Map.Create(TestMaps.Grid("ABCDE"), new(0, 0), new(0, 4))),
        "worst 30" => ("Untitled", Worst30()),
        _ => ("Café ☕ 👨‍👩‍👧‍👦", EveryTwist()),
    };

    [Fact]
    public void Crc_of_the_check_string_is_cbf43926()
    {
        Assert.Equal(0xCBF43926u, Crc32.Of("123456789"u8));
    }

    [Fact]
    public void Code_is_pinned()
    {
        Assert.Equal(PinnedCode, ShareCode.Write(PinnedLevel()));
    }

    [Fact]
    public void Pinned_code_reads_back()
    {
        var expected = PinnedLevel();

        var read = ShareCode.Read(PinnedCode);

        Assert.Equal(ShareCodeProblem.None, read.Problem);
        Assert.Equal((expected.Id, expected.Name, expected.Edited, (DateTimeOffset?)null), (read.Level!.Id, read.Level.Name, read.Level.Edited, read.Level.Imported));
        Assert.True(RegionGrids.SameLevel(expected.Map, read.Level.Map));
    }

    [Theory]
    [MemberData(nameof(RoundTripCases))]
    public void Round_trip_keeps_the_level(string kind)
    {
        var (name, map) = Case(kind);
        var level = new CustomLevel(fixture.Create<Guid>(), name, map, Seconds(fixture.Create<DateTime>()));

        var read = ShareCode.Read(ShareCode.Write(level)).Level!;

        Assert.Equal((level.Id, level.Name, level.Edited), (read.Id, read.Name, read.Edited));
        Assert.True(RegionGrids.SameLevel(level.Map, read.Map));
    }

    // The reader's length bound must stay above every code Write can make: 255×255 single squares, a tile grid, 45 pairs
    // and a 512-byte name
    [Fact]
    public void Largest_code_the_format_allows_reads()
    {
        var side = 255;
        var regions = new int[side, side];
        var tiles = new Tile[side, side];
        for (var row = 0; row < side; row++)
        {
            for (var column = 0; column < side; column++)
            {
                regions[row, column] = row * side + column + 1;
                tiles[row, column] = row > 2 && row < side - 1 ? Tile.Wall : Tile.None;
            }
        }
        var pairs = Enumerable.Range(0, 45).Select(i => new TeleporterPair(new(1, 2 * i + 1), new(2, 2 * i + 1))).ToArray();
        var map = Map.Create(regions, new(0, 0), new(side - 1, side - 1), tiles, pairs);
        var level = new CustomLevel(fixture.Create<Guid>(), CustomLevelNamesTests.NameOfBytes(CustomLevelNames.MaxBytes), map, PinnedLevel().Edited);

        Assert.Equal(ShareCodeProblem.None, ShareCode.Read(ShareCode.Write(level)).Problem);
    }

    // Measured on the first run; a change here means the version 1 layout drifted
    [Fact]
    public void Worst_case_length_is_pinned()
    {
        var level = new CustomLevel(Guid.Parse("0f8e2c1a-4b6d-4c1e-9a3f-2d7b5e8c9a10"), "Untitled", Worst30(), PinnedLevel().Edited);

        Assert.Equal(799, ShareCode.Write(level).Length);
    }

    public static TheoryData<string> Messages => new()
    {
        "Try this one: " + PinnedCode + " thanks!",
        "Try this:\n" + PinnedCode + "\n😀",
        PinnedCode + ".",
        PinnedCode + "\r\n",
    };

    [Theory]
    [MemberData(nameof(Messages))]
    public void Code_inside_a_message_reads(string message)
    {
        Assert.Equal(PinnedLevel().Id, ShareCode.Read(message).Level?.Id);
    }

    // Only a token with a payload counts, so a bare "MC1-" earlier in the message doesn't hide the real code
    [Theory]
    [InlineData("MC1- ")]
    [InlineData("Codes start with MC1-…: ")]
    public void Empty_token_before_the_code_is_skipped(string before)
    {
        Assert.Equal(PinnedLevel().Id, ShareCode.Read(before + PinnedCode).Level?.Id);
    }

    [Theory]
    [AutoData]
    public void First_of_two_codes_counts(Guid other)
    {
        var second = ShareCode.Write(PinnedLevel() with { Id = other });

        Assert.Equal(PinnedLevel().Id, ShareCode.Read(PinnedCode + " " + second).Level?.Id);
    }

    public static TheoryData<string> NotCodes => new()
    {
        "",
        "hello",
        PinnedCode.Replace("MC1-", "mc1-"),
        "MC1-",
        "MCx-abc",
        "MC1 " + PinnedCode[4..],
    };

    [Theory]
    [MemberData(nameof(NotCodes))]
    public void Text_without_a_code_is_no_code(string text)
    {
        Assert.Equal(new ShareCodeRead(null, ShareCodeProblem.NoCode), ShareCode.Read(text));
    }

    // A later layout may use any characters after its version, so the version alone decides
    [Theory]
    [InlineData("MC2-AAAA")]
    [InlineData("MC10-!")]
    [InlineData("MC99999999999-AAAA")]
    public void Newer_version_is_newer_whatever_follows(string text)
    {
        Assert.Equal(new ShareCodeRead(null, ShareCodeProblem.NewerGame), ShareCode.Read(text));
    }
}
