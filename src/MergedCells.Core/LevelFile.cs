using System.Text.Json;
using System.Text.Json.Serialization;

namespace MergedCells.Core;

// Reads one level file in section 7's format
public static class LevelFile
{
    private const char ByteOrderMark = (char)0xFEFF;

    /// <summary>
    /// Parses a level file's text. Throws <see cref="JsonException"/>, naming the problem, when a value is missing, null,
    /// of the wrong type or out of range, when a key is unknown, when the grid's rows differ in length, when twist tiles or
    /// teleporters are given (not supported yet), or when the text isn't JSON. Throws <see cref="InvalidMapException"/>
    /// when the map breaks a map rule.
    /// </summary>
    public static Level Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        // Some Windows editors save UTF-8 with a byte order mark, which System.Text.Json rejects
        var file = JsonSerializer.Deserialize(json.TrimStart(ByteOrderMark), LevelJsonContext.Default.LevelJson)
            ?? throw new JsonException("The level is null.");

        // .NET 8's System.Text.Json enforces required keys but not non-null values, so each is checked by name
        RequireValue(file.Name, "name");
        RequireValue(file.Regions, "regions");
        RequireValue(file.Start, "start");
        RequireValue(file.End, "end");
        if (file.Tiles is not null)
        {
            throw new JsonException("The level's \"tiles\" must be null: twists aren't supported yet.");
        }
        if (file.Teleporters is not null)
        {
            throw new JsonException("The level's \"teleporters\" must be null: twists aren't supported yet.");
        }
        if (file.Id < 1 || file.Chapter < 1 || file.Par < 1)
        {
            throw new JsonException($"The level's \"id\" ({file.Id}), \"chapter\" ({file.Chapter}) and \"par\" ({file.Par}) must be at least 1.");
        }
        if (string.IsNullOrWhiteSpace(file.Name))
        {
            throw new JsonException("The level's \"name\" is blank.");
        }

        var map = Map.Create(Grid(file.Regions), Coordinate(file.Start, "start"), Coordinate(file.End, "end"));
        return new Level(file.Id, file.Name, file.Chapter, map, file.Par);
    }

    private static void RequireValue(object? value, string key)
    {
        if (value is null)
        {
            throw new JsonException($"The level's \"{key}\" is null.");
        }
    }

    private static int[,] Grid(int[][] rows)
    {
        var columns = rows.Length == 0 || rows[0] is null ? 0 : rows[0].Length;
        var grid = new int[rows.Length, columns];
        for (var row = 0; row < rows.Length; row++)
        {
            if (rows[row] is null)
            {
                throw new JsonException($"Row {row} of \"regions\" is null.");
            }
            if (rows[row].Length != columns)
            {
                throw new JsonException($"Row {row} of \"regions\" has {rows[row].Length} squares; row 0 has {columns}.");
            }
            for (var column = 0; column < columns; column++)
            {
                grid[row, column] = rows[row][column];
            }
        }
        return grid;
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

    // Chunks 13 and 17 read these; until then a value is rejected rather than silently ignored
    public int[][]? Tiles { get; init; }

    public int[][][]? Teleporters { get; init; }

    public required int[] Start { get; init; }

    public required int[] End { get; init; }

    public required int Par { get; init; }
}

// Source-generated, so parsing needs no reflection and survives trimming
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(LevelJson))]
internal partial class LevelJsonContext : JsonSerializerContext;
