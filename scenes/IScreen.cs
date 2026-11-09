namespace MergedCells;

// One of the screens Main shows one at a time
public interface IScreen
{
    bool Dark { set; }

    // Closes a sheet over the screen, if one is open, and says whether it did; Main's back handling asks this first
    bool CloseOverlay();
}
