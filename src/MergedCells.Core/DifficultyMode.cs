namespace MergedCells.Core;

/// <summary>
/// The modes that rate maps by difficulty, each of which may override the config's difficulty weights. Custom levels are
/// rated in their own mode, for the band the editor and list show and for their play records. Versus has no difficulty
/// setting.
/// </summary>
public enum DifficultyMode
{
    Campaign,
    Infinite,
    Daily,
    Timed,
    Custom,
}
