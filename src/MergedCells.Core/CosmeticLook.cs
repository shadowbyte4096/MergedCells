namespace MergedCells.Core;

/// <summary>
/// A player look: only the shape, drawn in the theme's player colour. Side, stroke and corner are fractions of a
/// square's side (a corner of half the side makes a circle); turn is whole degrees clockwise.
/// </summary>
public sealed record CosmeticLook(string Id, string Name, double Side, double Stroke, double Corner, bool Filled, int Turn);
