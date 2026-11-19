using AutoFixture;

namespace MergedCells.Core.Tests;

// Play records for tests: anonymous IDs, times and counts, on whatever map a test needs
internal static class PlayRecords
{
    private static readonly Fixture Fixture = new();

    private static int Count(int most) => Math.Abs(Fixture.Create<int>() % most);

    public static DifficultyMetrics Metrics(int par, bool nullGreedy = false, bool nullBruteForce = false) =>
        new(par, Count(5), Count(9), nullGreedy ? null : Count(5), Count(9), Count(500), nullBruteForce ? null : Count(6));

    public static PlayRecordStart Infinite(Map map, DifficultyMetrics? metrics = null)
    {
        var par = Solver.Solve(map).Par!.Value;
        return new PlayRecordStart(
            Fixture.Create<Guid>(),
            Fixture.Create<Guid>(),
            WholeSeconds.Of(Fixture.Create<DateTimeOffset>()),
            Fixture.Create<string>(),
            Fixture.Create<string>(),
            Count(5) + 1,
            DifficultyMode.Infinite,
            null,
            null,
            new InfiniteChoice(Count(10) + 1, map.Rows, Fixture.Create<bool>(), Fixture.Create<bool>(), Fixture.Create<bool>(), Fixture.Create<bool>()),
            null,
            map,
            par,
            DifficultyRater.MetricsVersion,
            Count(9) + 1,
            Count(3) + 1,
            metrics ?? Metrics(par),
            Count(101));
    }

    public static PlayRecordStart Campaign(Map map) =>
        Infinite(map) with { Mode = DifficultyMode.Campaign, Level = Count(500) + 1, SeenBefore = Fixture.Create<bool>(), Infinite = null };

    public static PlayRecordStart Custom(Map map, bool imported) =>
        Infinite(map) with { Mode = DifficultyMode.Custom, Infinite = null, Custom = new CustomPlay(Fixture.Create<Guid>(), imported) };

    public static PlayRecord Record(PlayRecordStart start) =>
        new(
            start,
            Fixture.Create<PlayOutcome>(),
            Count(50) + 1,
            Count(20),
            Count(5),
            Count(100_000),
            [
                new PlayAction(Count(1000), PlayActionKind.Right),
                new PlayAction(Count(1000) + 1000, PlayActionKind.Undo),
                new PlayAction(Count(1000) + 2000, PlayActionKind.Down),
                new PlayAction(Count(1000) + 3000, PlayActionKind.Restart),
            ]);
}
