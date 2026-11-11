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
    /// unknown, when teleporters are given (not supported yet), when a tutorial has no lines, more than
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
        if (file.Teleporters is not null)
        {
            throw new JsonException("The level's \"teleporters\" must be null: teleporters aren't supported yet.");
        }
        if (file.Id < 1 || file.Chapter < 1 || file.Par < 1)
        {
            throw new JsonException($"The level's \"id\" ({file.Id}), \"chapter\" ({file.Chapter}) and \"par\" ({file.Par}) must be at least 1.");
        }
        if (string.IsNullOrWhiteSpace(file.Name))
        {
            throw new JsonException("The level's \"name\" is blank.");
        }

        var map = Map.Create(
            Grid(file.Regions, "regions"),
            Coordinate(file.Start, "start"),
            Coordinate(file.End, "end"),
            file.Tiles is null ? null : Tiles(Grid(file.Tiles, "tiles")));
        return new Level(file.Id, file.Name, file.Chapter, map, file.Par, ReadTutorial(file.Tutorial));
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

    // Chunk 17 reads these; until then a value is rejected rather than silently ignored
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
