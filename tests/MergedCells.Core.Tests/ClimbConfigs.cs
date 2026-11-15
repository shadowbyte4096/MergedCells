namespace MergedCells.Core.Tests;

// The climber's numbers for tests, independent of the shipped file so tuning it never breaks them
internal static class ClimbConfigs
{
    public static ClimbConfig With(
        int maxSteps = 4000,
        int stallSteps = 500,
        int splitWeight = 20,
        int mergeWeight = 20,
        int moveSquareWeight = 30,
        int moveStartWeight = 10,
        int moveTwistWeight = 20) => new()
    {
        MaxSteps = maxSteps,
        StallSteps = stallSteps,
        SplitWeight = splitWeight,
        MergeWeight = mergeWeight,
        MoveSquareWeight = moveSquareWeight,
        MoveStartWeight = moveStartWeight,
        MoveTwistWeight = moveTwistWeight,
    };
}
