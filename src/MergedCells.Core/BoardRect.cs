using System.Numerics;

namespace MergedCells.Core;

// A rectangle in board units. System.Numerics has none
public readonly record struct BoardRect(float Left, float Top, float Width, float Height)
{
    public float Right => Left + Width;

    public float Bottom => Top + Height;

    public Vector2 Centre => new(Left + Width / 2, Top + Height / 2);
}
