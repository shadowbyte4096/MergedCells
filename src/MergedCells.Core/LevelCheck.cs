namespace MergedCells.Core;

public static class LevelCheck
{
    /// <summary>
    /// Whether the level can be played. A looping slide is a silent no-op in play, so a level with one is never played,
    /// even when it can be won; the first one in the solver's order of stops and directions is named. Otherwise a level the
    /// solver can't win has no route, and a playable one gets par, its score with the campaign's weights (a custom level is
    /// judged like a campaign level) and its band.
    /// </summary>
    public static LevelStatus Of(Map map, DifficultyRater rater, CustomLevelsConfig config)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(rater);
        ArgumentNullException.ThrowIfNull(config);
        var solved = Solver.Solve(map);
        if (solved.LoopingSlides > 0)
        {
            var graph = MoveGraph.Build(map.Start, map.Slide);
            for (var stop = 0; stop < graph.StopCount; stop++)
            {
                foreach (var direction in Solver.Directions)
                {
                    if (map.Slide(graph.CellOf(stop), direction).Looped)
                    {
                        return new LevelStatus(LevelStatusKind.Loops, LoopFrom: graph.CellOf(stop), LoopDirection: direction);
                    }
                }
            }
        }
        if (solved.Par is not { } par)
        {
            return new LevelStatus(LevelStatusKind.NoRoute);
        }
        var score = rater.Score(rater.Measure(map)!, DifficultyMode.Campaign);
        return new LevelStatus(LevelStatusKind.Playable, par, score, config.BandFor(score).Name);
    }
}
