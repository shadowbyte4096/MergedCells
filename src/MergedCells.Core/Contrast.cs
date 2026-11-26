namespace MergedCells.Core;

/// <summary>WCAG 2's contrast ratio between two colours, from 1 (the same) to 21 (black on white).</summary>
public static class Contrast
{
    public static double Ratio(Rgb a, Rgb b)
    {
        var first = Luminance(a);
        var second = Luminance(b);
        return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
    }

    private static double Luminance(Rgb colour) =>
        0.2126 * Linear(colour.R) + 0.7152 * Linear(colour.G) + 0.0722 * Linear(colour.B);

    private static double Linear(byte channel)
    {
        var value = channel / 255.0;
        return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
