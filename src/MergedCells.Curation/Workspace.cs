namespace MergedCells.Curation;

// The repository files the tool reads and writes, from its root folder (the one holding MergedCells.sln)
internal sealed class Workspace(string root)
{
    public string Root { get; } = Path.GetFullPath(root);

    public string LevelsFolder => Path.Combine(Root, "levels");

    public string ChaptersFile => Path.Combine(Root, "campaign", "chapters.json");

    public string ConfigFile => Path.Combine(Root, "config", "game_config.json");

    public string RecipesFile => Path.Combine(Root, "src", "MergedCells.Curation", "recipes.json");

    public string RetiredFile => Path.Combine(Root, "src", "MergedCells.Curation", "retired-levels.json");

    public string ConceptsFolder => Path.Combine(Root, "src", "MergedCells.Curation", "concepts");

    public string CandidatesFolder => Path.Combine(Root, "builds", "candidates");

    public string RetiredBackupFolder => Path.Combine(Root, "builds", "retired");

    // For messages: the path from the root, with forward slashes, as the developer would type it
    public string Relative(string path) => Path.GetRelativePath(Root, path).Replace('\\', '/');

    public static string? FindRoot(string start)
    {
        for (var folder = new DirectoryInfo(Path.GetFullPath(start)); folder != null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "MergedCells.sln")))
            {
                return folder.FullName;
            }
        }
        return null;
    }
}
