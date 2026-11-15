using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using MergedCells.Core;

namespace MergedCells.Curation;

// The ID file (retired-levels.json beside the tool): every level ID taken out of the campaign. Saved bests and
// leaderboards key off IDs, so none is ever used again. Never shipped or read by the game
internal static class RetiredLevels
{
    private const char ByteOrderMark = (char)0xFEFF;

    public static IReadOnlyList<int> Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var text = json.TrimStart(ByteOrderMark);
        var file = JsonSerializer.Deserialize(text, RetiredJsonContext.Default.RetiredFileJson)
            ?? throw new JsonException("The ID file is null.");
        JsonKeys.RejectDuplicates(text, "ID file");
        var retired = file.Retired ?? throw new JsonException("The ID file's \"retired\" is null.");
        foreach (var id in retired)
        {
            if (id < 1)
            {
                throw new JsonException($"The ID file retires {id}; level IDs start at 1.");
            }
        }
        return retired;
    }

    public static string Write(IEnumerable<int> ids) =>
        "{\n  \"retired\": [" + string.Join(", ", ids.Order().Select(id => id.ToString(CultureInfo.InvariantCulture))) + "]\n}\n";

    // New levels always get higher IDs, as the chapter order already assumes
    public static int NextId(IEnumerable<int> levelIds, IEnumerable<int> retired) => levelIds.Concat(retired).DefaultIfEmpty(0).Max() + 1;
}

internal sealed record RetiredFileJson
{
    public required int[] Retired { get; init; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(RetiredFileJson))]
internal partial class RetiredJsonContext : JsonSerializerContext;
