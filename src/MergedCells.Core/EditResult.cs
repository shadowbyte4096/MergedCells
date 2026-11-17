namespace MergedCells.Core;

/// <summary>
/// What an edit did: a new valid map (<see cref="Changed"/>), the same map unchanged, or the same map with a short reason
/// for the player (<see cref="Refusal"/>).
/// </summary>
public sealed record EditResult(Map Map, bool Changed, string? Refusal)
{
    public static EditResult To(Map map) => new(map, true, null);

    public static EditResult Unchanged(Map map) => new(map, false, null);

    public static EditResult Refused(Map map, string reason) => new(map, false, reason);
}
