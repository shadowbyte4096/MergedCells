namespace MergedCells.Core;

/// <summary>The config's <c>customLevels</c> group: the level editor's sizes, names, undo limit and difficulty bands.</summary>
public sealed record CustomLevelsConfig
{
    // A new level's width and height to start from
    public required int NewSide { get; init; }

    // Each of the width and height, in squares
    public required int MinSide { get; init; }

    public required int MaxSide { get; init; }

    // In text elements, so an accent or an emoji counts as one
    public required int MaxNameLength { get; init; }

    public required int UndoLimit { get; init; }

    // In order of score; the first starts at 0
    public required IReadOnlyList<DifficultyBand> Bands { get; init; }

    /// <summary>
    /// Throws <see cref="ArgumentOutOfRangeException"/> naming the first value out of range. <see cref="GameConfig.CheckRanges"/>
    /// calls it at start-up.
    /// </summary>
    public void CheckRanges()
    {
        if (MinSide < 2)
        {
            throw new ArgumentOutOfRangeException("customLevels.minSide", MinSide, "Must be at least 2.");
        }
        if (NewSide < MinSide)
        {
            throw new ArgumentOutOfRangeException("customLevels.newSide", NewSide, "Must be at least minSide.");
        }
        if (MaxSide < NewSide || MaxSide > 100)
        {
            throw new ArgumentOutOfRangeException("customLevels.maxSide", MaxSide, "Must be from newSide to 100.");
        }
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxNameLength, 1, "customLevels.maxNameLength");
        ArgumentOutOfRangeException.ThrowIfNegative(UndoLimit, "customLevels.undoLimit");
        if (Bands.Count == 0 || Bands[0].MinScore != 0)
        {
            throw new ArgumentOutOfRangeException("customLevels.bands", "The first band must start at a score of 0.");
        }
        for (var index = 0; index < Bands.Count; index++)
        {
            if (string.IsNullOrWhiteSpace(Bands[index].Name))
            {
                throw new ArgumentOutOfRangeException("customLevels.bands", $"Band {index}'s name is blank.");
            }
            if (index > 0 && (Bands[index].MinScore <= Bands[index - 1].MinScore || Bands[index].MinScore > 100))
            {
                throw new ArgumentOutOfRangeException(
                    "customLevels.bands", Bands[index].MinScore, $"Band {index} must start above the band before it and at most at 100.");
            }
        }
    }

    /// <summary>The last band whose <see cref="DifficultyBand.MinScore"/> is at most the score.</summary>
    public DifficultyBand BandFor(int score) => Bands.Last(band => band.MinScore <= score);
}

public sealed record DifficultyBand
{
    // Shown to players, so it's display text
    public required string Name { get; init; }

    public required int MinScore { get; init; }
}
