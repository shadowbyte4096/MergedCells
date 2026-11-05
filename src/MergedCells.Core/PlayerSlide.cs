namespace MergedCells.Core;

// One counted move, as the view animates it
public readonly record struct PlayerSlide(Cell From, Cell To, bool Wins)
{
    // Slides are straight, so one of the two differences is always 0
    public int Squares => Math.Abs(To.Row - From.Row) + Math.Abs(To.Column - From.Column);
}
