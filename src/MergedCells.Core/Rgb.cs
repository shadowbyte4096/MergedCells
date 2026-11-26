using System.Globalization;

namespace MergedCells.Core;

/// <summary>An opaque colour as the cosmetics file writes it, <c>#rrggbb</c> in lowercase, with no Godot types.</summary>
public readonly record struct Rgb(byte R, byte G, byte B)
{
    public static Rgb Parse(string text) =>
        TryParse(text, out var colour) ? colour : throw new FormatException($"\"{text}\" isn't a colour written as #rrggbb in lowercase.");

    public static bool TryParse(string? text, out Rgb colour)
    {
        colour = default;
        if (text is not { Length: 7 } || text[0] != '#')
        {
            return false;
        }
        for (var i = 1; i < 7; i++)
        {
            if (!char.IsAsciiHexDigitLower(text[i]) && !char.IsAsciiDigit(text[i]))
            {
                return false;
            }
        }
        colour = new Rgb(Byte(text, 1), Byte(text, 3), Byte(text, 5));
        return true;
    }

    public override string ToString() => $"#{R:x2}{G:x2}{B:x2}";

    private static byte Byte(string text, int start) => byte.Parse(text.AsSpan(start, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
}
