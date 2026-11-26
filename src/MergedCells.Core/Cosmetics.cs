using System.Text.Json;
using System.Text.Json.Serialization;

namespace MergedCells.Core;

/// <summary>
/// The shop's catalogue, read from cosmetics/cosmetics.json: themes and player looks, each with an ID that's never
/// reused, because the save keys ownership by it. The first theme and the first look are the free defaults.
/// </summary>
public sealed class Cosmetics
{
    public const int MaxIdLength = 32;

    public const int MaxNameLength = 24;

    private const char ByteOrderMark = (char)0xFEFF;

    private Cosmetics(IReadOnlyList<CosmeticTheme> themes, IReadOnlyList<CosmeticLook> looks)
    {
        Themes = themes;
        Looks = looks;
    }

    public IReadOnlyList<CosmeticTheme> Themes { get; }

    public IReadOnlyList<CosmeticLook> Looks { get; }

    public CosmeticTheme DefaultTheme => Themes[0];

    public CosmeticLook DefaultLook => Looks[0];

    /// <summary>The theme with this ID, or the default for null or an ID this build doesn't have (a retired item).</summary>
    public CosmeticTheme ThemeFor(string? id) => Themes.FirstOrDefault(theme => theme.Id == id) ?? DefaultTheme;

    /// <summary>The look with this ID, or the default for null or an ID this build doesn't have.</summary>
    public CosmeticLook LookFor(string? id) => Looks.FirstOrDefault(look => look.Id == id) ?? DefaultLook;

    public bool IsFree(string id) => id == DefaultTheme.Id || id == DefaultLook.Id;

    /// <summary>
    /// Throws <see cref="ArgumentOutOfRangeException"/> unless every item but the two free ones has a price and every
    /// price names one of them. Run at start-up, since the prices and the catalogue are separate files.
    /// </summary>
    public void CheckPrices(ShopConfig shop)
    {
        ArgumentNullException.ThrowIfNull(shop);
        var items = Themes.Select(theme => (theme.Id, theme.Name)).Concat(Looks.Select(look => (look.Id, look.Name))).ToArray();
        foreach (var id in shop.Prices.Keys)
        {
            if (IsFree(id))
            {
                throw new ArgumentOutOfRangeException($"shop.prices.{id}", $"The config prices {id}, which is free.");
            }
            if (items.All(item => item.Id != id))
            {
                throw new ArgumentOutOfRangeException($"shop.prices.{id}", $"The config prices {id}, which isn't in the cosmetics file.");
            }
        }
        foreach (var (id, name) in items)
        {
            if (!IsFree(id) && shop.PriceOf(id) is null)
            {
                throw new ArgumentOutOfRangeException($"shop.prices.{id}", $"{name} has no price in the config's shop group.");
            }
        }
    }

