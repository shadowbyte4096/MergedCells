namespace MergedCells.Core;

// The start goes in a top-left area and the end in a bottom-right one, at least a minimum distance apart. The far
// corners are in their areas and 100% apart, so a pair always exists.
internal static class StartAndEnd
{
    public static (Cell Start, Cell End) Place(int size, GeneratorConfig config, GameRandom random)
    {
        var side = Math.Max(1, Percentage.Of(config.CornerPercent, size));
        var distance = Math.Max(1, Percentage.Of(config.MinDistancePercent, 2 * (size - 1)));

        // An end is possible when the far corner [0, 0] is far enough from it
        var ends = new List<Cell>();
        for (var row = size - side; row < size; row++)
        {
            for (var column = size - side; column < size; column++)
            {
                if (row + column >= distance)
                {
                    ends.Add(new Cell(row, column));
                }
            }
        }
        var end = ends[random.Next(ends.Count)];

        var starts = new List<Cell>();
        for (var row = 0; row < side && row <= end.Row; row++)
        {
            for (var column = 0; column < side && column <= end.Column; column++)
            {
                if (end.Row - row + end.Column - column >= distance)
                {
                    starts.Add(new Cell(row, column));
                }
            }
        }
        return (starts[random.Next(starts.Count)], end);
    }
}
