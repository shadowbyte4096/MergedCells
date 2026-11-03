namespace MergedCells.Core;

public sealed class InvalidMapException : Exception
{
    public InvalidMapException(IReadOnlyList<MapProblem> problems)
        : base(BuildMessage(problems))
    {
        Problems = problems;
    }

    public IReadOnlyList<MapProblem> Problems { get; }

    private static string BuildMessage(IReadOnlyList<MapProblem> problems)
    {
        ArgumentNullException.ThrowIfNull(problems);
        return "The map is invalid:\n" + string.Join("\n", problems.Select(problem => problem.Message));
    }
}
