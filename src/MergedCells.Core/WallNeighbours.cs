namespace MergedCells.Core;

/// <summary>Which of a wall square's eight neighbours are walls too.</summary>
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
        var sides = WallSides.None;
        if (IsWall(map, cell.Row - 1, cell.Column))
        {
            sides |= WallSides.Up;
        }
        if (IsWall(map, cell.Row, cell.Column + 1))
        {
            sides |= WallSides.Right;
        }
        if (IsWall(map, cell.Row + 1, cell.Column))
        {
            sides |= WallSides.Down;
        }
        if (IsWall(map, cell.Row, cell.Column - 1))
        {
            sides |= WallSides.Left;
        }
        sides |= Diagonal(map, sides, WallSides.Up | WallSides.Left, cell.Row - 1, cell.Column - 1, WallSides.UpLeft);
        sides |= Diagonal(map, sides, WallSides.Up | WallSides.Right, cell.Row - 1, cell.Column + 1, WallSides.UpRight);
        sides |= Diagonal(map, sides, WallSides.Down | WallSides.Right, cell.Row + 1, cell.Column + 1, WallSides.DownRight);
        sides |= Diagonal(map, sides, WallSides.Down | WallSides.Left, cell.Row + 1, cell.Column - 1, WallSides.DownLeft);
        return sides;
    }

    private static WallSides Diagonal(Map map, WallSides sides, WallSides both, int row, int column, WallSides diagonal) =>
        (sides & both) == both && IsWall(map, row, column) ? diagonal : WallSides.None;

    private static bool IsWall(Map map, int row, int column)
    {
        var cell = new Cell(row, column);
        return map.Contains(cell) && map.TileAt(cell) == Tile.Wall;
    }
}
