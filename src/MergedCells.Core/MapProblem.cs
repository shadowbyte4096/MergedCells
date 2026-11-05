namespace MergedCells.Core;

// Map.Validate reports problems in this order; MapValidator must keep its checks in step
public enum MapProblemKind
{
    EmptyGrid,
    RegionNotConnected,
    StartOutsideMap,
    EndOutsideMap,
    EndNotSingleSquare,
    StartRightOfEnd,
    StartBelowEnd,
    StartOnEnd,
}

public sealed record MapProblem(MapProblemKind Kind, string Message);
