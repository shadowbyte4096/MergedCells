using AutoFixture;
using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class StartAndEndTests
{
    private const int CornerPercent = 40;
    private const int MinDistancePercent = 50;

    public static TheoryData<int> Sizes => new(2, 3, 5, 10, 17, 100);

    // 200 placements on one size, each from its own seed
    private static List<(Cell Start, Cell End)> Placements(int size)
    {
        var config = GeneratorConfigs.With(cornerPercent: CornerPercent, minDistancePercent: MinDistancePercent);
        return new Fixture().CreateMany<ulong>(200).Select(seed => StartAndEnd.Place(size, config, new GameRandom(seed))).ToList();
    }

    private static int AreaSide(int size) => Math.Max(1, Percentage.Of(CornerPercent, size));

    [Theory]
    [MemberData(nameof(Sizes))]
    public void Start_is_in_the_top_left_area(int size)
    {
        var side = AreaSide(size);

        Assert.All(Placements(size), pair => Assert.True(pair.Start.Row < side && pair.Start.Column < side, $"{pair}"));
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void End_is_in_the_bottom_right_area(int size)
    {
        var first = size - AreaSide(size);

        Assert.All(Placements(size), pair => Assert.True(pair.End.Row >= first && pair.End.Column >= first, $"{pair}"));
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void Start_and_end_are_at_least_the_minimum_distance_apart(int size)
    {
        var distance = Math.Max(1, Percentage.Of(MinDistancePercent, 2 * (size - 1)));

        Assert.All(
            Placements(size),
            pair => Assert.True(pair.End.Row - pair.Start.Row + pair.End.Column - pair.Start.Column >= distance, $"{pair}"));
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void Start_is_never_right_of_below_or_on_the_end(int size)
    {
        Assert.All(
            Placements(size),
            pair => Assert.True(
                pair.Start.Row <= pair.End.Row && pair.Start.Column <= pair.End.Column && pair.Start != pair.End, $"{pair}"));
    }

    [Theory]
    [InlineAutoData(2)]
    [InlineAutoData(7)]
    [InlineAutoData(30)]
    public void Full_areas_and_full_distance_give_the_far_corners(int size, ulong seed)
    {
        var config = GeneratorConfigs.With(cornerPercent: 100, minDistancePercent: 100);

        var pair = StartAndEnd.Place(size, config, new GameRandom(seed));

        Assert.Equal((new Cell(0, 0), new Cell(size - 1, size - 1)), pair);
    }

    [Theory]
    [AutoData]
    public void One_seed_gives_one_pair(ulong seed)
    {
        var config = GeneratorConfigs.With();

        Assert.Equal(StartAndEnd.Place(12, config, new GameRandom(seed)), StartAndEnd.Place(12, config, new GameRandom(seed)));
    }
}
