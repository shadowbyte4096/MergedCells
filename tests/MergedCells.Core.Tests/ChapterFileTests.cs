using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class ChapterFileTests
{
    [Fact]
    public void Shipped_chapter_file_round_trips_byte_for_byte()
    {
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "campaign", "chapters.json"));

        Assert.Equal(text, ChapterFile.Write(ShippedCampaignTests.Campaign().Chapters.Select(ChapterEntry.From)));
    }

    [Theory]
    [AutoData]
    public void Written_file_parses_back_to_the_same_chapters(string first, string second, string intro)
    {
        Level[] levels = [new(1, "One", 1, TestMaps.Example(), 3), new(2, "Two", 1, TestMaps.Example(), 3), new(3, "Three", 2, TestMaps.Example(), 3)];
        ChapterEntry[] entries = [new(first, [2, 1], []), new(second, [3], [intro])];

        var chapters = Campaign.Parse(ChapterFile.Write(entries), levels).Chapters;

        Assert.Equal(
            entries.Select(entry => (entry.Name, string.Join(",", entry.LevelIds), string.Join("|", entry.Intro))),
            chapters.Select(chapter => (chapter.Name, string.Join(",", chapter.Levels.Select(level => level.Id)), string.Join("|", chapter.Intro))));
    }
}
