using MergedCells.Core;

namespace MergedCells;

// What the settings sheet shows when it opens: the appearance, each kind of data's switch, how many records the file holds,
// whether there's anything for "Delete my data", whether answers can change (not while the save can't be written), and why
// the last write failed, if it did, so an answer that wasn't kept says so
public sealed record SheetValues(Appearance Appearance, bool PlayData, bool Leaderboards, int Recorded, bool HasData, bool CanChange, string SaveProblem);
