namespace MergedCells.Core;

public static class CellPath
{
    /// <summary>
    /// The squares from <paramref name="from"/> to <paramref name="to"/>, both included, each sharing an edge with the one
    /// before, keeping as close to the straight line as it can, a horizontal step first on a tie. Integer arithmetic only.
    /// </summary>
    public static IReadOnlyList<Cell> Line(Cell from, Cell to)
    {
        var rows = Math.Abs(to.Row - from.Row);
        var columns = Math.Abs(to.Column - from.Column);
        var rowStep = Math.Sign(to.Row - from.Row);
        var columnStep = Math.Sign(to.Column - from.Column);
        var path = new List<Cell>(rows + columns + 1) { from };
        var row = 0;
        var column = 0;
        while (row < rows || column < columns)
        {
            // Step along whichever axis's next step comes first along the line: the column's at (column + 1) / columns of
            // the way, the row's at (row + 1) / rows, compared without dividing
            var stepColumn = row == rows || (column < columns && (long)(column + 1) * rows <= (long)(row + 1) * columns);
            if (stepColumn)
            {
                column++;
            }
            else
            {
                row++;
            }
            path.Add(new Cell(from.Row + row * rowStep, from.Column + column * columnStep));
        }
        return path;
    }
}
