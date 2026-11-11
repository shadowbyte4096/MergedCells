namespace MergedCells.Core;

// One straight stretch of a slide. A stretch that ends in a jump ends on the teleporter square the player entered, and the
// next starts on its partner
public readonly record struct SlideRun(Cell From, Cell To)
{
    // A run keeps one direction, so one of the two differences is always 0
    public int Squares => Math.Abs(To.Row - From.Row) + Math.Abs(To.Column - From.Column);
}
