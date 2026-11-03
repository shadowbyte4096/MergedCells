namespace MergedCells.Core.Tests;

// A made-up slide rule for the solver's test hook. Any move not recorded is a no-op.
internal sealed class MoveRule
{
    private readonly Dictionary<(Cell From, Direction Direction), SlideResult> moves = new();

    public MoveRule Slide(Cell from, Direction direction, Cell to)
    {
        moves[(from, direction)] = new SlideResult(to, false);
        return this;
    }

    // The stop is the square the slide started from, so a test passes only if the win comes from the flag
    public MoveRule Win(Cell from, Direction direction)
    {
        moves[(from, direction)] = new SlideResult(from, true);
        return this;
    }

    public SlideResult Apply(Cell from, Direction direction) =>
        moves.TryGetValue((from, direction), out var result) ? result : new SlideResult(from, false);
}
