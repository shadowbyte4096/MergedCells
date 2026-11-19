namespace MergedCells.Core;

/// <summary>
/// The level editor's edits. Each takes a valid map and returns a new valid map, the same map unchanged, or the same map
/// with a short reason for the player. Every new map is built by <see cref="Map.Create"/>, with its regions numbered 1, 2,
/// 3… in reading order, and a block left in pieces becomes one block per piece.
/// </summary>
public static class LevelEdits
{
    /// <summary>Every square its own block, the start at the top-left, the end at the bottom-right, and no twists.</summary>
    public static Map Blank(int rows, int columns)
    {
        if (rows < 1 || columns < 1 || rows * columns < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(rows), $"A {rows}×{columns} board can't hold a start and an end.");
        }
        var grid = new int[rows, columns];
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                grid[row, column] = row * columns + column + 1;
            }
        }
        return Map.Create(grid, new Cell(0, 0), new Cell(rows - 1, columns - 1));
    }

    /// <summary>
    /// The Blocks tool's drag: the block under the path's first square takes every square of the path, which must run
    /// square by square, each sharing an edge with the last. The end always stays a block of its own, so the path stops
    /// where it first reaches the end, and a path that starts on the end changes nothing. Twists stay where they are.
    /// Throws <see cref="ArgumentException"/> for an empty path, a square off the map or a gap.
    /// </summary>
    public static EditResult Paint(Map map, IReadOnlyList<Cell> path)
    {
        ArgumentNullException.ThrowIfNull(map);
        CheckPath(map, path);
        var grid = map.RegionGrid();
        var block = grid[path[0].Row, path[0].Column];
        foreach (var cell in path)
        {
            if (cell == map.End)
            {
                break;
            }
            grid[cell.Row, cell.Column] = block;
        }
        return Finish(map, grid, map.TileGrid(), map.Teleporters, map.Start, map.End);
    }

    /// <summary>The Blocks tool's tap: the square becomes a block of its own, and the rest of its old block one block per piece.</summary>
    public static EditResult Split(Map map, Cell cell)
    {
        ArgumentNullException.ThrowIfNull(map);
        CheckInside(map, cell);
        var grid = map.RegionGrid();
        grid[cell.Row, cell.Column] = RegionGrids.FreshId(grid);
        return Finish(map, grid, map.TileGrid(), map.Teleporters, map.Start, map.End);
    }

    /// <summary>
    /// The Eraser. The stroke's first square decides what it changes. A wall removes walls, a trap removes traps, and a
    /// teleporter square removes every pair the stroke touches (the later pairs move up a letter); none of these touch the
    /// blocks or the other twists. A square with no twist makes each square of the stroke a block of its own and keeps
    /// every twist. Throws <see cref="ArgumentException"/> for an empty stroke or a square off the map.
    /// </summary>
    public static EditResult Erase(Map map, IReadOnlyList<Cell> stroke)
    {
        var mode = EraseMode(map, stroke);
        var targets = Targets(map, stroke, mode);
        var grid = map.RegionGrid();
        var tiles = map.TileGrid();
        var pairs = map.Teleporters.ToList();
        var fresh = RegionGrids.FreshId(grid);
        foreach (var cell in targets)
        {
            if (mode == Erasing.Blocks)
            {
                grid[cell.Row, cell.Column] = fresh++;
            }
            else
            {
                ClearTwist(tiles, pairs, cell);
            }
        }
        return Finish(map, grid, tiles, pairs, map.Start, map.End);
    }

    /// <summary>
    /// The squares <see cref="Erase"/> would change, each once in stroke order, with a teleporter square's partner right
    /// after it: only the squares holding the first square's twist, or every square when the first square has none.
    /// </summary>
    public static IReadOnlyList<Cell> EraseTargets(Map map, IReadOnlyList<Cell> stroke) => Targets(map, stroke, EraseMode(map, stroke));

    /// <summary>
    /// Wall and Trap: marks each square, replacing the other of the two. Skips the start, the end and teleporter squares,
    /// and is refused when every square was skipped, so a tap on one says why. Throws
    /// <see cref="ArgumentOutOfRangeException"/> for any tile but a wall or a trap.
    /// </summary>
    public static EditResult PlaceTile(Map map, IReadOnlyCollection<Cell> cells, Tile tile)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(cells);
        if (tile is not (Tile.Wall or Tile.Trap))
        {
            throw new ArgumentOutOfRangeException(nameof(tile), tile, "Only walls and traps are placed square by square.");
        }
        var tiles = map.TileGrid();
        string? refusal = null;
        var marked = false;
        foreach (var cell in cells)
        {
            CheckInside(map, cell);
            var skipped = SkipReason(map, cell, tile);
            if (skipped is not null)
            {
                refusal ??= skipped;
                continue;
            }
            tiles[cell.Row, cell.Column] = tile;
            marked = true;
        }
        if (!marked && refusal is not null)
        {
            return EditResult.Refused(map, refusal);
        }
        return Finish(map, map.RegionGrid(), tiles, map.Teleporters, map.Start, map.End);
    }

    /// <summary>Why the square can't be one end of a new teleporter pair, or null when it can.</summary>
    public static string? CanHoldTeleporter(Map map, Cell cell)
    {
        ArgumentNullException.ThrowIfNull(map);
        CheckInside(map, cell);
        if (cell == map.Start)
        {
            return "Teleporters can't go on the start";
        }
        if (cell == map.End)
        {
            return "Teleporters can't go on the end";
        }
        if (PairIndex(map.Teleporters, cell) >= 0)
        {
            return "That square already has a teleporter";
        }
        return map.Teleporters.Count >= TeleporterLetters.Count ? $"No more letters: {TeleporterLetters.Count} pairs is the most" : null;
    }

    /// <summary>
    /// Teleport: adds the pair last, so it takes the next letter, clearing any wall or trap on its squares. Refused for one
    /// square twice, or a square <see cref="CanHoldTeleporter"/> refuses.
    /// </summary>
    public static EditResult AddTeleporters(Map map, Cell first, Cell second)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (first == second)
        {
            CheckInside(map, first);
            return EditResult.Refused(map, "Pick a different square for the other end");
        }
        if ((CanHoldTeleporter(map, first) ?? CanHoldTeleporter(map, second)) is { } refusal)
        {
            return EditResult.Refused(map, refusal);
        }
        var tiles = map.TileGrid();
        tiles[first.Row, first.Column] = Tile.None;
        tiles[second.Row, second.Column] = Tile.None;
        return Finish(map, map.RegionGrid(), tiles, [.. map.Teleporters, new TeleporterPair(first, second)], map.Start, map.End);
    }

    /// <summary>
    /// Start: refused on the end's square and to the right of or below the end. A twist on the new square goes (a
    /// teleporter's whole pair).
    /// </summary>
    public static EditResult MoveStart(Map map, Cell cell)
    {
        ArgumentNullException.ThrowIfNull(map);
        CheckInside(map, cell);
        if (cell == map.Start)
        {
            return EditResult.Unchanged(map);
        }
        if (cell == map.End)
        {
            return EditResult.Refused(map, "The start and end need squares of their own");
        }
        if (cell.Row > map.End.Row || cell.Column > map.End.Column)
        {
            return EditResult.Refused(map, "The start can't be right of or below the end");
        }
        var tiles = map.TileGrid();
        var pairs = map.Teleporters.ToList();
        ClearTwist(tiles, pairs, cell);
        return Finish(map, map.RegionGrid(), tiles, pairs, cell, map.End);
    }

    /// <summary>
    /// End: refused on the start's square and to the left of or above the start. The new square becomes a plain block of
    /// its own (a teleporter's whole pair goes); the old end square stays a single square.
    /// </summary>
    public static EditResult MoveEnd(Map map, Cell cell)
    {
        ArgumentNullException.ThrowIfNull(map);
        CheckInside(map, cell);
        if (cell == map.End)
        {
            return EditResult.Unchanged(map);
        }
        if (cell == map.Start)
        {
            return EditResult.Refused(map, "The start and end need squares of their own");
        }
        if (map.Start.Row > cell.Row || map.Start.Column > cell.Column)
        {
            return EditResult.Refused(map, "The end can't be left of or above the start");
        }
        var grid = map.RegionGrid();
        var tiles = map.TileGrid();
        var pairs = map.Teleporters.ToList();
        ClearTwist(tiles, pairs, cell);
        grid[cell.Row, cell.Column] = RegionGrids.FreshId(grid);
        return Finish(map, grid, tiles, pairs, map.Start, cell);
    }

    /// <summary>
    /// Keeps the top-left. New squares are single blocks; twists and pairs with a square outside go; a start or end outside
    /// moves to the nearest square inside, and the end's square is made a plain block of its own. Refused when the start
    /// and end would share a square. Throws <see cref="ArgumentOutOfRangeException"/> for a side below 1.
    /// </summary>
    public static EditResult Resize(Map map, int rows, int columns)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentOutOfRangeException.ThrowIfLessThan(rows, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 1);
        if (rows == map.Rows && columns == map.Columns)
        {
            return EditResult.Unchanged(map);
        }
        var start = new Cell(Math.Min(map.Start.Row, rows - 1), Math.Min(map.Start.Column, columns - 1));
        var end = new Cell(Math.Min(map.End.Row, rows - 1), Math.Min(map.End.Column, columns - 1));
        if (start == end)
        {
            return EditResult.Refused(map, "Too small to keep the start and end apart");
        }
        var oldGrid = map.RegionGrid();
        var oldTiles = map.TileGrid();
        var grid = new int[rows, columns];
        var tiles = new Tile[rows, columns];
        var fresh = RegionGrids.FreshId(oldGrid);
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var inside = row < map.Rows && column < map.Columns;
                grid[row, column] = inside ? oldGrid[row, column] : fresh++;
                tiles[row, column] = inside ? oldTiles[row, column] : Tile.None;
            }
        }
        var pairs = map.Teleporters
            .Where(pair => Map.IsInside(pair.First, rows, columns) && Map.IsInside(pair.Second, rows, columns))
            .ToList();
        ClearTwist(tiles, pairs, start);
        ClearTwist(tiles, pairs, end);
        grid[end.Row, end.Column] = fresh;
        return Finish(map, grid, tiles, pairs, start, end);
    }

    /// <summary>A blank level the same size: <see cref="Blank"/>, or unchanged when the level is blank already.</summary>
    public static EditResult Clear(Map map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var blank = Blank(map.Rows, map.Columns);
        return RegionGrids.SameLevel(map, blank) ? EditResult.Unchanged(map) : EditResult.To(blank);
    }

    private static string? SkipReason(Map map, Cell cell, Tile tile)
    {
        var kind = tile == Tile.Wall ? "Walls" : "Traps";
        if (cell == map.Start)
        {
            return $"{kind} can't go on the start";
        }
        if (cell == map.End)
        {
            return $"{kind} can't go on the end";
        }
        return PairIndex(map.Teleporters, cell) >= 0 ? "Erase the teleporter first" : null;
    }

    private enum Erasing
    {
        Blocks,
        Walls,
        Traps,
        Teleporters,
    }

    private static Erasing EraseMode(Map map, IReadOnlyList<Cell> stroke)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(stroke);
        if (stroke.Count == 0)
        {
            throw new ArgumentException("A stroke needs at least one square.", nameof(stroke));
        }
        foreach (var cell in stroke)
        {
            CheckInside(map, cell);
        }
        return Holds(map, stroke[0]);
    }

    private static Erasing Holds(Map map, Cell cell) => map.TileAt(cell) switch
    {
        Tile.Wall => Erasing.Walls,
        Tile.Trap => Erasing.Traps,
        _ => PairIndex(map.Teleporters, cell) >= 0 ? Erasing.Teleporters : Erasing.Blocks,
    };

    private static List<Cell> Targets(Map map, IReadOnlyList<Cell> stroke, Erasing mode)
    {
        var targets = new List<Cell>();
        var listed = new HashSet<Cell>();
        foreach (var cell in stroke)
        {
            if (mode != Erasing.Blocks && Holds(map, cell) != mode)
            {
                continue;
            }
            if (listed.Add(cell))
            {
                targets.Add(cell);
            }
            if (mode == Erasing.Teleporters)
            {
                var pair = map.Teleporters[PairIndex(map.Teleporters, cell)];
                var partner = pair.First == cell ? pair.Second : pair.First;
                if (listed.Add(partner))
                {
                    targets.Add(partner);
                }
            }
        }
        return targets;
    }

    // A square's wall or trap, or its teleporter's whole pair
    private static void ClearTwist(Tile[,] tiles, List<TeleporterPair> pairs, Cell cell)
    {
        tiles[cell.Row, cell.Column] = Tile.None;
        var pair = PairIndex(pairs, cell);
        if (pair >= 0)
        {
            pairs.RemoveAt(pair);
        }
    }

    private static int PairIndex(IReadOnlyList<TeleporterPair> pairs, Cell cell)
    {
        for (var index = 0; index < pairs.Count; index++)
        {
            if (pairs[index].First == cell || pairs[index].Second == cell)
            {
                return index;
            }
        }
        return -1;
    }

    private static EditResult Finish(Map before, int[,] regions, Tile[,] tiles, IReadOnlyList<TeleporterPair> pairs, Cell start, Cell end)
    {
        var after = RegionGrids.Rebuild(regions, tiles, pairs, start, end);
        return RegionGrids.SameLevel(before, after) ? EditResult.Unchanged(before) : EditResult.To(after);
    }

    private static void CheckPath(Map map, IReadOnlyList<Cell> path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path.Count == 0)
        {
            throw new ArgumentException("A path needs at least one square.", nameof(path));
        }
        for (var index = 0; index < path.Count; index++)
        {
            CheckInside(map, path[index]);
            if (index > 0 && path[index].DistanceTo(path[index - 1]) != 1)
            {
                throw new ArgumentException($"The path jumps from {path[index - 1]} to {path[index]}.", nameof(path));
            }
        }
    }

    private static void CheckInside(Map map, Cell cell)
    {
        if (!map.Contains(cell))
        {
            throw new ArgumentException($"{cell} is off the {map.Rows}×{map.Columns} map.", nameof(cell));
        }
    }
}
