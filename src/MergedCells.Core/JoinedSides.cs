namespace MergedCells.Core;

// The neighbour rule walls and sticky squares share: which of a square's eight neighbours it joins into one shape with
internal static class JoinedSides
{
    public static WallSides Of(Map map, Cell cell, Func<Cell, bool> joins)
    {
        var sides = WallSides.None;
        if (Joins(map, joins, cell.Row - 1, cell.Column))
        {
            sides |= WallSides.Up;
        }
        if (Joins(map, joins, cell.Row, cell.Column + 1))
        {
            sides |= WallSides.Right;
        }
        if (Joins(map, joins, cell.Row + 1, cell.Column))
        {
            sides |= WallSides.Down;
        }
        if (Joins(map, joins, cell.Row, cell.Column - 1))
        {
            sides |= WallSides.Left;
        }
        sides |= Diagonal(map, joins, sides, WallSides.Up | WallSides.Left, cell.Row - 1, cell.Column - 1, WallSides.UpLeft);
        sides |= Diagonal(map, joins, sides, WallSides.Up | WallSides.Right, cell.Row - 1, cell.Column + 1, WallSides.UpRight);
        sides |= Diagonal(map, joins, sides, WallSides.Down | WallSides.Right, cell.Row + 1, cell.Column + 1, WallSides.DownRight);
        sides |= Diagonal(map, joins, sides, WallSides.Down | WallSides.Left, cell.Row + 1, cell.Column - 1, WallSides.DownLeft);
        return sides;
    }

    // A diagonal changes the square's shape only when both sides next to it are joined; dropped otherwise, squares
    // that look alike get the same value
    private static WallSides Diagonal(
        Map map, Func<Cell, bool> joins, WallSides sides, WallSides both, int row, int column, WallSides diagonal) =>
        (sides & both) == both && Joins(map, joins, row, column) ? diagonal : WallSides.None;

    private static bool Joins(Map map, Func<Cell, bool> joins, int row, int column)
    {
        var cell = new Cell(row, column);
        return map.Contains(cell) && joins(cell);
    }
}
