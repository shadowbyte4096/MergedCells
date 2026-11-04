namespace MergedCells.Core;

internal readonly record struct RegionBounds(int MinRow, int MaxRow, int MinColumn, int MaxColumn, int Count)
{
    public int Area => (MaxRow - MinRow + 1) * (MaxColumn - MinColumn + 1);

    public RegionBounds Include(int row, int column) => new(
        Math.Min(MinRow, row),
        Math.Max(MaxRow, row),
        Math.Min(MinColumn, column),
        Math.Max(MaxColumn, column),
        Count + 1);

    public Rectangle ToRectangle(int id) => new(id, new Cell(MinRow, MinColumn), MaxRow - MinRow + 1, MaxColumn - MinColumn + 1);

    public static Dictionary<int, RegionBounds> Measure(int[,] regions, out List<int> firstSeenOrder)
    {
        var bounds = new Dictionary<int, RegionBounds>();
        firstSeenOrder = [];
        for (var row = 0; row < regions.GetLength(0); row++)
        {
            for (var column = 0; column < regions.GetLength(1); column++)
            {
                var id = regions[row, column];
                if (bounds.TryGetValue(id, out var region))
                {
                    bounds[id] = region.Include(row, column);
                }
                else
                {
                    bounds[id] = new RegionBounds(row, row, column, column, 1);
                    firstSeenOrder.Add(id);
                }
            }
        }
        return bounds;
    }
}
