namespace MergedCells.Core;

// Number counts from 1, in the chapter file's order; Levels are in play order. Intro is the one or two lines the chapter
// card shows when Next carries the player into this chapter, empty when the chapter file gives none.
public sealed record Chapter(int Number, string Name, IReadOnlyList<Level> Levels, IReadOnlyList<string> Intro);
