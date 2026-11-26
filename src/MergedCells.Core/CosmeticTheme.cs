namespace MergedCells.Core;

/// <summary>A theme the shop sells (or the free default): a palette for light mode and one for dark.</summary>
public sealed record CosmeticTheme(string Id, string Name, Palette Light, Palette Dark)
{
    public Palette For(bool dark) => dark ? Dark : Light;
}
