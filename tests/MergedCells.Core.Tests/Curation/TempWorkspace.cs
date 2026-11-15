using System.Text.Json;
using System.Text.Json.Nodes;
using MergedCells.Curation;

namespace MergedCells.Core.Tests;

// A throwaway repository for the tool's commands, holding a small campaign of its own, so promoting real levels in
// 21b never changes what these tests expect. Only the config comes from the shipped file, so a config group added
// later is there too; its campaign overrides are cleared, and level 2 is given its own star limits.
//   Chapter 1 "One": levels 1 and 2. Chapter 2 "Two": levels 3 and 6. Chapter 3 "Three": level 4 alone.
//   ID 5 is retired, so the next ID is 7. The recipes name chapters 1 to 4 and give each a "medium" band.
internal sealed class TempWorkspace : IDisposable
{
    public const int NextId = 7;

    public const int ChapterCount = 3;

    public static readonly StarsConfig LevelTwoStars = new()
    {
        TwoStarMinExtraMoves = 2,
        TwoStarExtraParPercent = 0,
        OneStarMinExtraMoves = 4,
        OneStarExtraParPercent = 0,
    };

    public TempWorkspace()
    {
        Root = Path.Combine(Path.GetTempPath(), "mergedcells-curation-" + Guid.NewGuid().ToString("N"));
        Workspace = new Workspace(Root);
        Directory.CreateDirectory(Workspace.LevelsFolder);
        Directory.CreateDirectory(Path.GetDirectoryName(Workspace.ChaptersFile)!);
        Directory.CreateDirectory(Path.GetDirectoryName(Workspace.ConfigFile)!);
        Directory.CreateDirectory(Path.GetDirectoryName(Workspace.RecipesFile)!);

        Write(new Level(1, "Alpha", 1, TestMaps.Example(), 3));
        Write(new Level(2, "Beta", 1, ReachableMap(), 4));
        Write(new Level(3, "Gamma", 2, TestMaps.Example(), 3));
        Write(new Level(6, "Delta", 2, TestMaps.Example(), 3));
        Write(new Level(4, "Epsilon", 3, TestMaps.Example(), 3));
        File.WriteAllText(
            Workspace.ChaptersFile,
            ChapterFile.Write([new("One", [1, 2], []), new("Two", [3, 6], ["Two's intro."]), new("Three", [4], [])]));
        File.WriteAllText(Workspace.RetiredFile, RetiredLevels.Write([5]));
        File.WriteAllText(Workspace.ConfigFile, Config());
        WriteRecipes(Recipes());
    }

    public string Root { get; }

    public Workspace Workspace { get; }

    // Top Shelf's layout: 10 winning routes by par + 2, so a medium band's limits are 2 and 4 moves over par 4
    public static Map ReachableMap() => Map.Create(
        new[,]
        {
            { 1, 1, 2, 2, 2, 2 },
            { 1, 1, 3, 4, 5, 6 },
            { 7, 8, 3, 4, 5, 6 },
            { 7, 9, 3, 4, 5, 6 },
            { 7, 9, 10, 11, 5, 6 },
            { 7, 9, 10, 11, 5, 12 },
        },
        new(0, 1),
        new(5, 5));

    // One strip: every route past par goes back and forth, too few to reach 10
    public static Map UnreachableMap() => Map.Create(new[,] { { 1, 1, 1, 1, 2 } }, new(0, 0), new(0, 4));

    public void WriteRecipes(string json) => File.WriteAllText(Workspace.RecipesFile, json);

    // Every file the commands may change, by path from the root, so a refusal can be shown to have changed nothing
    public IReadOnlyDictionary<string, string> Snapshot() =>
        Directory.GetFiles(Root, "*", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.Combine(Root, "builds"), StringComparison.Ordinal))
            .ToDictionary(path => Path.GetRelativePath(Root, path), File.ReadAllText);

    public void Dispose()
    {
        // A test may have made a file read-only, which Directory.Delete refuses
        foreach (var file in Directory.GetFiles(Root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }
        Directory.Delete(Root, recursive: true);
    }

    private void Write(Level level) =>
        File.WriteAllText(Path.Combine(Workspace.LevelsFolder, CampaignFiles.FileNameFor(level.Name)), LevelFile.Write(level));

    // The shipped config with the campaign group's overrides replaced, laid out as the shipped file is: two-space
    // indents, and each levelStars entry on one line
    private static string Config()
    {
        var config = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "game_config.json")))!;
        var campaign = config["campaign"]!;
        campaign["gatePercent"] = 66;
        campaign["chapterGatePercent"] = new JsonObject();
        campaign["chapterStars"] = new JsonObject();
        campaign["levelStars"] = new JsonObject();
        var text = config.ToJsonString(new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n") + "\n";
        var emptied = text.Split('\n').Single(line => line.TrimStart().StartsWith("\"levelStars\": {}", StringComparison.Ordinal));
        var indent = emptied[..(emptied.Length - emptied.TrimStart().Length)];
        var stars = LevelTwoStars;
        return text.Replace(
            emptied,
            $"{indent}\"levelStars\": {{\n{indent}  \"2\": {{ \"twoStarMinExtraMoves\": {stars.TwoStarMinExtraMoves}, \"twoStarExtraParPercent\": {stars.TwoStarExtraParPercent}, \"oneStarMinExtraMoves\": {stars.OneStarMinExtraMoves}, \"oneStarExtraParPercent\": {stars.OneStarExtraParPercent} }}\n{indent}}}{(emptied.TrimEnd().EndsWith(',') ? "," : "")}");
    }

    private static string Recipes()
    {
        const string Medium =
            """{"sizes":[6,6],"par":[1,20],"twists":null,"candidates":2,"oneStarExtraMoves":2,"target":{"minScore":0,"maxScore":100}}""";
        var chapters = string.Join(
            ",", Enumerable.Range(1, 4).Select(number => $"\"{number}\":{{\"name\":\"Recipe {number}\",\"bends\":false,\"bands\":{{\"medium\":{Medium}}}}}"));
        return "{\"starLimits\":{\"minRoutes\":10,\"maxExtraMoves\":10},\"chapters\":{" + chapters + "}}";
    }
}
