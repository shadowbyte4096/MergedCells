namespace MergedCells.Core.Tests;

public class LevelCheckTests
{
    private static readonly DifficultyRater Rater = new(DifficultyConfigs.With());

    private static CustomLevelsConfig Config(params (string Name, int MinScore)[] bands) => new()
    {
        NewSide = 7,
        MinSide = 2,
        MaxSide = 30,
        MaxNameLength = 16,
        UndoLimit = 5,
        Bands = bands.Select(band => new DifficultyBand { Name = band.Name, MinScore = band.MinScore }).ToArray(),
    };

    private static readonly CustomLevelsConfig Shipped = Config(("Easy", 0), ("Medium", 20), ("Hard", 40), ("Extreme", 60), ("Insane", 80));

    [Fact]
    public void Playable_level_has_par_score_and_band()
    {
        var map = TestMaps.Example();
        var score = Rater.Score(Rater.Measure(map)!, DifficultyMode.Campaign);

        var status = LevelCheck.Of(map, Rater, Shipped);

        Assert.True(status.IsPlayable);
        Assert.Equal((3, score, Shipped.BandFor(score).Name), (status.Par, status.Score, status.Band));
    }

    // The second band starting at the level's own score, or one above it
    [Theory]
    [InlineData(0, "High")]
    [InlineData(1, "Low")]
    public void Band_follows_the_score(int aboveScore, string band)
    {
        var map = TestMaps.Example();
        var score = Rater.Score(Rater.Measure(map)!, DifficultyMode.Campaign);

        var status = LevelCheck.Of(map, Rater, Config(("Low", 0), ("High", score + aboveScore)));

        Assert.Equal(band, status.Band);
    }

    [Fact]
    public void Unwinnable_level_has_no_route()
    {
        var map = Map.Create(TestMaps.Grid("ABZ"), new(0, 0), new(0, 2), TestMaps.Walls(1, 3, (0, 1)));

        Assert.Equal(LevelStatusKind.NoRoute, LevelCheck.Of(map, Rater, Shipped).Kind);
    }

    // Down then right wins, but left from the start goes round the teleporters for ever
    [Fact]
    public void Looping_level_is_loops_even_when_winnable()
    {
        var status = LevelCheck.Of(TestMaps.LoopingLane(), Rater, Shipped);

        Assert.Equal((LevelStatusKind.Loops, new Cell(0, 2), Direction.Left), (status.Kind, status.LoopFrom, status.LoopDirection));
    }

    public static TheoryData<LevelStatus, string> Lines => new()
    {
        { new LevelStatus(LevelStatusKind.Playable, 6, 31, "Medium"), "Par 6 · Medium" },
        { new LevelStatus(LevelStatusKind.NoRoute), "No way to the end yet" },
        { new LevelStatus(LevelStatusKind.Loops, LoopFrom: new Cell(2, 4), LoopDirection: Direction.Right), "A slide from row 3, column 5 going right loops forever" },
    };

    [Theory]
    [MemberData(nameof(Lines))]
    public void Describe_gives_the_status_line(LevelStatus status, string line)
    {
        Assert.Equal(line, status.Describe());
    }
}
