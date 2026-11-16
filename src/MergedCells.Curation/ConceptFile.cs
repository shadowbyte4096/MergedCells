using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using MergedCells.Core;

namespace MergedCells.Curation;

// A concept level's file (concepts/<name>.json beside the tool): the band it aims at, its own par range, bends and
// twists, and two hand-drawn layers that become a MapSkeleton. Blocks and Tiles keep the layers as written, for the page
internal sealed record Concept(
    int Chapter,
    string Band,
    int MinPar,
    int MaxPar,
    bool Bends,
    TwistDensities? Twists,
    int? MaxWallRun,
    int Candidates,
    MapSkeleton Skeleton,
    IReadOnlyList<string> Blocks,
    IReadOnlyList<string> Tiles);

internal static class ConceptFile
{
    private const char ByteOrderMark = (char)0xFEFF;

    public static readonly Regex Name = new("^[a-z]+(-[a-z]+)*$", RegexOptions.CultureInvariant);

    // One character per square, single spaces between them, nothing else
    private static readonly Regex Row = new("^[^\\s]( [^\\s])*$", RegexOptions.CultureInvariant);

    private const string BlockSymbols = ". for a free square, E for the end, or a letter for a fixed block";

    private const string TileSymbols = ". where the tool may add twists, - for nothing, # for a wall, x for a trap, S for the start or s for a start square";

    /// <summary>
    /// Throws <see cref="JsonException"/>, naming the key, and the row and column for a square, when a value is missing,
    /// null or out of range, a key is unknown or repeated, a layer isn't a square grid of known symbols, or the squares
    /// break a skeleton rule.
    /// </summary>
    public static Concept Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var text = json.TrimStart(ByteOrderMark);
        var file = JsonSerializer.Deserialize(text, ConceptJsonContext.Default.ConceptFileJson)
            ?? throw new JsonException("The concept file is null.");
        JsonKeys.RejectDuplicates(text, "concept file");

        Require(file.Chapter >= 1, "chapter", "must be at least 1");
        var band = file.Band ?? throw Problem("band", "is null");
        Require(!string.IsNullOrWhiteSpace(band), "band", "is blank");
        var par = file.Par ?? throw Problem("par", "is null");
        Require(par is [>= 1, _] && par[0] <= par[1], "par", "must be [min, max], with min at least 1 and no more than max");
        TwistDensities? twists = null;
        if (file.Twists is { } given)
        {
            foreach (var (percent, key) in new[]
            {
                (given.WallPercent, "wallPercent"), (given.StickyPercent, "stickyPercent"), (given.TrapPercent, "trapPercent"), (given.TeleporterPercent, "teleporterPercent"),
            })
            {
                Require(percent is >= 0 and <= 100, $"twists.{key}", "must be from 0 to 100");
            }
            twists = new TwistDensities(given.WallPercent, given.StickyPercent, given.TrapPercent, given.TeleporterPercent);
        }
        Require(file.MaxWallRun is null or >= 1 and <= 100, "maxWallRun", "must be null or from 1 to 100");
        Require(file.Candidates >= 1, "candidates", "must be at least 1");

