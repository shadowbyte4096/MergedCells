using System.Text.Json;
using System.Text.Json.Serialization;

namespace MergedCells.Core;

// The app icon's shapes, read from icons/icon-art.json, in icon units: the icon's visible area is 0 to 100 across.
// The icon's SVG files are drawn by hand from the same numbers, and the intro draws its opening from them. Outlines
// are centred on the shape's edge, as in SVG.
public sealed record IconArt
{
    private const char ByteOrderMark = (char)0xFEFF;

    public required ArtBlock LeftBlock { get; init; }

    public required ArtBlock RightBlock { get; init; }

    public required ArtPlayer Player { get; init; }

    /// <summary>
    /// Parses the icon art file's text. Throws <see cref="JsonException"/>, naming the problem, when a group or value
    /// is missing, null or of the wrong type, when a key is unknown or repeated, or when the text isn't JSON.
    /// </summary>
    public static IconArt Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        // Some Windows editors save UTF-8 with a byte order mark, which System.Text.Json rejects
        var text = json.TrimStart(ByteOrderMark);
        var art = JsonSerializer.Deserialize(text, IconArtJsonContext.Default.IconArt)
            ?? throw new JsonException("The icon art is null.");
        JsonKeys.RejectDuplicates(text, "icon art");
        // .NET 8's System.Text.Json enforces required keys but not non-null values, so each group is checked here
        if (art.LeftBlock is null)
        {
            throw new JsonException("The icon art's \"leftBlock\" is null.");
        }
        if (art.RightBlock is null)
        {
            throw new JsonException("The icon art's \"rightBlock\" is null.");
        }
        if (art.Player is null)
        {
            throw new JsonException("The icon art's \"player\" is null.");
        }
        return art;
    }
}

// A rounded rectangle with an outline; Fill indexes the dark board theme's fills
public sealed record ArtBlock
{
    public required double X { get; init; }

    public required double Y { get; init; }

    public required double Width { get; init; }

    public required double Height { get; init; }

    public required double Corner { get; init; }

    public required double Outline { get; init; }

    public required int Fill { get; init; }
}

// The hollow player square and its trail, which runs from TrailStart to the player as one shape
public sealed record ArtPlayer
{
    public required double X { get; init; }

    public required double Y { get; init; }

    public required double Side { get; init; }

    public required double Outline { get; init; }

    public required double Corner { get; init; }

    public required double TrailStart { get; init; }

    // The trail is at full strength from the player's outer left edge on
    public double FadeEnd => X - Outline / 2;
}

// Source-generated, so parsing needs no reflection and survives trimming
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(IconArt))]
internal partial class IconArtJsonContext : JsonSerializerContext;
