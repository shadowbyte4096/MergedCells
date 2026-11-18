using MergedCells.Core;

namespace MergedCells;

// What the settings sheet shows when it opens: the appearance, and on playtest builds the play-data switch and how many
// records the file holds
public sealed record SheetValues(Appearance Appearance, bool RecordPlayData, bool Playtest, int Recorded);
