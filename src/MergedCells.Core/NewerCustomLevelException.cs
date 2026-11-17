namespace MergedCells.Core;

// A custom level written by a newer build: this build can't read it, and mustn't overwrite it
public sealed class NewerCustomLevelException(int version)
    : Exception($"The custom level is version {version}; this build reads up to version {CustomLevelFile.CurrentVersion}.")
{
    public int Version { get; } = version;
}
