using System.Text.Json;

namespace MergedCells.Core.Tests;

public class CosmeticsTests
{
    private const string LightPalette =
        """{"background":"#e3f1ed","ink":"#235e56","card":"#ffffff","muted":"#3a7068","chart":"#3a7068","placeholder":"#e5eceb","disabled":"#8dafa9","pressed":"#497b74","new":"#349f8b","fills":["#f2f5f5","#e0e8e7","#cfdcda","#bdcfcc","#abc2bf"],"outline":"#235e56","end":"#235e56","wall":"#235e56","wallMiddle":"#a9c5c0","trap":"#235e56","teleporter":"#235e56","player":"#235e56"}""";

    private const string DarkPalette =
        """{"background":"#0e1f1c","ink":"#5ed3bf","card":"#16312c","muted":"#4aa696","chart":"#62bdac","placeholder":"#1f443e","disabled":"#327065","pressed":"#4eaf9e","new":"#9cf2e2","fills":["#132a26","#193833","#204740","#26554d","#2c635a"],"outline":"#5ed3bf","end":"#5ed3bf","wall":"#5ed3bf","wallMiddle":"#26554d","trap":"#5ed3bf","teleporter":"#5ed3bf","player":"#5ed3bf"}""";

    private const string SquareLook = """{"id":"square","name":"Square","side":0.56,"stroke":0.1,"corner":0.1,"filled":false,"turn":0}""";

    private const string RingLook = """{"id":"ring","name":"Ring","side":0.56,"stroke":0.1,"corner":0.28,"filled":false,"turn":0}""";

    private static readonly string Valid =
        "{\"themes\":[" + Theme("teal", "Teal") + "," + Theme("ember", "Ember") + "],\"looks\":[" + SquareLook + "," + RingLook + "]}";

    private static string Theme(string id, string name) =>
        $$"""{"id":"{{id}}","name":"{{name}}","light":{{LightPalette}},"dark":{{DarkPalette}}}""";

    public static TheoryData<string> BadFiles() => new()
    {
        "not json",
        "null",
        Valid.Replace("\"themes\":", "\"themez\":"),
        Valid.Replace("{\"themes\":[", "{\"extra\":1,\"themes\":["),
        "{\"themes\":[],\"looks\":[" + SquareLook + "]}",
        "{\"themes\":[" + Theme("teal", "Teal") + "],\"looks\":[]}",
        "{\"themes\":null,\"looks\":[" + SquareLook + "]}",
        "{\"themes\":[null],\"looks\":[" + SquareLook + "]}",
        Valid.Replace("\"looks\":[", "\"looks\":[null,"),
        Valid.Replace("\"looks\":[", "\"looks\":[],\"looks\":["),
        Valid.Replace("\"ember\"", "\"Ember\""),
        Valid.Replace("\"ember\"", "\"-ember\""),
        Valid.Replace("\"ember\"", "\"em--ber\""),
        Valid.Replace("\"ember\"", "\"\""),
        Valid.Replace("\"ember\"", "\"" + new string('e', 33) + "\""),
        Valid.Replace("\"ember\"", "\"teal\""),
        Valid.Replace("\"ember\"", "\"ring\""),
        Valid.Replace("\"name\":\"Ember\"", "\"name\":\" \""),
        Valid.Replace("\"name\":\"Ember\"", "\"name\":\"" + new string('E', 25) + "\""),
        Valid.Replace("\"name\":\"Ember\"", "\"name\":null"),
        Valid.Replace("\"ink\":\"#235e56\"", "\"ink\":\"#235E56\""),
        Valid.Replace("\"ink\":\"#235e56\"", "\"ink\":\"#235e5\""),
        Valid.Replace("\"ink\":\"#235e56\"", "\"ink\":null"),
        Valid.Replace("\"ink\":\"#235e56\",", ""),
        Valid.Replace("\"ink\":\"#235e56\",", "\"ink\":\"#235e56\",\"extra\":\"#235e56\","),
        Valid.Replace(",\"#abc2bf\"]", "]"),
        Valid.Replace(",\"#abc2bf\"]", ",\"#abc2bf\",\"#ffffff\"]"),
        Valid.Replace("\"fills\":[\"#f2f5f5\"", "\"fills\":[null"),
        Valid.Replace("\"dark\":" + DarkPalette, "\"dark\":null"),
        Valid.Replace(RingLook, RingLook.Replace("\"side\":0.56", "\"side\":0.9")),
        Valid.Replace(RingLook, RingLook.Replace("\"side\":0.56", "\"side\":0.1")),
        Valid.Replace(RingLook, RingLook.Replace("\"stroke\":0.1", "\"stroke\":0.28")),
        Valid.Replace(RingLook, RingLook.Replace("\"stroke\":0.1", "\"stroke\":0")),
        Valid.Replace(RingLook, RingLook.Replace("\"corner\":0.28", "\"corner\":0.29")),
        Valid.Replace(RingLook, RingLook.Replace("\"corner\":0.28", "\"corner\":-0.01")),
        Valid.Replace(RingLook, RingLook.Replace("\"turn\":0", "\"turn\":90")),
        Valid.Replace(RingLook, RingLook.Replace("\"turn\":0", "\"turn\":-1")),
        Valid.Replace(RingLook, RingLook.Replace("\"filled\":false", "\"filled\":null")),
        Valid.Replace(RingLook, RingLook.Replace(",\"turn\":0", "")),
    };

