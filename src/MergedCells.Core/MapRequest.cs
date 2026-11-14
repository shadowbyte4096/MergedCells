namespace MergedCells.Core;

/// <summary>
/// What <see cref="MapGenerator.Generate"/> makes: a <paramref name="Size"/> × <paramref name="Size"/> map (at least 2)
/// whose par is from <paramref name="MinPar"/> (at least 1) to <paramref name="MaxPar"/>. <paramref name="Bends"/> allows
/// thin L, T and zigzag regions; plain maps leave it off.
/// </summary>
public sealed record MapRequest(int Size, int MinPar, int MaxPar, bool Bends = false);
