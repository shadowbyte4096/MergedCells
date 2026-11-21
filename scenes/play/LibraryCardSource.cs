using System;
using MergedCells.Core;

namespace MergedCells;

// What the results card's library box shows: the finish's answer as it comes (the counts), and how to like or unlike the
// level, so Play never sees a level ID or the queue
public sealed record LibraryCardSource(CallHandle<LevelCounts> Finish, Func<bool, CallHandle<LevelCounts>> Like);
