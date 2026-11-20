using System;
using MergedCells.Core;

namespace MergedCells;

// One map's pieces for Play.Start, so a mode adds a field here rather than another parameter
public sealed record PlaySetup(string Title)
{
    // Shown before the moves, such as an infinite map's size; null shows the moves alone
    public string Detail { get; init; }

    // The level's star thresholds, or null for a card without stars
    public StarsConfig Stars { get; init; }

    // Asked when the results card appears; null hides Next
    public Func<bool> HasNext { get; init; }

    public string NextText { get; init; } = "Next";

    // Called with the move count on a win, returning how it compares with the saved best; null records nothing
    public Func<int, LevelFinish> RecordFinish { get; init; }

    // Null is unlimited undo
    public PlayRules Rules { get; init; }

    public Tutorial Tutorial { get; init; }

    // Shows the New map button
    public bool NewMap { get; init; }

    // Whether the results card shows "Everyone's best moves", asked as the card appears; null hides it
    public Func<bool> Leaderboard { get; init; }

    // Starts each visit's play record (at the map's start and at each Retry); null records nothing
    public Func<PlayRecorder> Recording { get; init; }
}
