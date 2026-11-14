namespace MergedCells.Core;

internal static class Percentage
{
    // Rounded up, in whole numbers, so the generator never depends on floating point
    public static int Of(int percent, int length) => (percent * length + 99) / 100;
}
