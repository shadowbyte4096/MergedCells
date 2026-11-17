using System.Globalization;

namespace MergedCells.Core;

public enum ImportOutcome
{
    Added,
    Updated,
    Restored,
    Same,
    HaveNewer,
    YoursAlready,
    NoCode,
    Damaged,
    NewerGame,
    TooBig,
    CantBeWon,
    Loops,
    Unreadable,
    // The import brought a level, but writing its file failed
    NotSaved,
    // The custom levels folder couldn't be listed, so the level's file may be there
    FolderUnreadable,
}

/// <summary>
/// What a paste did. <paramref name="Level"/> is the level to write, only for <see cref="ImportOutcome.Added"/>,
/// <see cref="ImportOutcome.Updated"/> and <see cref="ImportOutcome.Restored"/>; every other outcome writes nothing.
/// </summary>
public sealed record ImportResult(ImportOutcome Outcome, CustomLevel? Level)
{
    // The import sheet's title
    public string Title => Outcome switch
    {
        ImportOutcome.Added => "Imported",
        ImportOutcome.Updated => "Updated",
        ImportOutcome.Restored => "Restored",
        ImportOutcome.Same or ImportOutcome.HaveNewer or ImportOutcome.YoursAlready => "Already here",
        ImportOutcome.NoCode => "No level code",
        ImportOutcome.Damaged => "Damaged code",
        ImportOutcome.NewerGame => "Update needed",
        ImportOutcome.TooBig => "Too big",
        ImportOutcome.CantBeWon or ImportOutcome.Loops => "Can't be played",
        ImportOutcome.NotSaved => "Not saved",
        _ => "Can't import",
    };

    // The import sheet's line under the title
    public string Describe(int maxSide) => Outcome switch
    {
        ImportOutcome.Added => "It's at the top of your list.",
        ImportOutcome.Updated => "Replaced with the newer version.",
        ImportOutcome.Restored => "One of your levels, back in your list.",
        ImportOutcome.Same => "You already have this version.",
        ImportOutcome.HaveNewer => "You already have a newer version.",
        ImportOutcome.YoursAlready => "That's one of your own levels.",
        ImportOutcome.NoCode => "Copy a level code, then tap Import.",
        ImportOutcome.Damaged => "That code is damaged. It may have been cut short.",
        ImportOutcome.NewerGame => "This level needs a newer version of Merged Cells.",
        ImportOutcome.TooBig => string.Create(CultureInfo.InvariantCulture, $"This level is bigger than {maxSide}×{maxSide}."),
        ImportOutcome.CantBeWon => "This level has no way to the end.",
        ImportOutcome.Loops => "A slide in this level loops forever.",
        ImportOutcome.NotSaved => "This level couldn't be saved.",
        ImportOutcome.FolderUnreadable => "Your levels can't be read right now, so nothing was imported.",
        _ => "A level with this ID is here but can't be read.",
    };

    // For a level whose file couldn't be written: the sheet then shows no row and no Play
    public ImportResult NotSaved() => new(ImportOutcome.NotSaved, null);
}

public static class LevelImport
{
    /// <summary>
    /// What importing the first share code in the clipboard's text does, given the levels on the device and the IDs of the
    /// player's own levels shared from it. Only levels that could be shared come in: winnable, no looping slide, no side
    /// over <see cref="CustomLevelsConfig.MaxSide"/>. A file the store couldn't read and the player's own levels are never
    /// written over. An imported level is stamped with <paramref name="now"/>, cut to whole seconds as the file keeps it.
    /// </summary>
    public static ImportResult Of(string clipboard, CustomLevelLoad load, IReadOnlySet<Guid> shared, CustomLevelsConfig config, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(clipboard);
        ArgumentNullException.ThrowIfNull(load);
        ArgumentNullException.ThrowIfNull(shared);
        ArgumentNullException.ThrowIfNull(config);
        var read = ShareCode.Read(clipboard);
        if (read.Level is not { } level)
        {
            return Refused(read.Problem switch
            {
                ShareCodeProblem.NewerGame => ImportOutcome.NewerGame,
                ShareCodeProblem.Damaged => ImportOutcome.Damaged,
                _ => ImportOutcome.NoCode,
            });
        }
        if (level.Map.Rows > config.MaxSide || level.Map.Columns > config.MaxSide)
        {
            return Refused(ImportOutcome.TooBig);
        }
        // Loops first, as LevelCheck orders them: a looping level is never played, even when it can be won
        var solved = Solver.Solve(level.Map);
        if (solved.LoopingSlides > 0)
        {
            return Refused(ImportOutcome.Loops);
        }
        if (solved.Par is null)
        {
            return Refused(ImportOutcome.CantBeWon);
        }
        if (!load.Listed)
        {
            return Refused(ImportOutcome.FolderUnreadable);
        }
        if (load.Unreadable?.Contains(level.Id) == true)
        {
            return Refused(ImportOutcome.Unreadable);
        }
        var stamp = WholeSeconds.Of(now);
        if (load.Levels.FirstOrDefault(held => held.Id == level.Id) is { } held)
        {
            if (!held.IsImported)
            {
                return Refused(ImportOutcome.YoursAlready);
            }
            return level.Edited > held.Edited ? new ImportResult(ImportOutcome.Updated, level with { Imported = stamp })
                : level.Edited == held.Edited ? Refused(ImportOutcome.Same)
                : Refused(ImportOutcome.HaveNewer);
        }
        return shared.Contains(level.Id)
            ? new ImportResult(ImportOutcome.Restored, level)
            : new ImportResult(ImportOutcome.Added, level with { Imported = stamp });
    }

    private static ImportResult Refused(ImportOutcome outcome) => new(outcome, null);
}
