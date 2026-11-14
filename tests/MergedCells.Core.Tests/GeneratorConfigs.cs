namespace MergedCells.Core.Tests;

// The generator's numbers for tests, independent of the shipped file so tuning it never breaks them
internal static class GeneratorConfigs
{
    public static GeneratorConfig With(
        int acrossWeight = 40,
        int downWeight = 40,
        int singleWeight = 10,
        int blockWeight = 10,
        int maxStripPercent = 100,
        int maxBlockSide = 3,
        int bendPercent = 35,
        int maxBendSquares = 8,
        int cornerPercent = 40,
        int minDistancePercent = 50,
        int maxAttempts = 200,
        int maxWallRun = 3) => new()
    {
        AcrossWeight = acrossWeight,
        DownWeight = downWeight,
        SingleWeight = singleWeight,
        BlockWeight = blockWeight,
        MaxStripPercent = maxStripPercent,
        MaxBlockSide = maxBlockSide,
        BendPercent = bendPercent,
        MaxBendSquares = maxBendSquares,
        CornerPercent = cornerPercent,
        MinDistancePercent = minDistancePercent,
        MaxAttempts = maxAttempts,
        MaxWallRun = maxWallRun,
    };
}
