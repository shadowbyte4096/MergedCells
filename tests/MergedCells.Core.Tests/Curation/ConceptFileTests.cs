using System.Text.Json;
using MergedCells.Curation;

namespace MergedCells.Core.Tests;

public class ConceptFileTests
{
    // The design's example: The Gap, a wall line across row 3 with one gap, the start anywhere in the top-left 2x2
    private static readonly string[] Blocks =
    [
        ". . . . . . .",
        ". . . . . . .",
        ". . . . . . .",
        ". . . . . . .",
        ". . . . . . .",
        ". . . . . . .",
        ". . . . . . E",
    ];

    private static readonly string[] Tiles =
    [
        "s s . . . . .",
        "s s . . . . .",
        ". . . . . . .",
        "# # # # . # #",
        ". . . . . . .",
        ". . . . . . .",
        ". . . . . . .",
    ];

    private const string Settings =
        "\"chapter\": 3,\n  \"band\": \"medium\",\n  \"par\": [4, 12],\n  \"bends\": true,\n  \"twists\": null,\n  \"maxWallRun\": null,\n  \"candidates\": 20";

    private const string Twists = """{ "wallPercent": 6, "stickyPercent": 0, "trapPercent": 0, "teleporterPercent": 0 }""";

    private static string Layer(IEnumerable<string> rows) => "[\n    " + string.Join(",\n    ", rows.Select(row => JsonSerializer.Serialize(row))) + "\n  ]";

    private static string Text(string[]? blocks = null, string[]? tiles = null, string settings = Settings) =>
        $"{{\n  {settings},\n  \"blocks\": {Layer(blocks ?? Blocks)},\n  \"tiles\": {Layer(tiles ?? Tiles)}\n}}\n";

    private static string[] With(string[] layer, params (int Row, string Text)[] rows)
    {
        var changed = (string[])layer.Clone();
        foreach (var (row, text) in rows)
        {
            changed[row] = text;
        }
        return changed;
    }

    private static readonly string Valid = Text();

    public static TheoryData<string> InvalidTexts => new()
    {
        Text(settings: Settings + ",\n  \"extra\": 1"),
        Text(settings: Settings + ",\n  \"chapter\": 4"),
        Text(settings: Settings.Replace("\"par\": [4, 12],\n  ", "")),
        Text(settings: Settings.Replace("[4, 12]", "[5, 4]")),
        Text(settings: Settings.Replace("[4, 12]", "[0, 4]")),
        Text(settings: Settings.Replace("\"maxWallRun\": null", "\"maxWallRun\": 0")),
        Text(settings: Settings.Replace("\"maxWallRun\": null", "\"maxWallRun\": 101")),
        Text(settings: Settings.Replace("\"candidates\": 20", "\"candidates\": 0")),
        Text(settings: Settings.Replace("\"twists\": null", "\"twists\": " + Twists.Replace("\"wallPercent\": 6", "\"wallPercent\": 101"))),
        Text(settings: Settings.Replace("\"chapter\": 3", "\"chapter\": 0")),
        Text(settings: Settings.Replace("\"band\": \"medium\"", "\"band\": \" \"")),
        Text(settings: Settings.Replace("\"band\": \"medium\"", "\"band\": null")),
        Text(blocks: [.. Blocks, ". . . . . . ."]),
        Text(tiles: With(Tiles, (2, ". . . . . .")), blocks: Blocks),
        Text(Blocks.Select(row => row[..^2]).ToArray(), Tiles.Select(row => row[..^2]).ToArray()),
        Text(["E"], ["."]),
        Text(tiles: With(Tiles, (3, "# # # # ? # #"))),
        Text(blocks: With(Blocks, (0, "? . . . . . ."))),
        Text(tiles: With(Tiles, (3, "# # # #  . # #"))),
        Text(tiles: With(Tiles, (3, "# # # # . # # "))),
        Text(tiles: With(Tiles, (3, "# # # #\t. # #"))),
        Text(blocks: With(Blocks, (6, ". . . . . . ."))),
        Text(blocks: With(Blocks, (6, ". . . . . E E"))),
        Text(tiles: With(Tiles, (0, "S . . . . . ."), (1, "S . . . . . ."))),
        Text(tiles: With(Tiles, (0, "S s . . . . ."), (1, ". . . . . . ."))),
        Text(blocks: With(Blocks, (0, "X . . . . . ."), (6, ". . . . . X E"))),
        Text(tiles: With(Tiles, (6, ". . . . . . #"))),
        "null",
    };

    private static Concept Example() => ConceptFile.Parse(Valid);

    [Fact]
    public void Example_parses_into_its_size_and_end()
    {
        var concept = Example();

        Assert.Equal((7, new Cell(6, 6)), (concept.Skeleton.Size, concept.Skeleton.End));
    }

    [Fact]
    public void Example_parses_into_its_start_squares()
    {
        Assert.Equal([new Cell(0, 0), new(0, 1), new(1, 0), new(1, 1)], Example().Skeleton.Starts);
    }

    [Fact]
    public void X_reads_as_a_fixed_trap()
    {
        var skeleton = ConceptFile.Parse(Text(tiles: With(Tiles, (5, ". . x . . . .")))).Skeleton;

        Assert.Equal(Tile.Trap, skeleton.TileAt(new(5, 2)));
    }

    [Fact]
    public void Example_parses_into_its_tiles()
    {
        var skeleton = Example().Skeleton;

        Assert.Equal(
            ((Tile?)Tile.Wall, (Tile?)null, (Tile?)Tile.None),
            (skeleton.TileAt(new(3, 0)), skeleton.TileAt(new(3, 4)), skeleton.TileAt(new(0, 0))));
    }

