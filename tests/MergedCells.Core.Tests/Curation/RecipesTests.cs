using System.Text.Json;
using MergedCells.Curation;

namespace MergedCells.Core.Tests;

public class RecipesTests
{
    private const string Band =
        """{"sizes":[5,6],"par":[2,4],"twists":null,"candidates":2,"oneStarExtraMoves":null,"target":{"minScore":0,"maxScore":10}}""";

    private const string Twists = """{"wallPercent":6,"stickyPercent":0,"trapPercent":0,"teleporterPercent":0}""";

    private const string Valid =
        """{"starLimits":{"minRoutes":10,"maxExtraMoves":10},"chapters":{"1":{"name":"Plain","bends":false,"bands":{"easy":""" + Band + "}}}}";

    private static string WithBand(string band) => Valid.Replace(Band, band);

    public static TheoryData<string> InvalidTexts => new()
    {
        Valid[..^1] + ",\"extra\":1}",
        Valid.Replace("\"minRoutes\":10", "\"minRoutes\":10,\"minRoutes\":9"),
        Valid[..Valid.IndexOf("\"chapters\"", StringComparison.Ordinal)] + "\"chapters\":null}",
        Valid.Replace("{\"minRoutes\":10,\"maxExtraMoves\":10}", "null"),
        Valid.Replace("\"easy\":" + Band, "\"easy\":null"),
        Valid.Replace("\"name\":\"Plain\"", "\"name\":null"),
        Valid.Replace("\"name\":\"Plain\"", "\"name\":\" \""),
        Valid.Replace("\"1\":{", "\"0\":{"),
        Valid.Replace("\"easy\":", "\"Hard\":"),
        Valid.Replace("\"easy\":", "\"two--words\":"),
        WithBand(Band.Replace("[5,6]", "[1,5]")),
        WithBand(Band.Replace("[5,6]", "[6,5]")),
        WithBand(Band.Replace("[5,6]", "[5,101]")),
        WithBand(Band.Replace("[5,6]", "[5]")),
        WithBand(Band.Replace("[2,4]", "[0,3]")),
        WithBand(Band.Replace("[2,4]", "[4,2]")),
        WithBand(Band.Replace("\"twists\":null", "\"twists\":" + Twists.Replace("\"wallPercent\":6", "\"wallPercent\":101"))),
        WithBand(Band.Replace("\"twists\":null", "\"twists\":" + Twists.Replace(",\"teleporterPercent\":0", ""))),
        WithBand(Band.Replace("\"candidates\":2", "\"candidates\":0")),
        WithBand(Band.Replace("\"oneStarExtraMoves\":null", "\"oneStarExtraMoves\":-1")),
        WithBand(Band.Replace("\"minScore\":0", "\"minScore\":-1")),
        WithBand(Band.Replace("\"minScore\":0,\"maxScore\":10", "\"minScore\":20,\"maxScore\":10")),
        WithBand(Band.Replace("\"maxScore\":10", "\"maxScore\":101")),
        WithBand(Band.Replace("\"maxScore\":10", "\"maxScore\":10,\"minimums\":{\"decoys\":-1}")),
        WithBand(Band.Replace("\"maxScore\":10", "\"maxScore\":10,\"minimums\":{\"loops\":1}")),
        WithBand(Band.Replace("\"maxScore\":10", "\"maxScore\":10,\"minTwistReliance\":-1")),
        WithBand(Band.Replace("\"twists\":null,", "")),
        Valid.Replace("\"minRoutes\":10", "\"minRoutes\":0"),
        Valid.Replace("\"maxExtraMoves\":10", "\"maxExtraMoves\":21"),
        "null",
    };

    [Fact]
    public void Shipped_recipes_parse()
    {
        var recipes = Recipes.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "curation", "recipes.json")));

        Assert.Equal(Enumerable.Range(1, 8), recipes.Chapters.Keys.Order());
    }

    [Fact]
    public void Valid_text_used_by_the_invalid_cases_parses()
    {
        Assert.Null(Record.Exception(() => Recipes.Parse(Valid)));
    }

    [Theory]
    [MemberData(nameof(InvalidTexts))]
    public void Invalid_recipe_throws_JsonException(string text)
    {
        Assert.Throws<JsonException>(() => Recipes.Parse(text));
    }

    // A case that broke the JSON itself would throw without testing the rule it was written for
    [Theory]
    [MemberData(nameof(InvalidTexts))]
    public void Every_invalid_recipe_case_is_still_JSON(string text)
    {
        Assert.Null(Record.Exception(() => JsonDocument.Parse(text).Dispose()));
    }

    [Fact]
    public void Band_reads_into_a_request_and_a_target()
    {
        var text = WithBand(Band.Replace("\"twists\":null", "\"twists\":" + Twists)
            .Replace("\"maxScore\":10", "\"maxScore\":10,\"minimums\":{\"bruteForceExtraMoves\":2},\"minTwistReliance\":1"));

        var band = Recipes.Parse(text).Chapters[1].Bands["easy"];

        Assert.Equal(
            (5, 6, 2, 4, new TwistDensities(6, 0, 0, 0), new DifficultyTarget(0, 10, new MetricMinimums(BruteForceExtraMoves: 2), 1)),
            (band.MinSize, band.MaxSize, band.MinPar, band.MaxPar, band.Twists, band.Target));
    }

    [Fact]
    public void Recipe_accepts_a_leading_byte_order_mark()
    {
        Assert.Null(Record.Exception(() => Recipes.Parse((char)0xFEFF + Valid)));
    }
}