    [Theory]
    [MemberData(nameof(BadFiles))]
    public void Parse_rejects(string json)
    {
        Assert.ThrowsAny<JsonException>(() => Cosmetics.Parse(json));
    }

    [Fact]
    public void Parse_reads_themes_in_order()
    {
        var cosmetics = Cosmetics.Parse(Valid);

        Assert.Equal(["teal", "ember"], cosmetics.Themes.Select(theme => theme.Id));
    }

    [Fact]
    public void Parse_reads_looks_in_order()
    {
        var cosmetics = Cosmetics.Parse(Valid);

        Assert.Equal(["square", "ring"], cosmetics.Looks.Select(look => look.Id));
    }

    [Fact]
    public void Parse_reads_a_palette()
    {
        var palette = Cosmetics.Parse(Valid).Themes[0].Light;

        Assert.Equal(Rgb.Parse("#235e56"), palette.Ink);
        Assert.Equal(Rgb.Parse("#bdcfcc"), palette.Fills[3]);
    }

    [Fact]
    public void Dark_picks_the_dark_palette()
    {
        var theme = Cosmetics.Parse(Valid).Themes[0];

        Assert.Same(theme.Dark, theme.For(dark: true));
    }

    [Fact]
    public void Parse_reads_a_look()
    {
        var ring = Cosmetics.Parse(Valid).Looks[1];

        Assert.Equal(new CosmeticLook("ring", "Ring", 0.56, 0.1, 0.28, false, 0), ring);
    }

    [Fact]
    public void Parse_accepts_a_byte_order_mark()
    {
        Assert.Equal(2, Cosmetics.Parse('﻿' + Valid).Themes.Count);
    }

    [Fact]
    public void Defaults_are_the_first_of_each_kind()
    {
        var cosmetics = Cosmetics.Parse(Valid);

        Assert.Equal(("teal", "square"), (cosmetics.DefaultTheme.Id, cosmetics.DefaultLook.Id));
    }

    [Fact]
    public void ThemeFor_null_is_the_default()
    {
        var cosmetics = Cosmetics.Parse(Valid);

        Assert.Same(cosmetics.DefaultTheme, cosmetics.ThemeFor(null));
    }

    [Fact]
    public void ThemeFor_unknown_id_is_the_default()
    {
        var cosmetics = Cosmetics.Parse(Valid);

        Assert.Same(cosmetics.DefaultTheme, cosmetics.ThemeFor("retired-theme"));
    }

    [Fact]
    public void ThemeFor_known_id_is_that_theme()
    {
        var cosmetics = Cosmetics.Parse(Valid);

        Assert.Same(cosmetics.Themes[1], cosmetics.ThemeFor("ember"));
    }

    [Fact]
    public void LookFor_unknown_id_is_the_default()
    {
        var cosmetics = Cosmetics.Parse(Valid);

        Assert.Same(cosmetics.DefaultLook, cosmetics.LookFor("retired-look"));
    }

    [Fact]
    public void LookFor_known_id_is_that_look()
    {
        var cosmetics = Cosmetics.Parse(Valid);

        Assert.Same(cosmetics.Looks[1], cosmetics.LookFor("ring"));
    }

    [Theory]
    [InlineData("teal", true)]
    [InlineData("square", true)]
    [InlineData("ember", false)]
    [InlineData("ring", false)]
    [InlineData("retired", false)]
    public void IsFree_only_the_first_of_each_kind(string id, bool expected)
    {
        Assert.Equal(expected, Cosmetics.Parse(Valid).IsFree(id));
    }

    [Theory]
    [InlineData("ember", true)]
    [InlineData("a1-b2", true)]
    [InlineData("x", true)]
    [InlineData("Ember", false)]
    [InlineData("-x", false)]
    [InlineData("x-", false)]
    [InlineData("a--b", false)]
    [InlineData("a b", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsId(string? id, bool expected)
    {
        Assert.Equal(expected, Cosmetics.IsId(id));
    }

    [Fact]
    public void CheckPrices_accepts_exact_coverage()
    {
        Cosmetics.Parse(Valid).CheckPrices(Shop(("ember", 300), ("ring", 150)));
    }

    [Fact]
    public void CheckPrices_rejects_an_unpriced_item()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => Cosmetics.Parse(Valid).CheckPrices(Shop(("ember", 300))));

        Assert.Contains("Ring has no price", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckPrices_rejects_a_priced_free_item()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => Cosmetics.Parse(Valid).CheckPrices(Shop(("ember", 300), ("ring", 150), ("teal", 1))));

        Assert.Contains("prices teal, which is free", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckPrices_rejects_a_price_for_no_item()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => Cosmetics.Parse(Valid).CheckPrices(Shop(("ember", 300), ("ring", 150), ("dot", 1))));

        Assert.Contains("prices dot, which isn't in the cosmetics file", exception.Message, StringComparison.Ordinal);
    }

    private static ShopConfig Shop(params (string Id, int Price)[] prices) =>
        new() { Prices = prices.ToDictionary(price => price.Id, price => price.Price) };

    [Fact]
    public void IsId_allows_32_characters_but_not_33()
    {
        Assert.Equal((true, false), (Cosmetics.IsId(new string('a', 32)), Cosmetics.IsId(new string('a', 33))));
    }
}
