using MergedCells.Curation;

namespace MergedCells.Core.Tests;

// Level IDs are never reused: saved bests and leaderboards key off them. The tool's ID file holds every ID taken out
public class ShippedLevelIdsTests
{
    private static IReadOnlyList<int> Retired() =>
        RetiredLevels.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "curation", "retired-levels.json")));

    private static int[] Shipped() => ShippedCampaignTests.Campaign().Levels.Select(level => level.Id).ToArray();

    [Fact]
    public void No_level_uses_a_retired_id()
    {
        Assert.Empty(Shipped().Intersect(Retired()));
    }

    [Fact]
    public void No_id_is_retired_twice()
    {
        Assert.Equal(Retired().Count, Retired().Distinct().Count());
    }

    // Also catches a level file deleted by hand without retiring it, unless it had the highest ID
    [Fact]
    public void Every_id_up_to_the_highest_is_shipped_or_retired()
    {
        var taken = Shipped().Concat(Retired()).ToHashSet();

        Assert.All(Enumerable.Range(1, taken.Max()), id => Assert.Contains(id, taken));
    }
}
