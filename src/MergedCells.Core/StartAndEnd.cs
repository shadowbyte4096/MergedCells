namespace MergedCells.Core;

// The start goes in a top-left area and the end in a bottom-right one, at least a minimum distance apart. The far
// corners are in their areas and 100% apart, so a pair always exists.
internal static class StartAndEnd
{
    public static (Cell Start, Cell End) Place(int size, GeneratorConfig config, GameRandom random)
    {
        var side = Side(size, config);
        var distance = Distance(size, config);

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
        var starts = Starts(size, end, config);
        return (starts[random.Next(starts.Count)], end);
    }

    // Every square the start may sit on for this end, in reading order. The climber moves the start within it
    public static List<Cell> Starts(int size, Cell end, GeneratorConfig config)
    {
        var side = Side(size, config);
        var distance = Distance(size, config);
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
        return starts;
    }

    private static int Side(int size, GeneratorConfig config) => Math.Max(1, Percentage.Of(config.CornerPercent, size));

    private static int Distance(int size, GeneratorConfig config) => Math.Max(1, Percentage.Of(config.MinDistancePercent, 2 * (size - 1)));
}
