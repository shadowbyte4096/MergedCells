namespace MergedCells.Core;

// A save written by a newer build: this build can't read it, and mustn't overwrite it
public sealed class NewerSaveException(int version)
    : Exception($"The save is version {version}; this build reads up to version {SaveGame.CurrentVersion}.")
{
    public int Version { get; } = version;
}
