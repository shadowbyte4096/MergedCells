using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MergedCells.Core;

public static partial class CustomLevelNames
{
    public const string Untitled = "Untitled";

    // A format bound, not a tunable number: every name a level keeps must fit a share code. 16 of the longest emoji
    // sequences in use (a family, a subdivision flag) take 25 to 28 bytes each
    public const int MaxBytes = 512;

    /// <summary>
    /// The name as it's kept: trimmed, with each run of whitespace made one space. Null when that's blank, longer than
    /// <paramref name="maxLength"/> text elements (an accented letter or an emoji counts as one), over
    /// <see cref="MaxBytes"/> bytes of UTF-8, or has a hidden character (see <see cref="HasHiddenCharacters"/>).
    /// </summary>
    public static string? Clean(string text, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(text);
        var clean = Whitespace().Replace(text.Trim(), " ");
        return clean.Length == 0
            || new StringInfo(clean).LengthInTextElements > maxLength
            || Encoding.UTF8.GetByteCount(clean) > MaxBytes
            || HasHiddenCharacters(clean)
            ? null
            : clean;
    }

    /// <summary>
    /// Whether the text holds a control character other than whitespace (which <see cref="Clean"/> turns into spaces), or
    /// one that overrides or isolates the direction of text around it.
    /// Names travel to other players inside share codes, so none may hide or rearrange what's shown. Zero-width joiners,
    /// which join emoji, are allowed.
    /// </summary>
    public static bool HasHiddenCharacters(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        foreach (var character in text)
        {
            if ((char.IsControl(character) && !char.IsWhiteSpace(character)) || character is >= '‪' and <= '‮' or >= '⁦' and <= '⁩')
            {
                return true;
            }
        }
        return false;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
