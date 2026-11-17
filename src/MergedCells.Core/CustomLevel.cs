namespace MergedCells.Core;

/// <summary>
/// A level a player made in the editor, or imported from a share code. The ID is a random GUID made when the level is,
/// kept through edits and imports, and never a campaign ID. There's no par: the solver works it out whenever it's needed.
/// <paramref name="Imported"/> is when the level was imported or last updated by an import, and null for the player's
/// own levels (made, copied or restored here); chunk 27 pays coins only for imported ones.
/// </summary>
public sealed record CustomLevel(Guid Id, string Name, Map Map, DateTimeOffset Edited, DateTimeOffset? Imported = null)
{
    public bool IsImported => Imported is not null;
}
