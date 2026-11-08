namespace MergedCells.Core;

// How a results star is drawn: New was earned for the first time, Ghost was earned before but not this time
public enum StarLook
{
    Empty,
    Earned,
    New,
    Ghost,
}

public static class StarLooks
{
    /// <summary>The three stars' looks, left to right, for a finish that earned <paramref name="earned"/> stars when the
    /// best before it had earned <paramref name="previousStars"/>. Throws when either is outside 0 to 3.</summary>
    public static IReadOnlyList<StarLook> For(int earned, int previousStars)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(earned);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(earned, 3);
        ArgumentOutOfRangeException.ThrowIfNegative(previousStars);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(previousStars, 3);

        var looks = new StarLook[3];
        for (var i = 0; i < looks.Length; i++)
        {
            var hadIt = i < previousStars;
            looks[i] = i < earned
                ? (hadIt ? StarLook.Earned : StarLook.New)
                : (hadIt ? StarLook.Ghost : StarLook.Empty);
        }
        return looks;
    }
}
