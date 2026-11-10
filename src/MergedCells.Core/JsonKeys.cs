using System.Globalization;
using System.Text;
using System.Text.Json;

namespace MergedCells.Core;

// .NET 8's System.Text.Json keeps the last value of a repeated key without complaint, so the strict parsers check here.
// Replace with JsonSerializerOptions.AllowDuplicateProperties once every project is on .NET 10.
internal static class JsonKeys
{
    /// <summary>
    /// Throws <see cref="JsonException"/>, naming the key, when an object in the text has a key twice. Keys that read as
    /// the same whole number ("4", "04", "+4") count as one, because a dictionary keyed by number reads them as one.
    /// The text must already be known to be JSON.
    /// </summary>
    public static void RejectDuplicates(string json, string file)
    {
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
        var objects = new Stack<HashSet<string>>();
        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    objects.Push(new HashSet<string>(StringComparer.Ordinal));
                    break;
                case JsonTokenType.EndObject:
                    objects.Pop();
                    break;
                case JsonTokenType.PropertyName:
                    var key = reader.GetString()!;
                    var canonical = int.TryParse(key, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)
                        ? number.ToString(CultureInfo.InvariantCulture)
                        : key;
                    if (!objects.Peek().Add(canonical))
                    {
                        throw new JsonException($"The {file} has the key \"{key}\" more than once in one object.");
                    }
                    break;
            }
        }
    }
}
