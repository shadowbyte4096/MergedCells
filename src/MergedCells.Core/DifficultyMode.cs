namespace MergedCells.Core;

/// <summary>
/// The modes that choose maps by difficulty, each of which may override the config's difficulty weights. Versus has no
/// difficulty setting.
/// </summary>
public enum DifficultyMode
{
    Campaign,
    Infinite,
    Daily,
    Timed,
}
