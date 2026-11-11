namespace MergedCells.Core;

// One counted move, as the view animates it: its straight runs in order (more than one when it went through a
// teleporter), then, when a trap caught the player at the end of the last run, a jump to To (the start). Otherwise the
// last run ends on To.
public readonly record struct PlayerSlide(Direction Direction, IReadOnlyList<SlideRun> Runs, Cell To, bool Wins)
{
    public IReadOnlyList<SlideRun> Runs { get; } = Runs is null
        ? throw new ArgumentNullException(nameof(Runs))
        : Runs.Count > 0 ? Runs : throw new ArgumentException("A slide has at least one run.", nameof(Runs));

    public Cell From => Runs[0].From;

    public Cell Reached => Runs[^1].To;

    // A trap is never on the start, so the two differ exactly when one sent the player back
    public bool Trapped => Reached != To;

    // Every run's squares, not counting the jumps between them
    public int Squares
    {
        get
        {
            var squares = 0;
            foreach (var run in Runs)
            {
                squares += run.Squares;
            }
            return squares;
        }
    }

    // By the runs' contents, so slides built from different lists compare equal
    public bool Equals(PlayerSlide other) =>
        Direction == other.Direction && To == other.To && Wins == other.Wins
        && (ReferenceEquals(Runs, other.Runs) || (Runs is not null && other.Runs is not null && Runs.SequenceEqual(other.Runs)));

    public override int GetHashCode() =>
        Runs is null or { Count: 0 } ? HashCode.Combine(Direction, To, Wins) : HashCode.Combine(Direction, To, Wins, Runs.Count, Runs[0], Runs[^1]);
}
