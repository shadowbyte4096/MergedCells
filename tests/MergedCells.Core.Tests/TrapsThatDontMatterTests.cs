using static MergedCells.Core.Tests.TestMaps;

namespace MergedCells.Core.Tests;

public class TrapsThatDontMatterTests
{
    private static readonly DifficultyRater Rater = new(DifficultyConfigs.With());

    // Right from the start runs over the lane's last square and, without the trap there, stops one square on at [0, 5],
    // a slower way to the end than down then right
    private static Map TrapOnTheSlowerRoute() => Map.Create(Grid("ABBBBC", "DDDDDZ"), new(0, 0), new(1, 5), Traps(2, 6, (0, 4)));

    // Right from the start enters [0, 1] and comes out of [1, 4] still sliding right, into the end: par 1. The covering
    // traps only catch the slide from [1, 0] that jumps back up into the lane; without the pair par would be 2
    private static Map CoveringTrapsBesideAPair() =>
        Map.Create(Grid("ABBBBC", "DDDDDZ"), new(0, 0), new(1, 5), Traps(2, 6, (0, 2), (0, 3)), [Pair(0, 1, 1, 4)]);

    [Fact]
    public void Map_without_traps_has_none()
    {
        Assert.Empty(TwistReliance.TrapsThatDontMatter(Example(), Rater));
    }

    [Fact]
    public void Trap_that_closes_the_quick_route_matters()
    {
        Assert.Empty(TwistReliance.TrapsThatDontMatter(TrapTutorial(), Rater));
    }

    [Fact]
    public void Trap_whose_removal_changes_nothing_is_listed()
    {
        Assert.Equal([new Cell(0, 4)], TwistReliance.TrapsThatDontMatter(TrapOnTheSlowerRoute(), Rater));
    }

    // The trap in the top lane closes the quick route; the one on the bottom row's first square only closes one of two
    // 3-move routes
    [Fact]
    public void Only_the_trap_that_changes_nothing_is_listed_beside_one_that_matters()
    {
        var map = Map.Create(Grid("AAAAB", "CCCCB", "DDDDE"), new(0, 0), new(2, 4), Traps(3, 5, (0, 2), (2, 0)));

        Assert.Equal([new Cell(2, 0)], TwistReliance.TrapsThatDontMatter(map, Rater));
    }

    [Fact]
    public void Traps_that_cover_each_other_are_both_listed_in_reading_order()
    {
        Assert.Equal([new Cell(0, 2), new Cell(0, 3)], TwistReliance.TrapsThatDontMatter(CoveringTraps(), Rater));
    }

    // Dropping the pairs with the trap would change par from 1 to 2 and make every trap look as if it mattered
    [Fact]
    public void Teleporter_pairs_stay_when_a_trap_is_taken_off()
    {
        Assert.Equal([new Cell(0, 2), new Cell(0, 3)], TwistReliance.TrapsThatDontMatter(CoveringTrapsBesideAPair(), Rater));
    }

    // Every way out of the start's two stops runs into a trap
    [Fact]
    public void Unwinnable_map_throws()
    {
        var map = Map.Create(Grid("ABB", "CCZ"), new(0, 0), new(1, 2), Traps(2, 3, (0, 2), (1, 1)));

        Assert.Throws<ArgumentException>(() => TwistReliance.TrapsThatDontMatter(map, Rater));
    }
}
