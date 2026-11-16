using System.Globalization;
using System.Net;
using System.Text;
using MergedCells.Core;

namespace MergedCells.Curation;

internal sealed record PageLevel(Level Level, int Score);

// TrapsThatDontMatter holds the map's trap squares whose removal alone changes nothing (TwistReliance.TrapsThatDontMatter)
internal sealed record PageCandidate(
    string File, ClimbedMap Climbed, StarsConfig? Limits, string PromoteCommand, IReadOnlyList<Cell> TrapsThatDontMatter);

// A concept batch's page shows the concept's name and its two layers as drawn
internal sealed record PageConcept(string Name, int? MaxWallRun, IReadOnlyList<string> Blocks, IReadOnlyList<string> Tiles);

// The page generate writes beside its candidates: one self-contained HTML file, no scripts, that opens from disk. The
// pictures follow chunks 18 and 19's sample pages, in the light board theme's colours
internal static class CandidatePage
{
    private const string Css = """
        body { font-family: Inter, "Segoe UI", sans-serif; background: #f4f8f7; color: #1d3b37; margin: 24px; }
        h1 { margin: 0 0 4px; font-size: 22px; }
        h2 { margin: 28px 0 10px; font-size: 17px; }
        .recipe { color: #4a6a66; margin: 0 0 4px; }
        .shipped { display: flex; flex-wrap: wrap; gap: 14px; align-items: flex-end; }
        .level { text-align: center; font-size: 12px; color: #4a6a66; }
        .candidates { display: flex; flex-wrap: wrap; gap: 20px; align-items: flex-start; }
        .candidate { background: #fff; border-radius: 12px; padding: 14px 16px; box-shadow: 0 1px 3px rgba(0, 0, 0, .08); }
        .candidate h3 { margin: 0 0 8px; font-size: 15px; }
        .numbers { border-collapse: collapse; font-size: 13px; margin: 8px 0; }
        .numbers td { padding: 1px 10px 1px 0; }
        .note { font-size: 13px; margin: 4px 0; }
        pre { background: #eef4f3; padding: 6px 8px; border-radius: 6px; font-size: 12px; white-space: pre-wrap; max-width: 420px; }
        .board { fill: #235E56; }
        .f0 { fill: #F2F5F5; }
        .f1 { fill: #E0E8E7; }
        .f2 { fill: #CFDCDA; }
        .f3 { fill: #BDCFCC; }
        .f4 { fill: #ABC2BF; }
        .wall { fill: #235E56; }
        .sticky { fill: #235E56; fill-opacity: 0.25; }
        .stripe, .trap { stroke: #235E56; stroke-linecap: round; }
        .letter { fill: #235E56; font-weight: 800; text-anchor: middle; dominant-baseline: central; }
        .start { fill: none; stroke: #235E56; }
        .end { fill: #235E56; }
        """;

    public static string Write(
        int chapter,
        string band,
        BandRecipe recipe,
        ulong seed,
        int climbs,
        IReadOnlyList<PageLevel> shipped,
        IReadOnlyList<PageCandidate> candidates,
        PageConcept? concept = null)
    {
        var title = concept is null
            ? $"Chapter {chapter} · {Encode(band)} · seed {seed}"
            : $"Concept {Encode(concept.Name)} · chapter {chapter} · {Encode(band)} · seed {seed}";
        var page = new StringBuilder();
        page.Append("<!DOCTYPE html>\n<html lang=\"en\"><head><meta charset=\"utf-8\">\n")
            .Append($"<title>{title}</title>\n<style>\n{Css}</style></head><body>\n")
            .Append($"<h1>{title}</h1>\n")
            .Append($"<p class=\"recipe\">{Encode(Describe(recipe))}</p>\n");
        if (concept is not null)
        {
            var wallRun = concept.MaxWallRun is { } run ? $"placed wall runs up to {run}" : "placed wall runs as the game config's";
            page.Append($"<p class=\"recipe\">Skeleton (blocks, then tiles), {wallRun}:</p>\n")
                .Append($"<pre>{Encode(string.Join("\n", concept.Blocks))}</pre>\n<pre>{Encode(string.Join("\n", concept.Tiles))}</pre>\n");
        }
        page.Append($"<p class=\"recipe\">{candidates.Count} of {climbs} climbs hit the target.</p>\n");

        page.Append("<h2>Already in the chapter</h2>\n<div class=\"shipped\">\n");
        foreach (var (level, score) in shipped)
        {
            page.Append($"<div class=\"level\">{Picture(level.Map, 140)}<br>{level.Id} · {Encode(level.Name)} · score {score}</div>\n");
        }
        page.Append("</div>\n<h2>Candidates</h2>\n<div class=\"candidates\">\n");
        foreach (var candidate in candidates)
        {
            page.Append(Card(candidate, recipe));
        }
        return page.Append("</div>\n</body></html>\n").ToString();
    }

