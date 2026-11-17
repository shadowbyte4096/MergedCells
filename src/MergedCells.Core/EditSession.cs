namespace MergedCells.Core;

/// <summary>
/// One level open in the editor: the level as it stands, its undo history and the clock its edits are stamped with.
/// Each change returns the level to write, or null when nothing changed. A rename isn't an undo step.
/// </summary>
public sealed class EditSession(CustomLevel level, Func<DateTimeOffset> now)
{
    // Imported levels are edited only as a copy, which becomes the player's own
    private readonly EditHistory history = new(
        level is null ? throw new ArgumentNullException(nameof(level))
        : level.IsImported ? throw new ArgumentException("An imported level is edited only as a copy.", nameof(level))
        : level.Map);
    private readonly Func<DateTimeOffset> now = now ?? throw new ArgumentNullException(nameof(now));

    public CustomLevel Level { get; private set; } = level;

    public bool CanUndo => history.CanUndo;

    public bool CanRedo => history.CanRedo;

    public CustomLevel? Apply(EditResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.Changed)
        {
            return null;
        }
        history.Apply(result.Map);
        return Stamp(Level with { Map = result.Map });
    }

    public CustomLevel? Undo() => history.Undo() ? Stamp(Level with { Map = history.Current }) : null;

    public CustomLevel? Redo() => history.Redo() ? Stamp(Level with { Map = history.Current }) : null;

    // The name already cleaned by CustomLevelNames.Clean
    public CustomLevel? Rename(string cleanName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cleanName);
        return cleanName == Level.Name ? null : Stamp(Level with { Name = cleanName });
    }

    // Whole seconds, as the file keeps them
    private CustomLevel Stamp(CustomLevel changed)
    {
        Level = changed with { Edited = WholeSeconds.Of(now()) };
        return Level;
    }
}
