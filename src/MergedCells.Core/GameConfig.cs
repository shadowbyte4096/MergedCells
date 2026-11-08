using System.Text.Json;
using System.Text.Json.Serialization;

namespace MergedCells.Core;

// Every tunable number in the game, read from config/game_config.json
public sealed record GameConfig
{
    private const char ByteOrderMark = (char)0xFEFF;

    public required InputConfig Input { get; init; }

    public required SlideConfig Slide { get; init; }

    public required StarsConfig Stars { get; init; }

    public required CameraConfig Camera { get; init; }

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
        if (config.Slide is null)
        {
            throw new JsonException("The config's \"slide\" group is null.");
        }
        if (config.Stars is null)
        {
            throw new JsonException("The config's \"stars\" group is null.");
        }
        if (config.Camera is null)
        {
            throw new JsonException("The config's \"camera\" group is null.");
        }
        return config;
    }
}

public sealed record InputConfig
{
    // In viewport units (the 720-wide base)
    public required float SwipeThreshold { get; init; }
}

public sealed record SlideConfig
{
    // Squares a second, at a constant speed
    public required double SquaresPerSecond { get; init; }

    // In seconds: no slide takes longer, however far it goes
    public required double MaxSeconds { get; init; }
}

// Each star limit is par plus the larger of its minimum and its percentage of par, rounded up
public sealed record StarsConfig
{
    public required int TwoStarMinExtraMoves { get; init; }

    public required int TwoStarExtraParPercent { get; init; }

    public required int OneStarMinExtraMoves { get; init; }

    public required int OneStarExtraParPercent { get; init; }
}

public sealed record CameraConfig
{
    // In viewport units. A level whose whole map would show smaller squares opens zoomed in to this size
    public required float ReadableSquareSize { get; init; }

    // In viewport units: the closest zoom
    public required float ClosestSquareSize { get; init; }

    // The middle part of the screen, on each axis, inside which a stop doesn't move the camera
    public required float FollowZonePercent { get; init; }

    public required double GlideSeconds { get; init; }

    public required double OpeningSeconds { get; init; }

    // On the whole map, before the opening zoom starts
    public required double OpeningHoldSeconds { get; init; }

    // The zoom factor for one mouse-wheel notch or zoom-key press
    public required float ZoomStep { get; init; }

    // In viewport units: how far a two-finger gesture or a mouse drag must move before it moves the view
    public required float GestureSlop { get; init; }
}

// Source-generated, so parsing needs no reflection and survives trimming
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(GameConfig))]
internal partial class GameConfigJsonContext : JsonSerializerContext;
