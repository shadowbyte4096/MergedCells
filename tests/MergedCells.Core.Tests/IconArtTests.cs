using System.Globalization;
using System.Text.Json;
using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class IconArtTests
{
    private const string LeftGroup = """{"x":1,"y":2,"width":3,"height":4,"corner":5,"outline":6,"fill":0}""";

    private const string RightGroup = """{"x":7,"y":8,"width":9,"height":10,"corner":11,"outline":12,"fill":2}""";

    private const string PlayerGroup = """{"x":13,"y":14,"side":15,"outline":16,"corner":17,"trailStart":18}""";

    private const string Valid = """{"leftBlock":""" + LeftGroup + ""","rightBlock":""" + RightGroup + ""","player":""" + PlayerGroup + "}";

    private static readonly string[] BlockKeys = ["x", "y", "width", "height", "corner", "outline", "fill"];

    private static readonly string[] PlayerKeys = ["x", "y", "side", "outline", "corner", "trailStart"];

    internal static IconArt Shipped() =>
        IconArt.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "icons", "icon-art.json")));

    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string Block(ArtBlock block) =>
        $$"""{"x":{{Number(block.X)}},"y":{{Number(block.Y)}},"width":{{Number(block.Width)}},"height":{{Number(block.Height)}},"corner":{{Number(block.Corner)}},"outline":{{Number(block.Outline)}},"fill":{{block.Fill}}}""";

    private static string Text(IconArt art) =>
        $$$"""{"leftBlock":{{{Block(art.LeftBlock)}}},"rightBlock":{{{Block(art.RightBlock)}}},"player":{"x":{{{Number(art.Player.X)}}},"y":{{{Number(art.Player.Y)}}},"side":{{{Number(art.Player.Side)}}},"outline":{{{Number(art.Player.Outline)}}},"corner":{{{Number(art.Player.Corner)}}},"trailStart":{{{Number(art.Player.TrailStart)}}}}}""";

    // Removes one key (and its comma) from a one-line object
    private static string Without(string group, string key)
    {
        var start = group.IndexOf($"\"{key}\":", StringComparison.Ordinal);
        var end = group.IndexOfAny([',', '}'], start);
        var cut = group[end] == ',' ? group.Remove(start, end - start + 1) : group.Remove(start - 1, end - start + 1);
        return cut;
    }

    public static TheoryData<string> InvalidTexts()
    {
        var texts = new TheoryData<string>
        {
            "",
            "not json",
            "null",
            Valid.Replace(LeftGroup, LeftGroup.Replace("}", ",\"extra\":1}")),
            Valid.Replace(RightGroup, RightGroup.Replace("}", ",\"extra\":1}")),
            Valid.Replace(PlayerGroup, PlayerGroup.Replace("}", ",\"extra\":1}")),
            Valid.Replace("}}", "},\"extra\":1}"),
            Valid.Replace(""","player":""" + PlayerGroup, ""),
            Valid.Replace(""","rightBlock":""" + RightGroup, ""),
            Valid.Replace("""{"leftBlock":""" + LeftGroup + ",", "{"),
            Valid.Replace(LeftGroup, "null"),
            Valid.Replace(RightGroup, "null"),
            Valid.Replace(PlayerGroup, "null"),
            Valid[..^1] + ""","player":""" + PlayerGroup + "}",
        };
        foreach (var key in BlockKeys)
        {
            texts.Add(Valid.Replace(LeftGroup, Without(LeftGroup, key)));
            texts.Add(Valid.Replace(RightGroup, Without(RightGroup, key)));
        }
        foreach (var key in PlayerKeys)
        {
            texts.Add(Valid.Replace(PlayerGroup, Without(PlayerGroup, key)));
        }
        return texts;
    }

    [Fact]
    public void The_shipped_art_parses()
    {
        Assert.Null(Record.Exception(() => Shipped()));
    }

    [Fact]
    public void Valid_text_used_by_the_invalid_cases_parses()
    {
        Assert.Null(Record.Exception(() => IconArt.Parse(Valid)));
    }

    [Theory]
    [AutoData]
    public void Parse_reads_every_value(IconArt art)
    {
        Assert.Equal(art, IconArt.Parse(Text(art)));
    }

    [Theory]
    [AutoData]
    public void Parse_accepts_a_leading_byte_order_mark(IconArt art)
    {
        Assert.Equal(art, IconArt.Parse((char)0xFEFF + Text(art)));
    }

    // The outline is centred on the square's edge, so half of it lies outside
    [Theory]
    [AutoData]
    public void The_fade_ends_at_the_players_outer_left_edge(ArtPlayer player)
    {
        Assert.Equal(player.X - player.Outline / 2, player.FadeEnd, 9);
    }

    [Theory]
    [MemberData(nameof(InvalidTexts))]
    public void Unknown_missing_repeated_or_null_parts_are_rejected(string text)
    {
        Assert.Throws<JsonException>(() => IconArt.Parse(text));
    }
}
