namespace MergedCells.Core;

// What a player's own level's Publish button offers
public enum PublishState
{
    // Greyed, as Share code is: it can't be played, or no code can hold it
    NotPlayable,

    // Its code is longer than the library takes
    TooBig,

    // The server is off for the run
    Unavailable,

    Publish,

    // Published, and edited since
    Update,

    UpToDate,
}

public static class LibraryPublishing
{
    /// <summary>
    /// The Publish button for one of the player's own levels; imported levels can't be published, so asking for one
    /// throws. Copies of campaign levels and of other players' library levels are the server's to refuse, by layout.
    /// </summary>
    public static PublishState StateOf(CustomLevel level, LevelStatus status, SaveGame save, bool serverOn)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(save);
        if (level.IsImported)
        {
            throw new ArgumentException("An imported level can't be published.", nameof(level));
        }
        if (!status.IsPlayable || !ShareCode.CanWrite(level))
        {
            return PublishState.NotPlayable;
        }
        if (ShareCode.Write(level).Length > LibraryConfig.MaxCodeCharacters)
        {
            return PublishState.TooBig;
        }
        if (!serverOn)
        {
            return PublishState.Unavailable;
        }
        if (!save.Published.TryGetValue(level.Id, out var published))
        {
            return PublishState.Publish;
        }
        return level.Edited > published ? PublishState.Update : PublishState.UpToDate;
    }
}
