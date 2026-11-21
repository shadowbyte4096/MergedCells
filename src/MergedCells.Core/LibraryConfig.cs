namespace MergedCells.Core;

/// <summary>The config's <c>library</c> group: how the online level library lists levels.</summary>
public sealed record LibraryConfig
{
    // Mirrors library_levels' cap in supabase/migrations: the server never returns more
    public const int MaxPageSize = 50;

    // Mirrors library_publish's cap: a longer code is refused, so the game never sends one
    public const int MaxCodeCharacters = 4096;

    public required int PageSize { get; init; }

    // The size filter's choices, by the larger side: up to the first step, each step after it, then up to maxSide
    public required IReadOnlyList<int> SizeSteps { get; init; }

    /// <summary>
    /// Throws <see cref="ArgumentOutOfRangeException"/> naming the first value out of range. <see cref="GameConfig.CheckRanges"/>
    /// calls it at start-up.
    /// </summary>
    public void CheckRanges(CustomLevelsConfig customLevels)
    {
        ArgumentNullException.ThrowIfNull(customLevels);
        if (PageSize is < 1 or > MaxPageSize)
        {
            throw new ArgumentOutOfRangeException("library.pageSize", PageSize, $"Must be from 1 to {MaxPageSize}.");
        }
        for (var index = 0; index < SizeSteps.Count; index++)
        {
            var step = SizeSteps[index];
            if (step < 1 || (index > 0 && step <= SizeSteps[index - 1]) || step >= customLevels.MaxSide)
            {
                throw new ArgumentOutOfRangeException(
                    "library.sizeSteps", step, "Steps must be at least 1, rise each time, and stay below customLevels.maxSide.");
            }
        }
    }
}
