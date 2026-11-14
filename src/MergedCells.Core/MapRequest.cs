namespace MergedCells.Core;

/// <summary>
/// What <see cref="MapGenerator.Generate"/> makes: a <paramref name="Size"/> × <paramref name="Size"/> map (at least 2)
/// whose par is from <paramref name="MinPar"/> (at least 1) to <paramref name="MaxPar"/>. <paramref name="Bends"/> allows
/// thin L, T and zigzag regions; plain maps leave it off. <paramref name="Twists"/> says how many walls, sticky squares,
/// traps and teleporter pairs to place; null, or every density 0, is a plain map.
/// </summary>
public sealed record MapRequest(int Size, int MinPar, int MaxPar, bool Bends = false, TwistDensities? Twists = null);
