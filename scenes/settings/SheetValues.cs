using System;
using MergedCells.Core;

namespace MergedCells;

// What the settings sheet shows when it opens: the appearance, each kind of data's switch, how many records are waiting
// to be sent (the file is emptied once the server has them all), whether there's anything for "Delete my data", whether
// answers can change (not while the save can't be written), why the last write failed, if it did, so an answer that
// wasn't kept says so, and the player ID, for a player to quote when they email about their data
public sealed record SheetValues(Appearance Appearance, bool PlayData, bool Leaderboards, bool Library, int Waiting, bool HasData, bool CanChange, string SaveProblem, Guid? Player);
