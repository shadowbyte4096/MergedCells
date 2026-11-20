using MergedCells.Core;

namespace MergedCells;

// What the results card's chart shows: the server's answer as it comes, and the axis known before it (par, the save's best
// and the column count), so Play never sees a level ID or the save
public sealed record LeaderboardSource(SpreadFetch Fetch, SpreadAxis Axis);
