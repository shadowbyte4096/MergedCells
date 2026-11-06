using System.Numerics;

namespace MergedCells.Core;

// Focus: the board point shown at the middle of the band between the HUD's bars. Zoom: viewport units per board unit
public readonly record struct CameraView(Vector2 Focus, float Zoom);
