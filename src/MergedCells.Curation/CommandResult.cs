namespace MergedCells.Curation;

// What a command would print, and its exit code: 0 done, 1 refused or bad data, 2 a usage mistake. Commands return this
// rather than writing to the console, so the tests can run them
internal sealed record CommandResult(int ExitCode, string Output)
{
    public const int Done = 0;

    public const int Refused = 1;

    public const int UsageMistake = 2;
}
