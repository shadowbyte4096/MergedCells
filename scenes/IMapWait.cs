using System;

namespace MergedCells;

// A screen that waits while Main makes an infinite map in the background
public interface IMapWait
{
    // From the press on, the screen ignores its own input, with nothing changed on screen, so a quick map doesn't flicker
    void BeginWait();

    // Every frame once the map has taken longer than the config's delay: the bar shows and fills to progress, 0 to 1
    void ShowWait(double progress);

    // Back to normal, after Cancel
    void EndWait();

    event Action WaitCancelled;
}
