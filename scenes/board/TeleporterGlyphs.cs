using System.Collections.Generic;
using Godot;

namespace MergedCells;

// Draws teleporter letters into a block's canvas item. The letters use a distance-field copy of the theme's font, which
// stays sharp at any zoom, so zooming never rebuilds blocks; the UI's own import of the font is left as it is
internal sealed class TeleporterGlyphs
{
    private readonly Font outlines;
    private readonly FontFile letters;
    private readonly Color color;
    private readonly int outlinedSize;
    private readonly int flatSize;
    // From a letter's ink centre to the left end of its baseline, by letter and size
    private readonly Dictionary<(string Letter, int Size), Vector2> offsets = [];

    public TeleporterGlyphs(BoardTheme theme)
    {
        outlines = theme.TeleporterFont;
        letters = (FontFile)theme.TeleporterFont.Duplicate();
        letters.MultichannelSignedDistanceField = true;
        color = theme.Teleporter;
        outlinedSize = Mathf.RoundToInt(theme.TeleporterLetterFraction * BoardView.CellSize);
        flatSize = Mathf.RoundToInt(theme.TeleporterFlatLetterFraction * BoardView.CellSize);
    }

    public void Draw(Rid item, Vector2 centre, string letter, bool outlined)
    {
        var size = outlined ? outlinedSize : flatSize;
        letters.DrawString(item, centre + Offset(letter, size), letter, HorizontalAlignment.Left, -1, size, color);
    }

    // Measured on the original font's outline, which the copy shares, at the size drawn
    private Vector2 Offset(string letter, int size)
    {
        if (offsets.TryGetValue((letter, size), out var offset))
        {
            return offset;
        }
        var server = TextServerManager.GetPrimaryInterface();
        var font = outlines.GetRids()[0];
        var glyph = server.FontGetGlyphIndex(font, size, letter[0], 0);
        var points = server.FontGetGlyphContours(font, size, glyph)["points"].AsVector3Array();
        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        foreach (var point in points)
        {
            min = min.Min(new Vector2(point.X, point.Y));
            max = max.Max(new Vector2(point.X, point.Y));
        }
        offset = -(min + max) / 2;
        offsets[(letter, size)] = offset;
        return offset;
    }
}
