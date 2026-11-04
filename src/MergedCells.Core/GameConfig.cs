using System.Text.Json;
using System.Text.Json.Serialization;

namespace MergedCells.Core;

// Every tunable number in the game, read from config/game_config.json
public sealed record GameConfig
{
    private const char ByteOrderMark = (char)0xFEFF;

    public required InputConfig Input { get; init; }

    /// <summary>
    /// Parses the config file's text. Throws <see cref="JsonException"/>, naming the problem, when a group or value
    /// is missing, null or of the wrong type, when a key is unknown, or when the text isn't JSON.
    /// </summary>
    public static GameConfig Parse(string json)
    {
        // Some Windows editors save UTF-8 with a byte order mark, which System.Text.Json rejects
        var config = JsonSerializer.Deserialize(json.TrimStart(ByteOrderMark), GameConfigJsonContext.Default.GameConfig)
            ?? throw new JsonException("The config is null.");

        // .NET 8's System.Text.Json enforces required keys but not non-null values, so each group is checked here
        if (config.Input is null)
        {
            throw new JsonException("The config's \"input\" group is null.");
        }
        return config;
    }
}

public sealed record InputConfig
{
    // In viewport units (the 720-wide base)
    public required float SwipeThreshold { get; init; }
}

// Source-generated, so parsing needs no reflection and survives trimming
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(GameConfig))]
internal partial class GameConfigJsonContext : JsonSerializerContext;
