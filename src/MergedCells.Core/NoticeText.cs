namespace MergedCells.Core;

// One part of a third-party component in Godot's copyright info: its copyright lines and the licence they're under
public sealed record ComponentPart(IReadOnlyList<string> Copyright, string Licence);

// Shapes licence notices for the credits screen, which shows each paragraph as its own label so off-screen ones are culled
public static class NoticeText
{
    // Blank lines (whitespace only) separate paragraphs; line breaks and indentation inside one are kept
    public static IReadOnlyList<string> Paragraphs(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var paragraphs = new List<string>();
        var current = new List<string>();
        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                Flush();
            }
            else
            {
                current.Add(line.TrimEnd());
            }
        }
        Flush();
        return paragraphs;

        void Flush()
        {
            if (current.Count > 0)
            {
                paragraphs.Add(string.Join("\n", current));
                current.Clear();
            }
        }
    }

    public static string Component(string name, IReadOnlyList<ComponentPart> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        var lines = new List<string> { name };
        foreach (var part in parts)
        {
            lines.AddRange(part.Copyright.Select(copyright => $"Copyright {copyright}"));
            lines.Add($"Licence: {part.Licence}");
        }
        return string.Join("\n", lines);
    }
}
