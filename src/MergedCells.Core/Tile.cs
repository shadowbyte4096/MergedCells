namespace MergedCells.Core;

// The numbers are the level file's tile codes (section 7 of the design doc)
public enum Tile : byte
{
    None = 0,
    Wall = 1,
    Sticky = 2,
    Trap = 3,
}
