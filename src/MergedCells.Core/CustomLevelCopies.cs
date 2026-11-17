using System.Globalization;

namespace MergedCells.Core;

/// <summary>New levels of the player's own made from others: "Edit a copy" of an imported level, and a 3-starred campaign level.</summary>
public static class CustomLevelCopies
{
    // The same name and map under a new ID; the copy is the player's own
    public static CustomLevel OwnCopy(CustomLevel level, Guid id, DateTimeOffset edited)
    {
        ArgumentNullException.ThrowIfNull(level);
        return new CustomLevel(id, level.Name, level.Map, edited);
    }

    /// <summary>
    /// The campaign level as the player's own custom level: its map relabelled as every edit leaves one, its name cut to
    /// <paramref name="maxNameLength"/> text elements if longer, and no tutorial. Throws <see cref="ArgumentException"/>
    /// for a sticky square, which custom levels can't hold (no shipped level has one).
    /// </summary>
    public static CustomLevel FromCampaign(Level level, Guid id, DateTimeOffset edited, int maxNameLength)
    {
        ArgumentNullException.ThrowIfNull(level);
        var map = level.Map;
        var tiles = map.HasTiles ? map.TileGrid() : null;
        if (tiles is not null && tiles.Cast<Tile>().Contains(Tile.Sticky))
        {
            throw new ArgumentException($"Level {level.Id} has a sticky square, which a custom level can't hold.", nameof(level));
        }
        var copy = RegionGrids.Rebuild(map.RegionGrid(), tiles, map.Teleporters, map.Start, map.End);
        return new CustomLevel(id, Name(level.Name, maxNameLength), copy, edited);
    }

    private static string Name(string name, int maxNameLength)
    {
        if (CustomLevelNames.Clean(name, maxNameLength) is { } clean)
        {
            return clean;
        }
        var info = new StringInfo(CustomLevelNames.Clean(name, int.MaxValue) ?? CustomLevelNames.Untitled);
        var cut = info.SubstringByTextElements(0, Math.Min(maxNameLength, info.LengthInTextElements)).Trim();
        return CustomLevelNames.Clean(cut, maxNameLength) ?? CustomLevelNames.Untitled;
    }
}
