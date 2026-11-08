namespace MergedCells.Core.Tests;

// Star counts are the point here, so these tests use literal values
public class StarLooksTests
{
    private const StarLook E = StarLook.Earned;
    private const StarLook N = StarLook.New;
    private const StarLook G = StarLook.Ghost;
    private const StarLook O = StarLook.Empty;

    public static TheoryData<int, int, StarLook[]> Cases => new()
    {
        { 3, 2, [E, E, N] },
        { 1, 3, [E, G, G] },
        { 2, 0, [N, N, O] },
        { 0, 0, [O, O, O] },
        { 3, 3, [E, E, E] },
        { 0, 2, [G, G, O] },
        { 2, 1, [E, N, O] },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Looks_compare_this_finish_with_the_best_before_it(int earned, int previous, StarLook[] expected)
    {
        Assert.Equal(expected, StarLooks.For(earned, previous));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(4, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 4)]
    public void Count_outside_0_to_3_throws(int earned, int previous)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StarLooks.For(earned, previous));
    }
}
