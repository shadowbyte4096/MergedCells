namespace MergedCells;

// One of the screens Main shows one at a time
public interface IScreen
{
    bool Dark { set; }

    // Closes a sheet over the screen, if one is open, and says whether it did; Main's back handling asks this first
    bool CloseOverlay();

    // The screen's settings sheet, which Main wires when the screen opens; null for a screen without a gear
    SettingsSheet Settings => null;
}
