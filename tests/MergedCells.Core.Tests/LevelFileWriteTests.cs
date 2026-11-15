using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class LevelFileWriteTests
{
    private static string ShippedText(string file) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "levels", file));

    // The end is its own region at [2, 2]; the IDs are out of reading order on purpose
    private static Map Renumberable() => Map.Create(new[,] { { 7, 7, 3 }, { 9, 9, 9 }, { 4, 4, 5 } }, new(0, 0), new(2, 2));

    private static Level PlainLevel(string name, Tutorial? tutorial = null) => new(1, name, 1, TestMaps.Example(), 3, tutorial);

    [Theory]
    [MemberData(nameof(ShippedLevelsTests.LevelFiles), MemberType = typeof(ShippedLevelsTests))]
    public void Shipped_level_round_trips_byte_for_byte(string file)
    {
        var text = ShippedText(file);

        Assert.Equal(text, LevelFile.Write(LevelFile.Parse(text)));
    }

    [Theory(Timeout = 10_000)]
    [AutoData]
    public async Task Level_with_every_twist_parses_back_to_the_same_layout(ulong seed, string name, int id, int chapter)
    {
        var (written, read) = await Task.Run(
            () =>
            {
                var map = GeneratedMaps.Make(new MapRequest(10, 1, 100, Twists: GeneratedMaps.EveryTwist), seed);
                var level = new Level(id, name, chapter, map, Solver.Solve(map).Par!.Value);
                return (level, LevelFile.Parse(LevelFile.Write(level)));
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(
            (LevelFingerprint.Of(written), written.Id, written.Name, written.Chapter, written.Par),
            (LevelFingerprint.Of(read), read.Id, read.Name, read.Chapter, read.Par));
    }

    [Theory]
    [AutoData]
    public void Tutorial_with_keyboard_lines_and_a_swipe_hint_parses_back(string first, string second, string keyboard, string name)
    {
        var tutorial = new Tutorial([first, second], [keyboard], Direction.Left);

        var read = LevelFile.Parse(LevelFile.Write(PlainLevel(name, tutorial))).Tutorial!;

        Assert.Equal(tutorial.Lines, read.Lines);
        Assert.Equal(tutorial.KeyboardLines, read.KeyboardLines);
        Assert.Equal(tutorial.SwipeHint, read.SwipeHint);
    }

    [Theory]
    [AutoData]
    public void Keyboard_lines_equal_to_the_lines_are_left_out(string line, string name)
    {
        var text = LevelFile.Write(PlainLevel(name, new Tutorial([line], [line], null)));

        Assert.DoesNotContain("\"keyboardLines\"", text, StringComparison.Ordinal);
    }

    [Theory]
    [AutoData]
    public void Regions_are_renumbered_in_reading_order(string name)
    {
        var text = LevelFile.Write(new Level(1, name, 1, Renumberable(), 2));

        Assert.Contains("\n    [1, 1, 2],\n    [3, 3, 3],\n    [4, 4, 5]\n", text, StringComparison.Ordinal);
    }

    [Theory]
    [AutoData]
    public void Quotes_and_backslashes_round_trip_in_a_name(string before, string after)
    {
        var name = before + "\"\\" + after;

        Assert.Equal(name, LevelFile.Parse(LevelFile.Write(PlainLevel(name))).Name);
    }

    [Theory]
    [AutoData]
    public void Arrow_is_written_as_itself(string name)
    {
        var text = LevelFile.Write(PlainLevel(name, new Tutorial(["Press → to slide"], ["Press → to slide"], null)));

        Assert.Contains("Press → to slide", text, StringComparison.Ordinal);
    }
}
