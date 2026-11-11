namespace MergedCells.Core;

/// <summary>
/// The letter both squares of a teleporter pair show, by the pair's place in <see cref="Map.Teleporters"/>: the small
/// Greek letters, then the capitals. χ and Χ are left out because they look like a trap's ×, and Ο because it is ο
/// made bigger.
/// </summary>
public static class TeleporterLetters
{
    private const string Letters = "αβγδεζηθικλμνξοπρστυφψω" + "ΑΒΓΔΕΖΗΘΙΚΛΜΝΞΠΡΣΤΥΦΨΩ";

    public const int Count = 45;

    /// <summary>
    /// The pair's letter. Past <see cref="Count"/> pairs the letters start again. Throws
    /// <see cref="ArgumentOutOfRangeException"/> for a negative index.
    /// </summary>
    public static string For(int pairIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pairIndex);
        return Letters[pairIndex % Count].ToString();
    }
}
