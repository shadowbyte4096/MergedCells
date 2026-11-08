namespace MergedCells.Core;

// One finish of a level, compared with the best saved before it. PreviousBest and PreviousStars are null for a first
// finish, which includes a finish whose saved best was from a level that has since changed.
public sealed record LevelFinish(int Moves, int Stars, int? PreviousBest, int? PreviousStars)
{
    // Also whether the save changed and needs writing
    public bool IsNewBest => PreviousBest is null || Moves < PreviousBest;
}
