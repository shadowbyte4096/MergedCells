using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MergedCells.Core;

// Reads one level file in section 7's format
public static class LevelFile
{
    private const char ByteOrderMark = (char)0xFEFF;

    /// <summary>
    /// Parses a level file's text. Throws <see cref="JsonException"/>, naming the problem, when a value is missing, null,
    /// of the wrong type or out of range, when a key is unknown or repeated, when a grid's rows differ in length, when a tile code is
    /// unknown, when a teleporter pair isn't two squares of two numbers each, when a tutorial has no lines, more than
    /// two or a blank one, or an unknown swipe direction, or when the text isn't JSON. Throws
    /// <see cref="InvalidMapException"/> when the map breaks a map rule.
    /// </summary>
    public static Level Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        // Some Windows editors save UTF-8 with a byte order mark, which System.Text.Json rejects
        var text = json.TrimStart(ByteOrderMark);
        var file = JsonSerializer.Deserialize(text, LevelJsonContext.Default.LevelJson)
            ?? throw new JsonException("The level is null.");
        JsonKeys.RejectDuplicates(text, "level");

        // .NET 8's System.Text.Json enforces required keys but not non-null values, so each is checked by name
        RequireValue(file.Name, "name");
        RequireValue(file.Regions, "regions");
        RequireValue(file.Start, "start");
        RequireValue(file.End, "end");
        if (file.Id < 1 || file.Chapter < 1 || file.Par < 1)
        {
            throw new JsonException($"The level's \"id\" ({file.Id}), \"chapter\" ({file.Chapter}) and \"par\" ({file.Par}) must be at least 1.");
        }
        if (string.IsNullOrWhiteSpace(file.Name))
        {
            throw new JsonException("The level's \"name\" is blank.");
        }

