namespace MergedCells.Core;

public readonly record struct Cell(int Row, int Column)
{
    public Cell Step(Direction direction) => direction switch
    {
        Direction.Up => this with { Row = Row - 1 },
        Direction.Down => this with { Row = Row + 1 },
        Direction.Left => this with { Column = Column - 1 },
        Direction.Right => this with { Column = Column + 1 },
        _ => throw new ArgumentOutOfRangeException(nameof(direction)),
    };
}
