using System.Globalization;
using System.Text;

namespace MergedCells.Core;

/// <summary>One chapter as the chapter file holds it: its name, its level IDs in play order, and its intro lines.</summary>
public sealed record ChapterEntry(string Name, IReadOnlyList<int> LevelIds, IReadOnlyList<string> Intro)
{
    public static ChapterEntry From(Chapter chapter)
    {
        ArgumentNullException.ThrowIfNull(chapter);
        return new ChapterEntry(chapter.Name, chapter.Levels.Select(level => level.Id).ToArray(), chapter.Intro);
    }
}

/// <summary>Writes campaign/chapters.json; <see cref="Campaign.Parse"/> is its reader.</summary>
public static class ChapterFile
{
    /// <summary>
    /// The chapter file's text in the hand-written layout: one chapter per line, each <c>levels</c> array on that line,
    /// and <c>intro</c> only when the chapter has one. LF line endings and a final newline.
    /// </summary>
    public static string Write(IEnumerable<ChapterEntry> chapters)
    {
        ArgumentNullException.ThrowIfNull(chapters);
        var lines = chapters.Select(chapter =>
        {
            var line = new StringBuilder("    { \"name\": ").Append(JsonText.Quote(chapter.Name));
            line.Append(", \"levels\": [")
                .Append(string.Join(", ", chapter.LevelIds.Select(id => id.ToString(CultureInfo.InvariantCulture))))
                .Append(']');
            if (chapter.Intro.Count > 0)
            {
                line.Append(", \"intro\": [").Append(string.Join(", ", chapter.Intro.Select(JsonText.Quote))).Append(']');
            }
            return line.Append(" }").ToString();
        });
        return "{\n  \"chapters\": [\n" + string.Join(",\n", lines) + "\n  ]\n}\n";
    }
}
