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

    // Since the ID reset after chunk 34b (id-reset.csv beside the tool maps the old IDs), chapter n's levels take IDs
    // n × 1000 + 1 to n × 1000 + 999, so an ID tells which chapter a level was made for. A level that later moves
    // chapter keeps its ID
    public const int IdsPerChapter = 1000;

    // One past the highest ID the chapter's block has ever held, so a retired ID never comes back; null once the block is
    // used up
    public static int? NextId(int chapter, IEnumerable<int> levelIds, IEnumerable<int> retired)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(chapter, 1);
        var first = chapter * IdsPerChapter + 1;
        var last = first + IdsPerChapter - 2;
        var next = levelIds.Concat(retired).Where(id => id >= first && id <= last).DefaultIfEmpty(first - 1).Max() + 1;
        return next <= last ? next : null;
    }
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
