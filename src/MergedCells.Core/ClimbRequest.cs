namespace MergedCells.Core;

/// <summary>
/// What <see cref="MapClimber.Climb"/> makes: maps from <paramref name="Map"/>'s generator request, pushed toward
/// <paramref name="Target"/> as scored in <paramref name="Mode"/>'s weights.
/// </summary>
public sealed record ClimbRequest(MapRequest Map, DifficultyTarget Target, DifficultyMode Mode = DifficultyMode.Campaign);
