namespace MergedCells.Core;

public enum LibraryTag
{
    None,
    Yours,
    InYourLevels,

    // The phone's imported copy is older than the library's
    Update,
}

// What a library level's Get button does
public enum LibraryGet
{
    Get,
    Newer,

    // The phone already holds this version or a newer one, or it's the player's own there
    Have,
}

public static class LibraryTags
{
    /// <summary>
    /// The row's tag and its Get button, from what the phone holds. A level the server says is the player's own is "Yours";
    /// one missing from the phone can be got back, which restores it as their own.
    /// </summary>
    public static (LibraryTag Tag, LibraryGet Get) For(LibraryEntry entry, CustomLevelLoad load)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(load);
        var held = load.Levels.FirstOrDefault(level => level.Id == entry.Id);
        var newer = held is { IsImported: true } && entry.Level is { } level && level.Edited > held.Edited;
        var get = held is null ? LibraryGet.Get : newer ? LibraryGet.Newer : LibraryGet.Have;
        var tag = entry.Mine ? LibraryTag.Yours
            : held is null ? LibraryTag.None
            : newer ? LibraryTag.Update
            : LibraryTag.InYourLevels;
        return (tag, get);
    }
}