        var map = ReadMap(file.Regions, file.Tiles, file.Teleporters, file.Start, file.End);
        return new Level(file.Id, file.Name, file.Chapter, map, file.Par, ReadTutorial(file.Tutorial));
    }

    // The map keys, shared with custom level files
    internal static Map ReadMap(int[][] regions, int[][]? tiles, int[][][]? teleporters, int[] start, int[] end) =>
        Map.Create(
            Grid(regions, "regions"),
            Coordinate(start, "start"),
            Coordinate(end, "end"),
            tiles is null ? null : Tiles(Grid(tiles, "tiles")),
            teleporters is null ? null : Teleporters(teleporters));

    /// <summary>
    /// The level's file text in the hand-written layout: one key per line in section 7's order, one grid row per line with
    /// every number padded to the grid's widest, <c>tiles</c> and <c>teleporters</c> written as null when there are none,
    /// one teleporter pair per line in the map's order, and the tutorial last. Region IDs are renumbered from 1 in reading
    /// order, which changes no fingerprint. LF line endings and a final newline.
    /// </summary>
    public static string Write(Level level)
    {
        ArgumentNullException.ThrowIfNull(level);
        var text = new StringBuilder("{\n");
        text.Append("  \"id\": ").Append(Number(level.Id)).Append(",\n");
        text.Append("  \"name\": ").Append(JsonText.Quote(level.Name)).Append(",\n");
        text.Append("  \"chapter\": ").Append(Number(level.Chapter)).Append(",\n");
        AppendMap(text, level.Map);
        text.Append(",\n  \"par\": ").Append(Number(level.Par));
        if (level.Tutorial is { } tutorial)
        {
            var keys = new List<string> { $"    \"lines\": {Lines(tutorial.Lines)}" };
            if (!tutorial.KeyboardLines.SequenceEqual(tutorial.Lines))
            {
                keys.Add($"    \"keyboardLines\": {Lines(tutorial.KeyboardLines)}");
            }
            if (tutorial.SwipeHint is { } hint)
            {
                keys.Add($"    \"swipeHint\": {JsonText.Quote(hint.ToString().ToLowerInvariant())}");
            }
            text.Append(",\n  \"tutorial\": {\n").Append(string.Join(",\n", keys)).Append("\n  }");
        }
        return text.Append("\n}\n").ToString();
    }

    // From "regions" to the end square, with no comma or newline after it, so each file adds its own keys after
    internal static void AppendMap(StringBuilder text, Map map)
    {
        text.Append("  \"regions\": [\n").Append(Rows(RenumberedRegions(map))).Append("  ],\n");
        if (map.HasTiles)
        {
            text.Append("  \"tiles\": [\n").Append(Rows(TileCodes(map))).Append("  ],\n");
        }
        else
        {
            text.Append("  \"tiles\": null,\n");
        }
        if (map.Teleporters.Count > 0)
        {
            var pairs = map.Teleporters.Select(pair => $"    [{Square(pair.First)}, {Square(pair.Second)}]");
            text.Append("  \"teleporters\": [\n").Append(string.Join(",\n", pairs)).Append("\n  ],\n");
        }
        else
        {
            text.Append("  \"teleporters\": null,\n");
        }
        text.Append("  \"start\": ").Append(Square(map.Start)).Append(",\n");
        text.Append("  \"end\": ").Append(Square(map.End));
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Square(Cell cell) => $"[{Number(cell.Row)}, {Number(cell.Column)}]";

    private static string Lines(IEnumerable<string> lines) => "[" + string.Join(", ", lines.Select(JsonText.Quote)) + "]";

    // Every number left-padded to the grid's widest, so the columns line up as the hand-written files have them
    private static string Rows(int[,] grid)
    {
        var width = 1;
        foreach (var value in grid)
        {
            width = Math.Max(width, Number(value).Length);
        }
        var rows = Enumerable.Range(0, grid.GetLength(0)).Select(row =>
            "    [" + string.Join(", ", Enumerable.Range(0, grid.GetLength(1)).Select(column => Number(grid[row, column]).PadLeft(width))) + "]");
        return string.Join(",\n", rows) + "\n";
    }

    // The map keys' values as arrays, regions renumbered as the files have them, for formats that write them on one line
    internal static MapArrays ArraysOf(Map map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return new MapArrays(
            Jagged(RenumberedRegions(map)),
            map.HasTiles ? Jagged(TileCodes(map)) : null,
            map.Teleporters.Count > 0
                ? map.Teleporters.Select(pair => new[] { new[] { pair.First.Row, pair.First.Column }, new[] { pair.Second.Row, pair.Second.Column } }).ToArray()
                : null,
            [map.Start.Row, map.Start.Column],
            [map.End.Row, map.End.Column]);
    }

    private static int[][] Jagged(int[,] grid) =>
        Enumerable.Range(0, grid.GetLength(0))
            .Select(row => Enumerable.Range(0, grid.GetLength(1)).Select(column => grid[row, column]).ToArray())
            .ToArray();

    private static int[,] RenumberedRegions(Map map)
    {
        var grid = map.RegionGrid();
        var numbers = new Dictionary<int, int>();
        for (var row = 0; row < map.Rows; row++)
        {
            for (var column = 0; column < map.Columns; column++)
            {
                if (!numbers.TryGetValue(grid[row, column], out var number))
                {
                    number = numbers.Count + 1;
                    numbers[grid[row, column]] = number;
                }
                grid[row, column] = number;
            }
        }
        return grid;
    }

    private static int[,] TileCodes(Map map)
    {
        var codes = new int[map.Rows, map.Columns];
        for (var row = 0; row < map.Rows; row++)
        {
            for (var column = 0; column < map.Columns; column++)
            {
                codes[row, column] = (int)map.TileAt(new Cell(row, column));
            }
        }
        return codes;
    }

    // Only the shape is checked here; Map.Create checks where the pairs sit
    private static TeleporterPair[] Teleporters(int[][][] pairs)
    {
        var read = new TeleporterPair[pairs.Length];
        for (var index = 0; index < pairs.Length; index++)
        {
            var pair = pairs[index] ?? throw new JsonException($"The level's \"teleporters\" pair {index} is null.");
            if (pair.Length != 2)
            {
                throw new JsonException($"The level's \"teleporters\" pair {index} has {pair.Length} squares; it needs two.");
            }
            read[index] = new TeleporterPair(PairSquare(pair[0], index), PairSquare(pair[1], index));
        }
        return read;
    }

    private static Cell PairSquare(int[]? square, int index)
    {
        if (square is null)
        {
            throw new JsonException($"The level's \"teleporters\" pair {index} has a null square.");
        }
        return square.Length == 2
            ? new Cell(square[0], square[1])
            : throw new JsonException(
                $"The level's \"teleporters\" pair {index} has a square of {square.Length} numbers; it needs two, [row, column].");
    }

    private static Tutorial? ReadTutorial(TutorialJson? tutorial)
    {
        if (tutorial is null)
        {
            return null;
        }
        var lines = TutorialLines(tutorial.Lines, "lines");
        var keyboardLines = tutorial.KeyboardLines is null ? lines : TutorialLines(tutorial.KeyboardLines, "keyboardLines");
        return new Tutorial(lines, keyboardLines, SwipeHint(tutorial.SwipeHint));
    }

    private static string[] TutorialLines(string[]? lines, string key)
    {
        if (lines is null)
        {
            throw new JsonException($"The level's tutorial \"{key}\" is null.");
        }
        if (lines.Length is < 1 or > 2)
        {
            throw new JsonException($"The level's tutorial \"{key}\" has {lines.Length} lines; it needs one or two.");
        }
        if (lines.Any(string.IsNullOrWhiteSpace))
        {
            throw new JsonException($"The level's tutorial \"{key}\" has a null or blank line.");
        }
        return lines;
    }

    // Exact lowercase words, never numbers, as the save file's appearance words are
    private static Direction? SwipeHint(string? word) => word switch
    {
        null => null,
        "up" => Direction.Up,
        "down" => Direction.Down,
        "left" => Direction.Left,
        "right" => Direction.Right,
        _ => throw new JsonException($"The level's tutorial \"swipeHint\" is \"{word}\"; it must be up, down, left or right."),
    };

    private static void RequireValue(object? value, string key)
    {
        if (value is null)
        {
            throw new JsonException($"The level's \"{key}\" is null.");
        }
    }

    private static int[,] Grid(int[][] rows, string key)
    {
        var columns = rows.Length == 0 || rows[0] is null ? 0 : rows[0].Length;
        var grid = new int[rows.Length, columns];
        for (var row = 0; row < rows.Length; row++)
        {
            if (rows[row] is null)
            {
                throw new JsonException($"Row {row} of \"{key}\" is null.");
            }
            if (rows[row].Length != columns)
            {
                throw new JsonException($"Row {row} of \"{key}\" has {rows[row].Length} squares; row 0 has {columns}.");
            }
            for (var column = 0; column < columns; column++)
            {
                grid[row, column] = rows[row][column];
            }
        }
        return grid;
    }

    private static Tile[,] Tiles(int[,] codes)
    {
        var tiles = new Tile[codes.GetLength(0), codes.GetLength(1)];
        for (var row = 0; row < codes.GetLength(0); row++)
        {
            for (var column = 0; column < codes.GetLength(1); column++)
            {
                tiles[row, column] = codes[row, column] switch
                {
                    0 => Tile.None,
                    1 => Tile.Wall,
                    2 => Tile.Sticky,
                    3 => Tile.Trap,
                    var code => throw new JsonException($"The level's \"tiles\" has the unknown code {code} at [{row}, {column}]."),
                };
            }
        }
        return tiles;
    }

    private static Cell Coordinate(int[] pair, string name) =>
        pair.Length == 2
            ? new Cell(pair[0], pair[1])
            : throw new JsonException($"The level's \"{name}\" has {pair.Length} numbers; it needs two, [row, column].");
}

internal sealed record LevelJson
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    public required int Chapter { get; init; }

    public required int[][] Regions { get; init; }

    // Codes 0 (none), 1 (wall), 2 (sticky) and 3 (trap)
    public int[][]? Tiles { get; init; }

    // Pairs of [row, column] squares; their order gives the letters drawn on them
    public int[][][]? Teleporters { get; init; }

    public required int[] Start { get; init; }

    public required int[] End { get; init; }

    public required int Par { get; init; }

    public TutorialJson? Tutorial { get; init; }
}

internal sealed record TutorialJson
{
    public required string[] Lines { get; init; }

    public string[]? KeyboardLines { get; init; }

    public string? SwipeHint { get; init; }
}

// Source-generated, so parsing needs no reflection and survives trimming
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(LevelJson))]
internal partial class LevelJsonContext : JsonSerializerContext;

// A map's file keys as arrays: regions, tile codes or null, pairs or null, start and end
internal sealed record MapArrays(int[][] Regions, int[][]? Tiles, int[][][]? Teleporters, int[] Start, int[] End);
