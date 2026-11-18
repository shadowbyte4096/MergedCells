namespace MergedCells.Core;

// A play record written by a newer build: this build can't read it, so a reader skips it rather than calling it broken
public sealed class NewerPlayRecordException(int version)
    : Exception($"The play record is version {version}; this build reads up to version {PlayRecordFile.CurrentVersion}.")
{
    public int Version { get; } = version;
}
