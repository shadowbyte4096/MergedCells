namespace MergedCells.Core;

// A level's on-screen text: one or two lines (the main line, then a smaller one), the lines desktop shows instead, and
// the direction of the ghost swipe touch screens show, if any
public sealed record Tutorial(IReadOnlyList<string> Lines, IReadOnlyList<string> KeyboardLines, Direction? SwipeHint);
