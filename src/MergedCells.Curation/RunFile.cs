using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using MergedCells.Core;

namespace MergedCells.Curation;

// run.json beside a batch of candidates: what made them, so promote can set star limits from the band without asking
internal sealed record RunFile(int Chapter, string Band, ulong Seed, int? OneStarExtraMoves)
{
    private const char ByteOrderMark = (char)0xFEFF;

    public static RunFile Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var text = json.TrimStart(ByteOrderMark);
        var file = JsonSerializer.Deserialize(text, RunJsonContext.Default.RunFileJson)
            ?? throw new JsonException("The run file is null.");
        JsonKeys.RejectDuplicates(text, "run file");
        var band = file.Band ?? throw new JsonException("The run file's \"band\" is null.");
        if (file.OneStarExtraMoves is < 0)
        {
            throw new JsonException("The run file's \"oneStarExtraMoves\" must be null or at least 0.");
        }
        return new RunFile(file.Chapter, band, file.Seed, file.OneStarExtraMoves);
    }

    public string Write() =>
        "{\n"
        + $"  \"chapter\": {Chapter.ToString(CultureInfo.InvariantCulture)},\n"
        + $"  \"band\": {JsonText.Quote(Band)},\n"
        + $"  \"seed\": {Seed.ToString(CultureInfo.InvariantCulture)},\n"
        + $"  \"oneStarExtraMoves\": {OneStarExtraMoves?.ToString(CultureInfo.InvariantCulture) ?? "null"}\n"
        + "}\n";
}

internal sealed record RunFileJson
{
    public required int Chapter { get; init; }

    public required string Band { get; init; }

    public required ulong Seed { get; init; }

    public required int? OneStarExtraMoves { get; init; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(RunFileJson))]
internal partial class RunJsonContext : JsonSerializerContext;
