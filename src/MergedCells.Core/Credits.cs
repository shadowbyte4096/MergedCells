using System.Text.Json;
using System.Text.Json.Serialization;

namespace MergedCells.Core;

public sealed record CreditEntry(string Role, IReadOnlyList<string> Names);

// Reads credits/credits.json: the people the credits screen names, in order
public static class Credits
{
    private const char ByteOrderMark = (char)0xFEFF;

    /// <summary>
    /// Parses the credits file's text. Throws <see cref="JsonException"/>, naming the problem, when there are no entries,
    /// when a role or name is missing, null or blank, when an entry has no names, when a key is unknown, or when the text
    /// isn't JSON.
    /// </summary>
    public static IReadOnlyList<CreditEntry> Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        // Some Windows editors save UTF-8 with a byte order mark, which System.Text.Json rejects
        var file = JsonSerializer.Deserialize(json.TrimStart(ByteOrderMark), CreditsJsonContext.Default.CreditsJson)
            ?? throw new JsonException("The credits file is null.");
        // .NET 8's System.Text.Json enforces required keys but not non-null values, so each is checked by name
        if (file.Credits is null)
        {
            throw new JsonException("The credits file's \"credits\" is null.");
        }
        if (file.Credits.Count == 0)
        {
            throw new JsonException("The credits file has no entries.");
        }

        var entries = new List<CreditEntry>();
        for (var index = 0; index < file.Credits.Count; index++)
        {
            var number = index + 1;
            var entry = file.Credits[index] ?? throw new JsonException($"Credits entry {number} is null.");
            if (string.IsNullOrWhiteSpace(entry.Role))
            {
                throw new JsonException($"Credits entry {number}'s \"role\" is null or blank.");
            }
            if (entry.Names is null || entry.Names.Length == 0)
            {
                throw new JsonException($"Credits entry {number} (\"{entry.Role}\") has no names.");
            }
            if (entry.Names.Any(string.IsNullOrWhiteSpace))
            {
                throw new JsonException($"Credits entry {number} (\"{entry.Role}\") has a null or blank name.");
            }
            entries.Add(new CreditEntry(entry.Role, entry.Names));
        }
        return entries;
    }
}

internal sealed record CreditsJson
{
    public required IReadOnlyList<CreditEntryJson> Credits { get; init; }
}

internal sealed record CreditEntryJson
{
    public required string Role { get; init; }

    public required string[] Names { get; init; }
}

// Source-generated, so parsing needs no reflection and survives trimming
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(CreditsJson))]
internal partial class CreditsJsonContext : JsonSerializerContext;
