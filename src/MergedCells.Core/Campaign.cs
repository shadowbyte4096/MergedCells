using System.Text.Json;
using System.Text.Json.Serialization;

namespace MergedCells.Core;

/// <summary>The campaign's chapters, read from campaign/chapters.json and checked against the level files.</summary>
public sealed class Campaign
{
    private const char ByteOrderMark = (char)0xFEFF;

    private readonly Dictionary<int, (Chapter Chapter, int Number)> placeById;

    private Campaign(IReadOnlyList<Chapter> chapters)
    {
        Chapters = chapters;
        Levels = chapters.SelectMany(chapter => chapter.Levels).ToArray();
        placeById = new Dictionary<int, (Chapter, int)>();
        foreach (var chapter in chapters)
        {
            for (var i = 0; i < chapter.Levels.Count; i++)
            {
                placeById[chapter.Levels[i].Id] = (chapter, i + 1);
            }
        }
    }

    public IReadOnlyList<Chapter> Chapters { get; }

    // Every level, in play order across the chapters
    public IReadOnlyList<Level> Levels { get; }

    public Chapter ChapterOf(Level level) => Place(level).Chapter;

    // From 1, within the level's chapter
    public int NumberInChapter(Level level) => Place(level).Number;

    /// <summary>
    /// Reads the chapter file's text against the level files. Throws <see cref="JsonException"/>, naming the problem,
    /// when a value is missing, null or blank, when a key is unknown, when a chapter has no levels, when an ID is listed
    /// twice or has no level file, when a level file is in no chapter or its "chapter" isn't the chapter that lists it,
    /// or when the text isn't JSON.
    /// </summary>
    public static Campaign Parse(string json, IReadOnlyList<Level> levels)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(levels);
        // Some Windows editors save UTF-8 with a byte order mark, which System.Text.Json rejects
        var file = JsonSerializer.Deserialize(json.TrimStart(ByteOrderMark), CampaignJsonContext.Default.CampaignJson)
            ?? throw new JsonException("The chapter file is null.");
        // .NET 8's System.Text.Json enforces required keys but not non-null values, so each is checked by name
        if (file.Chapters is null)
        {
            throw new JsonException("The chapter file's \"chapters\" is null.");
        }
        if (file.Chapters.Count == 0)
        {
            throw new JsonException("The chapter file has no chapters.");
        }

        var byId = levels.ToDictionary(level => level.Id);
        var listed = new HashSet<int>();
        var chapters = new List<Chapter>();
        for (var index = 0; index < file.Chapters.Count; index++)
        {
            var number = index + 1;
            var entry = file.Chapters[index] ?? throw new JsonException($"Chapter {number} is null.");
            if (entry.Name is null)
            {
                throw new JsonException($"Chapter {number}'s \"name\" is null.");
            }
            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                throw new JsonException($"Chapter {number}'s \"name\" is blank.");
            }
            if (entry.Levels is null)
            {
                throw new JsonException($"Chapter {number}'s \"levels\" is null.");
            }
            if (entry.Levels.Length == 0)
            {
                throw new JsonException($"Chapter {number} has no levels.");
            }

            var chapterLevels = new List<Level>();
            foreach (var id in entry.Levels)
            {
                if (!listed.Add(id))
                {
                    throw new JsonException($"Level {id} is listed more than once.");
                }
                if (!byId.TryGetValue(id, out var level))
                {
                    throw new JsonException($"Chapter {number} lists level {id}, which has no level file.");
                }
                if (level.Chapter != number)
                {
                    throw new JsonException($"Level {id}'s file says chapter {level.Chapter}, but chapter {number} lists it.");
                }
                chapterLevels.Add(level);
            }
            chapters.Add(new Chapter(number, entry.Name, chapterLevels));
        }

        if (levels.FirstOrDefault(level => !listed.Contains(level.Id)) is { } missing)
        {
            throw new JsonException($"Level {missing.Id} isn't in any chapter.");
        }
        return new Campaign(chapters);
    }

    private (Chapter Chapter, int Number) Place(Level level)
    {
        ArgumentNullException.ThrowIfNull(level);
        return placeById.TryGetValue(level.Id, out var place)
            ? place
            : throw new ArgumentException($"Level {level.Id} isn't in the campaign.", nameof(level));
    }
}

internal sealed record CampaignJson
{
    public required IReadOnlyList<ChapterJson> Chapters { get; init; }
}

internal sealed record ChapterJson
{
    public required string Name { get; init; }

    public required int[] Levels { get; init; }
}

// Source-generated, so parsing needs no reflection and survives trimming
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(CampaignJson))]
internal partial class CampaignJsonContext : JsonSerializerContext;
