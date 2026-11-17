using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class CustomLevelCopiesTests
{
    private static readonly DateTimeOffset Then = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [AutoData]
    public void Own_copy_has_the_new_id_and_time_and_isn_t_imported(Guid original, Guid copyId, string name)
    {
        var imported = new CustomLevel(original, name, TestMaps.Example(), Then, Then.AddDays(1));

        var copy = CustomLevelCopies.OwnCopy(imported, copyId, Then.AddDays(2));

        Assert.Equal((copyId, Then.AddDays(2), false), (copy.Id, copy.Edited, copy.IsImported));
    }

    [Theory]
    [AutoData]
    public void Own_copy_keeps_the_name_and_map(Guid original, Guid copyId, string name)
    {
        var imported = new CustomLevel(original, name, TestMaps.Example(), Then, Then);

        var copy = CustomLevelCopies.OwnCopy(imported, copyId, Then);

        Assert.Equal((name, imported.Map), (copy.Name, copy.Map));
    }

    // The editor's first edit compares grids, so a copy starts relabelled as every edit leaves a map
    [Theory]
    [AutoData]
    public void Campaign_copy_is_normalised(Guid id)
    {
        var level = new Level(7, "Top Shelf", 1, Map.Create(new[,] { { 7, 7, 3 }, { 9, 9, 5 } }, new(0, 0), new(1, 2)), 2);

        var copy = CustomLevelCopies.FromCampaign(level, id, Then, 16);

        Assert.Equal(new[,] { { 1, 1, 2 }, { 3, 3, 4 } }, copy.Map.RegionGrid());
    }

    [Theory]
    [AutoData]
    public void Campaign_copy_keeps_the_name_twists_and_ends(Guid id)
    {
        var map = Map.Create(TestMaps.Grid("ABBBB", "CCCCZ"), new(0, 0), new(1, 4), TestMaps.Traps(2, 5, (0, 3)));
        var level = new Level(7, "Near Miss", 4, map, 3);

        var copy = CustomLevelCopies.FromCampaign(level, id, Then, 16);

        Assert.Equal(("Near Miss", false), (copy.Name, copy.IsImported));
        Assert.True(RegionGrids.SameLevel(map, copy.Map));
    }

    [Theory]
    [AutoData]
    public void Campaign_copy_cuts_a_long_name(Guid id)
    {
        var level = new Level(7, "Abcdefghijklmnopq", 1, TestMaps.Example(), 3);

        Assert.Equal("Abcdefghijklmnop", CustomLevelCopies.FromCampaign(level, id, Then, 16).Name);
    }

    // A cut that ends on a space leaves no trailing space
    [Theory]
    [AutoData]
    public void Campaign_copy_trims_a_cut_name(Guid id)
    {
        var level = new Level(7, "Over the Top", 1, TestMaps.Example(), 3);

        Assert.Equal("Over", CustomLevelCopies.FromCampaign(level, id, Then, 5).Name);
    }

    [Theory]
    [AutoData]
    public void Campaign_copy_of_a_sticky_level_throws(Guid id)
    {
        var sticky = new Tile[4, 4];
        sticky[0, 1] = Tile.Sticky;
        var level = new Level(7, "Sticky", 1, Map.Create(TestMaps.ExampleRegions(), TestMaps.ExampleStart, TestMaps.ExampleEnd, sticky), 3);

        Assert.Throws<ArgumentException>(() => CustomLevelCopies.FromCampaign(level, id, Then, 16));
    }
}