    [Fact]
    public void Example_parses_into_its_settings()
    {
        var concept = Example();

        Assert.Equal(
            (3, "medium", 4, 12, true, (TwistDensities?)null, (int?)null, 20),
            (concept.Chapter, concept.Band, concept.MinPar, concept.MaxPar, concept.Bends, concept.Twists, concept.MaxWallRun, concept.Candidates));
    }

    [Fact]
    public void Free_squares_have_no_fixed_region()
    {
        Assert.Null(Example().Skeleton.RegionAt(new(2, 2)));
    }

    [Fact]
    public void Each_letter_becomes_one_fixed_region()
    {
        var skeleton = ConceptFile.Parse(Text(blocks: With(Blocks, (0, "A A A . . . ."), (1, "b b . . . . .")))).Skeleton;

        Assert.Equal(
            (true, true, false),
            (skeleton.RegionAt(new(0, 0)) == skeleton.RegionAt(new(0, 2)),
                skeleton.RegionAt(new(1, 0)) == skeleton.RegionAt(new(1, 1)),
                skeleton.RegionAt(new(0, 0)) == skeleton.RegionAt(new(1, 0))));
    }

    [Fact]
    public void Fixed_start_is_the_only_start()
    {
        var skeleton = ConceptFile.Parse(Text(tiles: With(Tiles, (0, "S . . . . . ."), (1, ". . . . . . .")))).Skeleton;

        Assert.Equal([new Cell(0, 0)], skeleton.Starts);
    }

    [Fact]
    public void No_start_squares_leave_the_start_to_the_generator()
    {
        var skeleton = ConceptFile.Parse(Text(tiles: With(Tiles, (0, ". . . . . . ."), (1, ". . . . . . .")))).Skeleton;

        Assert.Empty(skeleton.Starts);
    }

    [Fact]
    public void Plain_symbol_fixes_a_square_with_no_twist()
    {
        var skeleton = ConceptFile.Parse(Text(tiles: With(Tiles, (2, "- . . . . . .")))).Skeleton;

        Assert.Equal(Tile.None, skeleton.TileAt(new(2, 0)));
    }

    [Fact]
    public void Layers_are_kept_as_written_for_the_page()
    {
        var concept = Example();

        Assert.Equal((Tiles[3], Blocks[6]), (concept.Tiles[3], concept.Blocks[6]));
    }

    [Fact]
    public void Concept_with_crlf_and_a_byte_order_mark_parses()
    {
        var concept = ConceptFile.Parse("﻿" + Valid.Replace("\n", "\r\n"));

        Assert.Equal((7, Tiles[3]), (concept.Skeleton.Size, concept.Tiles[3]));
    }

    [Theory]
    [MemberData(nameof(InvalidTexts))]
    public void Invalid_concept_throws_JsonException(string text)
    {
        Assert.Throws<JsonException>(() => ConceptFile.Parse(text));
    }

    // A skeleton rule is reported in the file's own terms: rows and columns from 1, and the block's letter
    public static TheoryData<string, string> SkeletonRuleTexts => new()
    {
        {
            Text(With(Blocks, (2, ". . . E . . ."), (6, ". . . . . . .")), With(Tiles, (0, ". . . . . s ."), (1, ". . . . . . ."))),
            "\"tiles row 1, column 6\""
        },
        { Text(tiles: With(Tiles, (6, ". . . . . . #"))), "\"tiles row 7, column 7\"" },
        { Text(tiles: With(Tiles, (6, ". . . . . . x"))), "\"tiles row 7, column 7\"" },
        { Text(blocks: With(Blocks, (0, "X . . . . . ."), (6, ". . . . . X E"))), "letter \"X\"" },
    };

    [Theory]
    [MemberData(nameof(SkeletonRuleTexts))]
    public void Skeleton_rule_errors_name_the_file_s_square_or_letter(string text, string named)
    {
        var problem = Assert.Throws<JsonException>(() => ConceptFile.Parse(text));

        Assert.Contains(named, problem.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("the-gap", true)]
    [InlineData("maze", true)]
    [InlineData("The-Gap", false)]
    [InlineData("the--gap", false)]
    [InlineData("gap2", false)]
    public void Names_are_lowercase_words_joined_by_hyphens(string name, bool valid)
    {
        Assert.Equal(valid, ConceptFile.Name.IsMatch(name));
    }

    private static string ShippedFolder => Path.Combine(AppContext.BaseDirectory, "curation", "concepts");

    public static TheoryData<string> ShippedConcepts =>
        new(Directory.Exists(ShippedFolder) && Directory.GetFiles(ShippedFolder, "*.json").Length > 0
            ? Directory.GetFiles(ShippedFolder, "*.json").Select(path => Path.GetFileName(path))
            : ["none"]);

    [Theory]
    [MemberData(nameof(ShippedConcepts))]
    public void Shipped_concept_parses_and_names_a_band_in_the_recipes(string file)
    {
        Assert.SkipWhen(file == "none", "No concept files yet");
        var concept = ConceptFile.Parse(File.ReadAllText(Path.Combine(ShippedFolder, file)));
        var recipes = Recipes.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "curation", "recipes.json")));

        Assert.True(ConceptFile.Name.IsMatch(Path.GetFileNameWithoutExtension(file)), $"{file} isn't named in lowercase words joined by hyphens");
        Assert.True(recipes.Chapters[concept.Chapter].Bands.ContainsKey(concept.Band), $"{file} names a band its chapter's recipe doesn't have");
    }
}
