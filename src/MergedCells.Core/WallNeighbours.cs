namespace MergedCells.Core;

/// <summary>
/// Which of a square's eight neighbours it joins into one shape with: walls for a wall
/// (<see cref="WallNeighbours"/>), sticky squares in its region for a sticky square (<see cref="StickyNeighbours"/>).
/// </summary>
[Flags]
public enum WallSides : byte
{
    None = 0,
    Up = 1,
    Right = 2,
    Down = 4,
    Left = 8,
    UpLeft = 16,
    UpRight = 32,
    DownRight = 64,
    DownLeft = 128,
}

public static class WallNeighbours
{
    /// <summary>
    /// The walls round a square. Squares off the map aren't walls. A diagonal is set only when the two sides next to it
    /// are walls as well: otherwise it doesn't change the square's shape, so squares that look alike get the same value.
    /// </summary>
    public static WallSides Of(Map map, Cell cell)
    {
        ArgumentNullException.ThrowIfNull(map);
        return JoinedSides.Of(map, cell, neighbour => map.TileAt(neighbour) == Tile.Wall);
    }
}
