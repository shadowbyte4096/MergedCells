using System.Globalization;
using System.Text;

namespace MergedCells.Core;

// What the hand-formatted writers share: a JSON string literal that keeps non-ASCII text such as → as itself, as the
// hand-written files hold it, where System.Text.Json's default encoder would escape it
internal static class JsonText
{
    public static string Quote(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var text = new StringBuilder(value.Length + 2).Append('"');
        foreach (var character in value)
        {
            switch (character)
            {
                case '"':
                    text.Append("\\\"");
                    break;
                case '\\':
                    text.Append("\\\\");
                    break;
                case '\n':
                    text.Append("\\n");
                    break;
                case '\r':
                    text.Append("\\r");
                    break;
                case '\t':
                    text.Append("\\t");
                    break;
                case < ' ':
                    text.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    break;
                default:
                    text.Append(character);
                    break;
            }
        }
        return text.Append('"').ToString();
    }
}
