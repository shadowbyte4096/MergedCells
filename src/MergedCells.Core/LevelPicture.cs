namespace MergedCells.Core;

// A run of one region's squares along a row, in its fill colour's index
public readonly record struct PictureRun(int Row, int Column, int Length, int Color);

// A boundary between two regions: a vertical one on the left edge of Column from Row down, a horizontal one on the top
// edge of Row from Column across
public readonly record struct PictureLine(bool Vertical, int Row, int Column, int Length);

/// <summary>
/// What a level's small picture draws, in squares (the custom levels list and the campaign picker): each region's fill,
/// ink lines where regions meet, merged into straight runs (the map's own edge isn't listed; the view frames it), and the
/// twists, start and end. Lines are listed vertical first, column by column top down, then horizontal, row by row.
/// </summary>
public sealed record LevelPicture(
    int Rows,
    int Columns,
    IReadOnlyList<PictureRun> Fills,
    IReadOnlyList<PictureLine> Lines,
    IReadOnlyList<Cell> Walls,
    IReadOnlyList<Cell> Traps,
    IReadOnlyList<Cell> Teleporters,
    Cell Start,
    Cell End)
{
    // Fill colours from RectangleColoring, so neighbours never share one, as on the board
    public static LevelPicture Of(Map map, int colors)
    {
        ArgumentNullException.ThrowIfNull(map);
        var coloring = RectangleColoring.Assign(map, colors);
        var colorById = new Dictionary<int, int>();
        for (var index = 0; index < map.Rectangles.Count; index++)
        {
            colorById[map.Rectangles[index].Id] = coloring[index];
        }

        var fills = new List<PictureRun>();
        for (var row = 0; row < map.Rows; row++)
        {
            var start = 0;
            for (var column = 1; column <= map.Columns; column++)
            {
                if (column == map.Columns || Region(map, row, column) != Region(map, row, start))
                {
                    fills.Add(new PictureRun(row, start, column - start, colorById[Region(map, row, start)]));
                    start = column;
                }
            }
        }

        var lines = new List<PictureLine>();
        for (var column = 1; column < map.Columns; column++)
        {
            AddRuns(lines, map.Rows, row => Region(map, row, column - 1) != Region(map, row, column), (from, length) => new(true, from, column, length));
        }
        for (var row = 1; row < map.Rows; row++)
        {
            AddRuns(lines, map.Columns, column => Region(map, row - 1, column) != Region(map, row, column), (from, length) => new(false, row, from, length));
        }

        var walls = new List<Cell>();
        var traps = new List<Cell>();
        for (var row = 0; row < map.Rows; row++)
        {
            for (var column = 0; column < map.Columns; column++)
            {
                var cell = new Cell(row, column);
                switch (map.TileAt(cell))
                {
                    case Tile.Wall:
                        walls.Add(cell);
                        break;
                    case Tile.Trap:
                        traps.Add(cell);
                        break;
                }
            }
        }
        var teleporters = map.Teleporters.SelectMany(pair => new[] { pair.First, pair.Second }).ToArray();
        return new LevelPicture(map.Rows, map.Columns, fills, lines, walls, traps, teleporters, map.Start, map.End);
    }

    private static int Region(Map map, int row, int column) => map.RegionAt(new Cell(row, column));

    // Each unbroken stretch of positions where a boundary lies becomes one line
    private static void AddRuns(List<PictureLine> lines, int count, Func<int, bool> isBoundary, Func<int, int, PictureLine> line)
    {
        var from = -1;
        for (var position = 0; position <= count; position++)
        {
            var boundary = position < count && isBoundary(position);
            if (boundary && from < 0)
            {
                from = position;
            }
            else if (!boundary && from >= 0)
            {
                lines.Add(line(from, position - from));
                from = -1;
            }
        }
    }
}
