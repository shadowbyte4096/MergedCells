namespace MergedCells.Core;

public enum LibraryOrder
{
    Newest,
    Played,
    Liked,
}

/// <summary>
/// What the library screen asks for: an order, and optionally one of the custom levels' difficulty bands (by index in
/// <see cref="CustomLevelsConfig.Bands"/>) and one size step (by index of the size filter's choices).
/// </summary>
public sealed record LibraryQuery(LibraryOrder Order, int? Band = null, int? SizeStep = null)
{
    /// <summary>The band's scores: from its minimum to just below the next band's, the last up to 100. Null for any.</summary>
    public (int Min, int Max)? ScoreRange(CustomLevelsConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (Band is not { } band)
        {
            return null;
        }
        var max = band + 1 < config.Bands.Count ? config.Bands[band + 1].MinScore - 1 : 100;
        return (config.Bands[band].MinScore, max);
    }

    /// <summary>The step's range of the larger side: up to the first step, each step after it, then up to maxSide. Null for any.</summary>
    public (int Min, int Max)? SideRange(LibraryConfig library, CustomLevelsConfig customLevels)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(customLevels);
        if (SizeStep is not { } step)
        {
            return null;
        }
        var min = step == 0 ? 1 : library.SizeSteps[step - 1] + 1;
        var max = step < library.SizeSteps.Count ? library.SizeSteps[step] : customLevels.MaxSide;
        return (min, max);
    }
}
