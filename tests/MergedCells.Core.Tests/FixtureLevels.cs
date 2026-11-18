namespace MergedCells.Core.Tests;

// Copies of the shipped levels that some algorithm pins were measured on, so retiring or editing a shipped level never
// breaks them. Only the copies matter here: they needn't match levels/ any more
internal static class FixtureLevels
{
    private static readonly Lazy<Level[]> All = new(() =>
        Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "levels"), "*.json")
            .Select(file => LevelFile.Parse(File.ReadAllText(file)))
            .ToArray());

    public static Level ById(int id) => All.Value.Single(level => level.Id == id);
}