        var blocks = Squares(file.Blocks ?? throw Problem("blocks", "is null"), "blocks");
        var tiles = Squares(file.Tiles ?? throw Problem("tiles", "is null"), "tiles");
        Require(blocks.GetLength(0) == tiles.GetLength(0), "tiles", "must be the same size as \"blocks\"");
        MapSkeleton skeleton;
        try
        {
            skeleton = Skeleton(blocks, tiles);
        }
        catch (ArgumentException problem)
        {
            throw new JsonException($"The concept file's squares break a rule: {problem.Message}", problem);
        }
        return new Concept(
            file.Chapter, band, par[0], par[1], file.Bends, twists, file.MaxWallRun, file.Candidates, skeleton, [.. file.Blocks], [.. file.Tiles]);
    }

    // A layer's rows as a square grid of symbols, 2 to 100 a side
    private static char[,] Squares(string[] rows, string key)
    {
        Require(rows.Length is >= 2 and <= 100, key, "must have from 2 to 100 rows");
        var grid = new char[rows.Length, rows.Length];
        for (var row = 0; row < rows.Length; row++)
        {
            var text = rows[row] ?? throw Problem($"{key} row {row + 1}", "is null");
            Require(Row.IsMatch(text), $"{key} row {row + 1}", "must be single characters with one space between each");
            Require((text.Length + 1) / 2 == rows.Length, $"{key} row {row + 1}", $"must have {rows.Length} squares, one per row");
            for (var column = 0; column < rows.Length; column++)
            {
                grid[row, column] = text[column * 2];
            }
        }
        return grid;
    }

    private static MapSkeleton Skeleton(char[,] blocks, char[,] tiles)
    {
        var size = blocks.GetLength(0);
        var regions = new int?[size, size];
        var fixedTiles = new Tile?[size, size];
        var numbers = new Dictionary<char, int>();
        Cell? end = null;
        Cell? fixedStart = null;
        var starts = new List<Cell>();
        for (var row = 0; row < size; row++)
        {
            for (var column = 0; column < size; column++)
            {
                var cell = new Cell(row, column);
                var block = blocks[row, column];
                if (block == 'E')
                {
                    Require(end is null, Square("blocks", cell), "is a second E; a concept has one end");
                    end = cell;
                }
                else if (char.IsAsciiLetter(block))
                {
                    if (!numbers.TryGetValue(block, out var number))
                    {
                        number = numbers.Count + 1;
                        numbers[block] = number;
                    }
                    regions[row, column] = number;
                }
                else
                {
                    Require(block == '.', Square("blocks", cell), $"has \"{block}\": use {BlockSymbols}");
                }

                switch (tiles[row, column])
                {
                    case '.':
                        break;
                    case '-':
                        fixedTiles[row, column] = Tile.None;
                        break;
                    case '#':
                        fixedTiles[row, column] = Tile.Wall;
                        break;
                    case 'x':
                        fixedTiles[row, column] = Tile.Trap;
                        break;
                    case 'S':
                        Require(fixedStart is null, Square("tiles", cell), "is a second S; a concept has at most one");
                        fixedStart = cell;
                        fixedTiles[row, column] = Tile.None;
                        break;
                    case 's':
                        starts.Add(cell);
                        fixedTiles[row, column] = Tile.None;
                        break;
                    default:
                        throw Problem(Square("tiles", cell), $"has \"{tiles[row, column]}\": use {TileSymbols}");
                }
            }
        }
        Require(end is not null, "blocks", "needs an E for the end");
        Require(fixedStart is null || starts.Count == 0, "tiles", "can't mix S (a fixed start) with s (start squares)");
        var endCell = end!.Value;
        var startCells = fixedStart is { } start ? [start] : starts;
        CheckInFileTerms(blocks, tiles, endCell, startCells);
        return new MapSkeleton(regions, fixedTiles, endCell, startCells);
    }

    // The skeleton rules a drawing can break, said with the file's rows and columns from 1 and the block's letter;
    // MapSkeleton checks them again as the backstop
    private static void CheckInFileTerms(char[,] blocks, char[,] tiles, Cell end, IReadOnlyList<Cell> starts)
    {
        Require(tiles[end.Row, end.Column] is '.' or '-', Square("tiles", end), "is the end's square, so it can only be . or -");
        foreach (var start in starts)
        {
            Require(
                start.Row <= end.Row && start.Column <= end.Column,
                Square("tiles", start),
                $"is a start square right of or below the end ({Square("blocks", end)})");
        }

        var size = blocks.GetLength(0);
        var checkedLetters = new HashSet<char>();
        for (var row = 0; row < size; row++)
        {
            for (var column = 0; column < size; column++)
            {
                var letter = blocks[row, column];
                if (letter is '.' or 'E' || !checkedLetters.Add(letter))
                {
                    continue;
                }
                var reached = new bool[size, size];
                var queue = new Queue<Cell>([new Cell(row, column)]);
                reached[row, column] = true;
                while (queue.Count > 0)
                {
                    var cell = queue.Dequeue();
                    foreach (var next in new[] { cell.Step(Direction.Up), cell.Step(Direction.Down), cell.Step(Direction.Left), cell.Step(Direction.Right) })
                    {
                        if (next.Row >= 0 && next.Column >= 0 && next.Row < size && next.Column < size
                            && !reached[next.Row, next.Column] && blocks[next.Row, next.Column] == letter)
                        {
                            reached[next.Row, next.Column] = true;
                            queue.Enqueue(next);
                        }
                    }
                }
                for (var r = 0; r < size; r++)
                {
                    for (var c = 0; c < size; c++)
                    {
                        Require(
                            blocks[r, c] != letter || reached[r, c],
                            "blocks",
                            $"letter \"{letter}\" is in more than one piece: row {r + 1}, column {c + 1} doesn't join its first square");
                    }
                }
            }
        }
    }

    private static string Square(string key, Cell cell) => $"{key} row {cell.Row + 1}, column {cell.Column + 1}";

    private static void Require(bool holds, string path, string rule)
    {
        if (!holds)
        {
            throw Problem(path, rule);
        }
    }

    private static JsonException Problem(string path, string rule) => new($"The concept file's \"{path}\" {rule}.");
}

internal sealed record ConceptFileJson
{
    public required int Chapter { get; init; }

    public required string Band { get; init; }

    public required int[] Par { get; init; }

    public required bool Bends { get; init; }

    // Required but nullable, so a concept must say "null" to mean no twists
    public required RecipeTwistsJson? Twists { get; init; }

    public required int? MaxWallRun { get; init; }

    public required int Candidates { get; init; }

    public required string[] Blocks { get; init; }

    public required string[] Tiles { get; init; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(ConceptFileJson))]
internal partial class ConceptJsonContext : JsonSerializerContext;
