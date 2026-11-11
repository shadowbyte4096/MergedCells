namespace MergedCells.Core;

// One counted move, as the view animates it: a straight slide From to Reached, then, when a trap caught the player
// there, a jump to To (the start). Otherwise Reached is To.
public readonly record struct PlayerSlide(Direction Direction, Cell From, Cell Reached, Cell To, bool Wins)
{
    // A trap is never on the start, so the two differ exactly when one sent the player back
    public bool Trapped => Reached != To;

    // The straight slide only, so one of the two differences is always 0
    public int Squares => Math.Abs(Reached.Row - From.Row) + Math.Abs(Reached.Column - From.Column);
}
