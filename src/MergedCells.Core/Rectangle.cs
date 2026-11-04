namespace MergedCells.Core;

public readonly record struct Rectangle(int Id, Cell TopLeft, int Rows, int Columns)
{
    public Cell BottomRight => new(TopLeft.Row + Rows - 1, TopLeft.Column + Columns - 1);

    public bool Overlaps(CellRange range) =>
        TopLeft.Row <= range.Bottom && BottomRight.Row >= range.Top
        && TopLeft.Column <= range.Right && BottomRight.Column >= range.Left;
}
