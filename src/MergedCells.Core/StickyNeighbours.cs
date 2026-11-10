namespace MergedCells.Core;

public static class StickyNeighbours
{
    /// <summary>
    /// The neighbours a sticky square joins into one shape with: sticky squares in its own region. Squares off the map
    /// aren't joined. A diagonal is set only when the two sides next to it are joined as well, as
    /// <see cref="WallNeighbours.Of"/> does.
    /// </summary>
    public static WallSides Of(Map map, Cell cell)
    {
        ArgumentNullException.ThrowIfNull(map);
        var region = map.RegionAt(cell);
        return JoinedSides.Of(map, cell, neighbour => map.TileAt(neighbour) == Tile.Sticky && map.RegionAt(neighbour) == region);
    }
}
