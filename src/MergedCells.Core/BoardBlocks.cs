namespace MergedCells.Core;

/// <summary>
/// The map cut into square blocks of squares, for drawing each block once. Block indices run in reading order
/// (block row times <see cref="Columns"/>, plus block column). Each region belongs to the block holding the top-left
/// square of its entry in <see cref="Map.Rectangles"/>, so a ring's block is never after the blocks of the regions
/// inside it.
/// </summary>
public sealed class BoardBlocks
{
    private readonly int mapRows;
    private readonly int mapColumns;
    private readonly int[][] regions;
    // The squares each block's regions cover, which can reach well past the block; null for a block that draws nothing
    private readonly CellRange?[] reach;

    public BoardBlocks(Map map, int size)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);

        Size = size;
        mapRows = map.Rows;
        mapColumns = map.Columns;
        Rows = (map.Rows + size - 1) / size;
        Columns = (map.Columns + size - 1) / size;

        var lists = Enumerable.Range(0, Count).Select(_ => new List<int>()).ToArray();
        for (var index = 0; index < map.Rectangles.Count; index++)
        {
            var topLeft = map.Rectangles[index].TopLeft;
            lists[topLeft.Row / size * Columns + topLeft.Column / size].Add(index);
        }
        regions = lists.Select(list => list.ToArray()).ToArray();
        reach = regions
            .Select(indices => indices.Length == 0
                ? (CellRange?)null
                : new CellRange(
                    indices.Min(index => map.Rectangles[index].TopLeft.Row),
                    indices.Min(index => map.Rectangles[index].TopLeft.Column),
                    indices.Max(index => map.Rectangles[index].BottomRight.Row),
                    indices.Max(index => map.Rectangles[index].BottomRight.Column)))
            .ToArray();
    }

    /// <summary>Squares per block side.</summary>
    public int Size { get; }

    /// <summary>Blocks down.</summary>
    public int Rows { get; }

    /// <summary>Blocks across.</summary>
    public int Columns { get; }

    public int Count => Rows * Columns;

    /// <summary>The block's squares, clamped to the map.</summary>
    public CellRange Area(int block)
    {
        var top = block / Columns * Size;
        var left = block % Columns * Size;
        return new CellRange(top, left, Math.Min(top + Size, mapRows) - 1, Math.Min(left + Size, mapColumns) - 1);
    }

    /// <summary>Indices into <see cref="Map.Rectangles"/> of the regions the block draws, in that order.</summary>
    public IReadOnlyList<int> Regions(int block) => regions[block];

    /// <summary>The blocks whose squares overlap the range, in reading order.</summary>
    public IEnumerable<int> Overlapping(CellRange range)
    {
        for (var row = range.Top / Size; row <= range.Bottom / Size; row++)
        {
            for (var column = range.Left / Size; column <= range.Right / Size; column++)
            {
                yield return row * Columns + column;
            }
        }
    }

    /// <summary>The blocks whose regions reach into the range, so must be drawn to show it, in reading order.</summary>
    public IEnumerable<int> Drawing(CellRange range)
    {
        for (var block = 0; block < Count; block++)
        {
            if (reach[block] is { } covered
                && covered.Top <= range.Bottom && covered.Bottom >= range.Top
                && covered.Left <= range.Right && covered.Right >= range.Left)
            {
                yield return block;
            }
        }
    }

    /// <summary>
    /// Every block, nearest the range first: by the distance in blocks (the larger of rows and columns) from the blocks
    /// the range overlaps, ties in reading order.
    /// </summary>
    public IReadOnlyList<int> BuildOrder(CellRange range)
    {
        var top = range.Top / Size;
        var bottom = range.Bottom / Size;
        var left = range.Left / Size;
        var right = range.Right / Size;
        return Enumerable.Range(0, Count)
            .OrderBy(block =>
            {
                var row = block / Columns;
                var column = block % Columns;
                var rows = Math.Max(0, Math.Max(top - row, row - bottom));
                var columns = Math.Max(0, Math.Max(left - column, column - right));
                return Math.Max(rows, columns);
            })
            .ThenBy(block => block)
            .ToArray();
    }
}
