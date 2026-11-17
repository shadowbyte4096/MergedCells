using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MergedCells.Core;

/// <summary>
/// Reads and writes one custom level's file: a version, the level's GUID, name, last edit and import time (null for the
/// player's own; version 1 files have none), then the map keys in the level file's layout. No par, chapter or tutorial.
/// </summary>
public static class CustomLevelFile
{
    public const int CurrentVersion = 2;

    private const char ByteOrderMark = (char)0xFEFF;

    private const string DateFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    /// <summary>
    /// Parses a custom level file's text. Throws <see cref="NewerCustomLevelException"/> when its version is newer than this
    /// build's, whatever else it holds; <see cref="JsonException"/>, naming the problem, when a value is missing, null, of
    /// the wrong type or out of range, when a key is unknown or repeated, or when the text isn't JSON; and
    /// <see cref="InvalidMapException"/> when the map breaks a map rule.
    /// </summary>
    public static CustomLevel Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var text = json.TrimStart(ByteOrderMark);
        var version = CheckVersion(text);
        var file = JsonSerializer.Deserialize(text, CustomLevelJsonContext.Default.CustomLevelJson)
            ?? throw new JsonException("The custom level is null.");
        JsonKeys.RejectDuplicates(text, "custom level");

        RequireValue(file.Id, "id");
        RequireValue(file.Name, "name");
        RequireValue(file.Edited, "edited");
        RequireValue(file.Regions, "regions");
        RequireValue(file.Start, "start");
        RequireValue(file.End, "end");
        if (version.HasImported != (version.Number >= 2))
        {
            throw new JsonException(version.Number >= 2
                ? "The custom level has no \"imported\"."
                : $"The custom level is version {version.Number}, which has no \"imported\".");
        }
        // Lowercase only, so one level always has one file name
        if (!Guid.TryParseExact(file.Id, "D", out var id) || id == Guid.Empty || file.Id != id.ToString("D"))
        {
            throw new JsonException($"The custom level's \"id\" \"{file.Id}\" isn't a lowercase GUID, or is all zeros.");
        }
        if (CustomLevelNames.Clean(file.Name, int.MaxValue) != file.Name)
        {
            throw new JsonException("The custom level's \"name\" is blank, or has spaces round it or runs of whitespace in it.");
        }
        var edited = ReadTime(file.Edited, "edited");
        DateTimeOffset? imported = file.Imported is null ? null : ReadTime(file.Imported, "imported");

        var map = LevelFile.ReadMap(file.Regions, file.Tiles, file.Teleporters, file.Start, file.End);
        for (var row = 0; row < map.Rows; row++)
        {
            for (var column = 0; column < map.Columns; column++)
            {
                // Sticky squares were dropped from the game; the editor can't make one
                if (map.TileAt(new Cell(row, column)) == Tile.Sticky)
                {
                    throw new JsonException($"The custom level has a sticky square at [{row}, {column}].");
                }
            }
        }
        return new CustomLevel(id, file.Name, map, edited, imported);
    }

    private static DateTimeOffset ReadTime(string value, string key) =>
        DateTimeOffset.TryParseExact(
            value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var time)
            ? time
            : throw new JsonException($"The custom level's \"{key}\" \"{value}\" isn't a UTC time written as {DateFormat}.");

    private static string WriteTime(DateTimeOffset time) =>
        JsonText.Quote(time.UtcDateTime.ToString(DateFormat, CultureInfo.InvariantCulture));

    /// <summary>The level's file text, in the level file's layout, with LF line endings and a final newline.</summary>
    public static string Write(CustomLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);
        var text = new StringBuilder("{\n");
        text.Append("  \"version\": ").Append(CurrentVersion.ToString(CultureInfo.InvariantCulture)).Append(",\n");
        text.Append("  \"id\": ").Append(JsonText.Quote(level.Id.ToString("D"))).Append(",\n");
        text.Append("  \"name\": ").Append(JsonText.Quote(level.Name)).Append(",\n");
        text.Append("  \"edited\": ").Append(WriteTime(level.Edited)).Append(",\n");
        text.Append("  \"imported\": ").Append(level.Imported is { } imported ? WriteTime(imported) : "null").Append(",\n");
        LevelFile.AppendMap(text, level.Map);
        return text.Append("\n}\n").ToString();
    }

    // The version decides how the rest is read, so a newer file with keys this build doesn't know is never called broken.
    // "imported" is checked here because the strict reader can't tell a missing key from a null one
    private static (int Number, bool HasImported) CheckVersion(string text)
    {
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("The custom level isn't a JSON object.");
        }
        if (!root.TryGetProperty("version", out var version))
        {
            throw new JsonException("The custom level has no \"version\".");
        }
        if (version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number))
        {
            throw new JsonException("The custom level's \"version\" isn't a whole number.");
        }
        if (number > CurrentVersion)
        {
            throw new NewerCustomLevelException(number);
        }
        if (number < 1)
        {
            throw new JsonException($"The custom level's \"version\" is {number}; it must be at least 1.");
        }
        return (number, root.TryGetProperty("imported", out _));
    }

    private static void RequireValue(object? value, string key)
    {
        if (value is null)
        {
            throw new JsonException($"The custom level's \"{key}\" is null.");
        }
    }
}

internal sealed record CustomLevelJson
{
    public required int Version { get; init; }

    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Edited { get; init; }

    public string? Imported { get; init; }

    public required int[][] Regions { get; init; }

    public int[][]? Tiles { get; init; }

    public int[][][]? Teleporters { get; init; }

    public required int[] Start { get; init; }

    public required int[] End { get; init; }
}

// Source-generated, so parsing needs no reflection and survives trimming
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(CustomLevelJson))]
internal partial class CustomLevelJsonContext : JsonSerializerContext;
