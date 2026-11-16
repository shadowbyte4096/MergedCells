namespace MergedCells.Core;

/// <summary>
/// What <see cref="MapClimber.Climb"/> makes: maps from <paramref name="Map"/>'s generator request, pushed toward
/// <paramref name="Target"/> as scored in <paramref name="Mode"/>'s weights. With a <paramref name="Skeleton"/> (a
/// concept level's), every map keeps what it fixes, and the request's size must be the skeleton's.
/// </summary>
public sealed record ClimbRequest(
    MapRequest Map, DifficultyTarget Target, DifficultyMode Mode = DifficultyMode.Campaign, MapSkeleton? Skeleton = null);