    private static string Card(PageCandidate candidate, BandRecipe recipe)
    {
        var (map, solution, metrics, score, _, _) = candidate.Climbed;
        var rows = new List<(string Name, string Value)>
        {
            ("Size", $"{map.Rows}×{map.Columns}"),
            ("Par", Number(solution.Par!.Value)),
            ("Score", Number(score)),
            ("Away moves", Number(metrics.AwayMoves)),
            ("Retreat", Number(metrics.Retreat)),
            ("Greedy gap", metrics.GreedyGap is { } gap ? Number(gap) : "no greedy route"),
            ("Decoys", Number(metrics.Decoys)),
            ("Near-optimal routes", metrics.NearOptimalRoutes.ToString(CultureInfo.InvariantCulture)),
            ("Brute force", metrics.BruteForceExtraMoves is { } extra ? $"+{Number(extra)}" : "no lookahead player finishes"),
        };
        if (Squares(map, Tile.Wall) > 0)
        {
            rows.Add(("Wall stops on the best route", Number(WallStops.Count(map, solution.Path))));
        }
        if (Squares(map, Tile.Trap) is var traps and > 0)
        {
            rows.Add(("Traps that matter", TrapsThatMatter(traps, candidate.TrapsThatDontMatter)));
        }
        var card = new StringBuilder("<div class=\"candidate\">\n")
            .Append($"<h3>{Encode(candidate.File)}</h3>\n{Picture(map, 320)}\n<table class=\"numbers\">\n");
        foreach (var (name, value) in rows)
        {
            card.Append($"<tr><td>{name}</td><td>{Encode(value)}</td></tr>\n");
        }
        card.Append("</table>\n")
            .Append($"<p class=\"note\">{Encode(Twists(map, solution.Par!.Value))}</p>\n")
            .Append($"<p class=\"note\">{Encode(Limits(candidate.Limits, recipe, solution.Par!.Value))}</p>\n")
            .Append($"<pre>{Encode(candidate.PromoteCommand)}</pre>\n</div>\n");
        return card.ToString();
    }

    private static int Squares(Map map, Tile tile)
    {
        var count = 0;
        for (var row = 0; row < map.Rows; row++)
        {
            for (var column = 0; column < map.Columns; column++)
            {
                if (map.TileAt(new Cell(row, column)) == tile)
                {
                    count++;
                }
            }
        }
        return count;
    }

    // "3 of 4 (not row 2, column 5)", rows and columns from 1 as the concept files count them
    private static string TrapsThatMatter(int traps, IReadOnlyList<Cell> idle)
    {
        var count = $"{Number(traps - idle.Count)} of {Number(traps)}";
        return idle.Count == 0
            ? count
            : $"{count} (not {string.Join("; ", idle.Select(cell => $"row {Number(cell.Row + 1)}, column {Number(cell.Column + 1)}"))})";
    }

    private static string Describe(BandRecipe recipe)
    {
        var twists = recipe.Twists is { } t
            ? $"twists: walls {t.WallPercent}%, sticky {t.StickyPercent}%, traps {t.TrapPercent}%, teleporters {t.TeleporterPercent}%"
            : "no twists";
        var target = recipe.Target;
        var minimums = target.Minimums is { } m
            ? string.Join(", ", new[]
            {
                m.AwayMoves is { } a ? $"away moves ≥ {a}" : null,
                m.GreedyGap is { } g ? $"greedy gap ≥ {g}" : null,
                m.Decoys is { } d ? $"decoys ≥ {d}" : null,
                m.NearOptimalRoutes is { } n ? $"near-optimal routes ≥ {n}" : null,
                m.BruteForceExtraMoves is { } b ? $"brute force ≥ +{b}" : null,
            }.OfType<string>())
            : "";
        var reliance = target.MinTwistReliance > 0 ? $", twist reliance ≥ {target.MinTwistReliance}" : "";
        return $"Sizes {recipe.MinSize} to {recipe.MaxSize}, par {recipe.MinPar} to {recipe.MaxPar}, {twists}. "
            + $"Target score {target.MinScore} to {target.MaxScore}{(minimums.Length > 0 ? ", " + minimums : "")}{reliance}.";
    }

    private static string Twists(Map map, int par)
    {
        if (!map.HasTiles && map.Teleporters.Count == 0)
        {
            return "No twists.";
        }
        return Solver.Solve(TwistReliance.WithoutTwists(map)).Par is { } plainPar
            ? $"Without the twists par would be {plainPar} (it's {par})."
            : "Without the twists it can't be won.";
    }

    private static string Limits(StarsConfig? limits, BandRecipe recipe, int par)
    {
        if (limits is null)
        {
            return recipe.OneStarExtraMoves is null ? "Uses the chapter's star thresholds." : "No reachable star limits.";
        }
        return $"2 stars within par + {limits.TwoStarMinExtraMoves} ({par + limits.TwoStarMinExtraMoves} moves), "
            + $"1 star within par + {limits.OneStarMinExtraMoves} ({par + limits.OneStarMinExtraMoves} moves).";
    }

