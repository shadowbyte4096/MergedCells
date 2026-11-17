using System.Globalization;

namespace MergedCells.Core;

public enum LevelStatusKind
{
    Playable,
    NoRoute,
    Loops,
}

/// <summary>
/// Whether a custom level can be played, and the line the editor and the list show for it: par and band, or why not.
/// Loops names the first looping slide, the square it starts from and its direction.
/// </summary>
public sealed record LevelStatus(
    LevelStatusKind Kind, int? Par = null, int? Score = null, string? Band = null, Cell? LoopFrom = null, Direction? LoopDirection = null)
{
    public bool IsPlayable => Kind == LevelStatusKind.Playable;

    // Rows and columns from 1, as the candidates page counts them
    public string Describe() => Kind switch
    {
        LevelStatusKind.Playable => string.Create(CultureInfo.InvariantCulture, $"Par {Par} · {Band}"),
        LevelStatusKind.NoRoute => "No way to the end yet",
        _ => string.Create(
            CultureInfo.InvariantCulture,
            $"A slide from row {LoopFrom!.Value.Row + 1}, column {LoopFrom.Value.Column + 1} going {LoopDirection.ToString()!.ToLowerInvariant()} loops forever"),
    };
}
