namespace MergedCells.Core;

public static class Stars
{
    /// <summary>
    /// The most moves that still earn 2 stars: par plus the larger of the config's two-star minimum and percentage of par,
    /// rounded up. Throws <see cref="ArgumentOutOfRangeException"/> when par is below 1 or a config value is negative.
    /// </summary>
    public static int TwoStarLimit(int par, StarsConfig config)
    {
        Check(par, config);
        return Limit(par, config.TwoStarMinExtraMoves, config.TwoStarExtraParPercent);
    }

    /// <summary>
    /// The most moves that still earn 1 star, worked out the same way from the config's one-star values.
    /// Throws as <see cref="TwoStarLimit"/> does.
    /// </summary>
    public static int OneStarLimit(int par, StarsConfig config)
    {
        Check(par, config);
        return Limit(par, config.OneStarMinExtraMoves, config.OneStarExtraParPercent);
    }

    /// <summary>
    /// 3 stars at par, 2 within <see cref="TwoStarLimit"/>, 1 within <see cref="OneStarLimit"/>, otherwise 0: finishing
    /// alone earns nothing. Throws as <see cref="TwoStarLimit"/> does, and when moves are below par, which can only mean
    /// a wrong par.
    /// </summary>
    public static int Earned(int moves, int par, StarsConfig config)
    {
        Check(par, config);
        ArgumentOutOfRangeException.ThrowIfLessThan(moves, par);

        if (moves == par)
        {
            return 3;
        }
        if (moves <= TwoStarLimit(par, config))
        {
            return 2;
        }
        return moves <= OneStarLimit(par, config) ? 1 : 0;
    }

    private static int Limit(int par, int minExtra, int percent) => par + Math.Max(minExtra, (par * percent + 99) / 100);

    private static void Check(int par, StarsConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentOutOfRangeException.ThrowIfLessThan(par, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(config.TwoStarMinExtraMoves, nameof(config.TwoStarMinExtraMoves));
        ArgumentOutOfRangeException.ThrowIfNegative(config.TwoStarExtraParPercent, nameof(config.TwoStarExtraParPercent));
        ArgumentOutOfRangeException.ThrowIfNegative(config.OneStarMinExtraMoves, nameof(config.OneStarMinExtraMoves));
        ArgumentOutOfRangeException.ThrowIfNegative(config.OneStarExtraParPercent, nameof(config.OneStarExtraParPercent));
    }
}