    // Squares in their region's colour, ink lines between regions, then the twists, the start (a ring) and the end (a dot)
    private static string Picture(Map map, int width)
    {
        var square = Math.Max(3.0, Math.Min(26.0, (double)width / map.Columns));
        var colours = RectangleColoring.Assign(map, RectangleColoring.MinimumColors);
        var colourOf = new Dictionary<int, int>();
        for (var i = 0; i < map.Rectangles.Count; i++)
        {
            colourOf[map.Rectangles[i].Id] = colours[i];
        }
        var gap = Math.Max(0.6, square * 0.08);
        var svg = new StringBuilder()
            .Append($"<svg width=\"{N(square * map.Columns)}\" height=\"{N(square * map.Rows)}\" viewBox=\"0 0 {N(square * map.Columns)} {N(square * map.Rows)}\">")
            .Append($"<rect class=\"board\" width=\"{N(square * map.Columns)}\" height=\"{N(square * map.Rows)}\"/>");
        for (var row = 0; row < map.Rows; row++)
        {
            for (var column = 0; column < map.Columns; column++)
            {
                var cell = new Cell(row, column);
                var region = map.RegionAt(cell);
                // Each square is inset only on the sides where its neighbour is in another region, so the ink shows there
                bool Other(Direction direction) => !map.Contains(cell.Step(direction)) || map.RegionAt(cell.Step(direction)) != region;
                var left = column * square + (Other(Direction.Left) ? gap : 0);
                var top = row * square + (Other(Direction.Up) ? gap : 0);
                var right = (column + 1) * square - (Other(Direction.Right) ? gap : 0);
                var bottom = (row + 1) * square - (Other(Direction.Down) ? gap : 0);
                svg.Append($"<rect class=\"f{colourOf[region]}\" x=\"{N(left)}\" y=\"{N(top)}\" width=\"{N(right - left)}\" height=\"{N(bottom - top)}\"/>");
            }
        }
        for (var row = 0; row < map.Rows; row++)
        {
            for (var column = 0; column < map.Columns; column++)
            {
                svg.Append(Twist(map, new Cell(row, column), square));
            }
        }
        for (var pair = 0; pair < map.Teleporters.Count; pair++)
        {
            foreach (var end in new[] { map.Teleporters[pair].First, map.Teleporters[pair].Second })
            {
                svg.Append($"<text class=\"letter\" x=\"{N((end.Column + 0.5) * square)}\" y=\"{N((end.Row + 0.5) * square)}\" font-size=\"{N(square * 0.7)}\">{TeleporterLetters.For(pair)}</text>");
            }
        }
        svg.Append($"<circle class=\"start\" cx=\"{N((map.Start.Column + 0.5) * square)}\" cy=\"{N((map.Start.Row + 0.5) * square)}\" r=\"{N(square * 0.28)}\" stroke-width=\"{N(Math.Max(1, square * 0.1))}\"/>");
        svg.Append($"<circle class=\"end\" cx=\"{N((map.End.Column + 0.5) * square)}\" cy=\"{N((map.End.Row + 0.5) * square)}\" r=\"{N(square * 0.3)}\"/>");
        return svg.Append("</svg>").ToString();
    }

    private static string Twist(Map map, Cell cell, double square)
    {
        var x = cell.Column * square;
        var y = cell.Row * square;
        var stroke = N(Math.Max(1, square * 0.1));
        switch (map.TileAt(cell))
        {
            case Tile.Wall:
                return $"<rect class=\"wall\" x=\"{N(x)}\" y=\"{N(y)}\" width=\"{N(square)}\" height=\"{N(square)}\"/>";
            case Tile.Sticky:
                var stripes = new StringBuilder($"<rect class=\"sticky\" x=\"{N(x)}\" y=\"{N(y)}\" width=\"{N(square)}\" height=\"{N(square)}\"/>");
                foreach (var step in new[] { 0.33, 0.66, 1.0 })
                {
                    stripes.Append($"<line class=\"stripe\" stroke-width=\"{stroke}\" x1=\"{N(x)}\" y1=\"{N(y + step * square)}\" x2=\"{N(x + step * square)}\" y2=\"{N(y)}\"/>");
                }
                return stripes.ToString();
            case Tile.Trap:
                var (near, far) = (square * 0.25, square * 0.75);
                return $"<line class=\"trap\" stroke-width=\"{stroke}\" x1=\"{N(x + near)}\" y1=\"{N(y + near)}\" x2=\"{N(x + far)}\" y2=\"{N(y + far)}\"/>"
                    + $"<line class=\"trap\" stroke-width=\"{stroke}\" x1=\"{N(x + far)}\" y1=\"{N(y + near)}\" x2=\"{N(x + near)}\" y2=\"{N(y + far)}\"/>";
            default:
                return "";
        }
    }

    private static string Encode(string text) => WebUtility.HtmlEncode(text);

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string N(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
