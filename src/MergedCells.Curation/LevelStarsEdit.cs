using System.Globalization;
using System.Text.RegularExpressions;
using MergedCells.Core;

namespace MergedCells.Curation;

// Adds or removes one level's line in game_config.json's "levelStars" object. The file is laid out by hand, so it's
// edited as lines and never written out again whole; the commands parse the result before anything is saved
internal static class LevelStarsEdit
{
    private static readonly Regex Entry = new("^(\\s*)\"(\\d+)\": \\{.*\\}(,?)$", RegexOptions.CultureInvariant);

    public static string Add(string configText, int id, StarsConfig stars)
    {
        var block = Block.Find(configText);
        if (block.Entries.Any(entry => entry.Id == id))
        {
            throw new InvalidOperationException($"The config's \"levelStars\" already has an entry for level {id}.");
        }
        if (block.Inline)
        {
            var indent = block.Indent + "  ";
            block.Lines[block.Open] = $"{block.Indent}\"levelStars\": {{";
            block.Lines.Insert(block.Open + 1, Line(indent, id, stars));
            block.Lines.Insert(block.Open + 2, $"{block.Indent}}}{block.Comma}");
            return block.Text();
        }
        var last = block.Entries.Count > 0 ? block.Entries[^1] : null;
        if (last is not null && last.Comma.Length == 0)
        {
            block.Lines[last.Line] += ",";
        }
        block.Lines.Insert(block.Close, Line(last?.Indent ?? block.Indent + "  ", id, stars));
        return block.Text();
    }

    // Leaves the text as it is when the level has no entry
    public static string Remove(string configText, int id)
    {
        var block = Block.Find(configText);
        var index = block.Entries.FindIndex(entry => entry.Id == id);
        if (index < 0)
        {
            return configText;
        }
        if (block.Entries.Count == 1)
        {
            var comma = block.Lines[block.Close].Trim().EndsWith(',') ? "," : "";
            block.Lines.RemoveRange(block.Open, block.Close - block.Open + 1);
            block.Lines.Insert(block.Open, $"{block.Indent}\"levelStars\": {{}}{comma}");
            return block.Text();
        }
        if (index == block.Entries.Count - 1)
        {
            var before = block.Entries[index - 1];
            block.Lines[before.Line] = block.Lines[before.Line].TrimEnd(',');
        }
        block.Lines.RemoveAt(block.Entries[index].Line);
        return block.Text();
    }

    private static string Line(string indent, int id, StarsConfig stars) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{indent}\"{id}\": {{ \"twoStarMinExtraMoves\": {stars.TwoStarMinExtraMoves}, \"twoStarExtraParPercent\": {stars.TwoStarExtraParPercent}, \"oneStarMinExtraMoves\": {stars.OneStarMinExtraMoves}, \"oneStarExtraParPercent\": {stars.OneStarExtraParPercent} }}");

    private sealed record EntryLine(int Line, int Id, string Indent, string Comma);

    // The text as lines (kept with its own line ending), and where the levelStars object opens and closes in it
    private sealed class Block
    {
        private string newline = "\n";

        public List<string> Lines { get; private init; } = [];

        public int Open { get; private init; }

        // The closing line; unused when Inline
        public int Close { get; private init; }

        // "levelStars": {} on one line
        public bool Inline { get; private init; }

        public string Indent { get; private init; } = "";

        // After an inline {}
        public string Comma { get; private init; } = "";

        public List<EntryLine> Entries { get; } = [];

        public string Text() => string.Join(newline, Lines);

        public static Block Find(string text)
        {
            var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var lines = text.Split(newline).ToList();
            var open = lines.FindIndex(line => line.TrimStart().StartsWith("\"levelStars\": {", StringComparison.Ordinal));
            if (open < 0)
            {
                throw new InvalidOperationException(
                    "Can't find the line \"levelStars\": { in config/game_config.json. Put the key and its { on one line, as the shipped file has it.");
            }
            var indent = lines[open][..(lines[open].Length - lines[open].TrimStart().Length)];
            var opening = lines[open].Trim();
            if (opening is "\"levelStars\": {}" or "\"levelStars\": {},")
            {
                return new Block { newline = newline, Lines = lines, Open = open, Inline = true, Indent = indent, Comma = opening.EndsWith(',') ? "," : "" };
            }
            if (opening != "\"levelStars\": {")
            {
                throw new InvalidOperationException(
                    $"Line {open + 1} of config/game_config.json opens \"levelStars\" with more after the {{. Put each entry on its own line, as the shipped file has them.");
            }
            var close = lines.FindIndex(open + 1, line => line.TrimStart().StartsWith('}'));
            if (close < 0)
            {
                throw new InvalidOperationException("The \"levelStars\" object in config/game_config.json never closes.");
            }
            var block = new Block { newline = newline, Lines = lines, Open = open, Close = close, Indent = indent };
            for (var line = open + 1; line < close; line++)
            {
                var match = Entry.Match(lines[line]);
                if (!match.Success)
                {
                    throw new InvalidOperationException(
                        $"Line {line + 1} of config/game_config.json isn't a whole \"levelStars\" entry. Put each entry on one line, as the shipped file has them, and run the command again.");
                }
                block.Entries.Add(new EntryLine(line, int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture), match.Groups[1].Value, match.Groups[3].Value));
            }
            return block;
        }
    }
}