    /// <summary>Lowercase letters and digits in runs joined by single hyphens, 1 to 32 characters.</summary>
    public static bool IsId(string? id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > MaxIdLength || id[0] == '-' || id[^1] == '-' || id.Contains("--", StringComparison.Ordinal))
        {
            return false;
        }
        return id.All(c => c == '-' || char.IsAsciiDigit(c) || char.IsAsciiLetterLower(c));
    }

    /// <summary>
    /// Parses the cosmetics file's text. Throws <see cref="JsonException"/>, naming the problem, for a missing, null,
    /// unknown or repeated key, a bad ID, name, colour or shape number, or text that isn't JSON.
    /// </summary>
    public static Cosmetics Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        // Some Windows editors save UTF-8 with a byte order mark, which System.Text.Json rejects
        var text = json.TrimStart(ByteOrderMark);
        var file = JsonSerializer.Deserialize(text, CosmeticsJsonContext.Default.CosmeticsJson)
            ?? throw new JsonException("The cosmetics file is null.");
        JsonKeys.RejectDuplicates(text, "cosmetics");
        // .NET 8's System.Text.Json enforces required keys but not non-null values, so each is checked by name
        var themesJson = file.Themes ?? throw new JsonException("The cosmetics file's \"themes\" is null.");
        var looksJson = file.Looks ?? throw new JsonException("The cosmetics file's \"looks\" is null.");
        if (themesJson.Length == 0 || looksJson.Length == 0)
        {
            throw new JsonException("The cosmetics file needs at least one theme and one look: the first of each is free.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var themes = new List<CosmeticTheme>();
        foreach (var theme in themesJson)
        {
            if (theme is null)
            {
                throw new JsonException("The cosmetics file has a null theme.");
            }
            var id = ReadId(theme.Id, ids);
            themes.Add(new CosmeticTheme(id, ReadName(theme.Name, id), ReadPalette(theme.Light, id, "light"), ReadPalette(theme.Dark, id, "dark")));
        }
        var looks = new List<CosmeticLook>();
        foreach (var look in looksJson)
        {
            if (look is null)
            {
                throw new JsonException("The cosmetics file has a null look.");
            }
            var id = ReadId(look.Id, ids);
            looks.Add(ReadLook(look, id));
        }
        return new Cosmetics(themes, looks);
    }

    private static string ReadId(string? id, HashSet<string> seen)
    {
        if (!IsId(id))
        {
            throw new JsonException($"The cosmetics ID \"{id}\" must be 1 to {MaxIdLength} lowercase letters and digits, joined by single hyphens.");
        }
        if (!seen.Add(id!))
        {
            throw new JsonException($"The cosmetics file uses the ID \"{id}\" twice.");
        }
        return id!;
    }

    private static string ReadName(string? name, string id)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > MaxNameLength)
        {
            throw new JsonException($"The cosmetic \"{id}\" needs a name of 1 to {MaxNameLength} characters.");
        }
        return name;
    }

    private static Palette ReadPalette(PaletteJson? palette, string id, string mode)
    {
        if (palette is null)
        {
            throw new JsonException($"The theme \"{id}\" has a null \"{mode}\" palette.");
        }
        Rgb Colour(string? text, string key) =>
            Rgb.TryParse(text, out var colour)
                ? colour
                : throw new JsonException($"The theme \"{id}\"'s {mode} \"{key}\" must be a colour written as #rrggbb in lowercase.");

        var fills = palette.Fills ?? throw new JsonException($"The theme \"{id}\"'s {mode} \"fills\" is null.");
        if (fills.Length != Palette.FillCount)
        {
            throw new JsonException($"The theme \"{id}\"'s {mode} \"fills\" has {fills.Length} colours; it needs exactly {Palette.FillCount}.");
        }
        return new Palette(
            Colour(palette.Background, "background"),
            Colour(palette.Ink, "ink"),
            Colour(palette.Card, "card"),
            Colour(palette.Muted, "muted"),
            Colour(palette.Chart, "chart"),
            Colour(palette.Placeholder, "placeholder"),
            Colour(palette.Disabled, "disabled"),
            Colour(palette.Pressed, "pressed"),
            Colour(palette.New, "new"),
            fills.Select(fill => Colour(fill, "fills")).ToArray(),
            Colour(palette.Outline, "outline"),
            Colour(palette.End, "end"),
            Colour(palette.Wall, "wall"),
            Colour(palette.WallMiddle, "wallMiddle"),
            Colour(palette.Trap, "trap"),
            Colour(palette.Teleporter, "teleporter"),
            Colour(palette.Player, "player"));
    }

    private static CosmeticLook ReadLook(LookJson look, string id)
    {
        if (look.Side is < 0.2 or > 0.8)
        {
            throw new JsonException($"The look \"{id}\"'s \"side\" is {look.Side}; it must be from 0.2 to 0.8.");
        }
        if (look.Stroke <= 0 || look.Stroke >= look.Side / 2)
        {
            throw new JsonException($"The look \"{id}\"'s \"stroke\" is {look.Stroke}; it must be above 0 and under half the side.");
        }
        if (look.Corner < 0 || look.Corner > look.Side / 2)
        {
            throw new JsonException($"The look \"{id}\"'s \"corner\" is {look.Corner}; it must be from 0 to half the side.");
        }
        if (look.Turn is < 0 or > 89)
        {
            throw new JsonException($"The look \"{id}\"'s \"turn\" is {look.Turn}; it must be from 0 to 89 degrees.");
        }
        return new CosmeticLook(id, ReadName(look.Name, id), look.Side, look.Stroke, look.Corner, look.Filled, look.Turn);
    }
}

internal sealed record CosmeticsJson
{
    public required ThemeJson?[]? Themes { get; init; }

    public required LookJson?[]? Looks { get; init; }
}

internal sealed record ThemeJson
{
    public required string? Id { get; init; }

    public required string? Name { get; init; }

    public required PaletteJson? Light { get; init; }

    public required PaletteJson? Dark { get; init; }
}

internal sealed record PaletteJson
{
    public required string? Background { get; init; }

    public required string? Ink { get; init; }

    public required string? Card { get; init; }

    public required string? Muted { get; init; }

    public required string? Chart { get; init; }

    public required string? Placeholder { get; init; }

    public required string? Disabled { get; init; }

    public required string? Pressed { get; init; }

    public required string? New { get; init; }

    public required string?[]? Fills { get; init; }

    public required string? Outline { get; init; }

    public required string? End { get; init; }

    public required string? Wall { get; init; }

    public required string? WallMiddle { get; init; }

    public required string? Trap { get; init; }

    public required string? Teleporter { get; init; }

    public required string? Player { get; init; }
}

internal sealed record LookJson
{
    public required string? Id { get; init; }

    public required string? Name { get; init; }

    public required double Side { get; init; }

    public required double Stroke { get; init; }

    public required double Corner { get; init; }

    public required bool Filled { get; init; }

    public required int Turn { get; init; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(CosmeticsJson))]
internal partial class CosmeticsJsonContext : JsonSerializerContext;
