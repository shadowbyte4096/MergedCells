using static MergedCells.Core.Tests.TestMaps;

namespace MergedCells.Core.Tests;

public class MapTeleportersTests
{
    [Fact]
    public void Teleporters_are_empty_on_a_plain_map()
    {
        Assert.Empty(Example().Teleporters);
    }

    [Fact]
    public void Teleporters_are_empty_when_given_an_empty_list()
    {
        Assert.Empty(Map.Create(ExampleRegions(), ExampleStart, ExampleEnd, teleporters: []).Teleporters);
    }

    [Fact]
    public void Teleporters_keep_the_pairs_in_order()
    {
        TeleporterPair[] pairs = [Pair(2, 2, 0, 1), Pair(1, 0, 1, 1)];

        Assert.Equal(pairs, Map.Create(ExampleRegions(), ExampleStart, ExampleEnd, teleporters: pairs).Teleporters);
    }

    [Fact]
    public void Teleporters_are_a_copy_of_the_callers_list()
    {
        TeleporterPair[] pairs = [Pair(0, 1, 2, 2)];
        var map = Map.Create(ExampleRegions(), ExampleStart, ExampleEnd, teleporters: pairs);

        pairs[0] = Pair(1, 0, 1, 1);

        Assert.Equal(Pair(0, 1, 2, 2), Assert.Single(map.Teleporters));
    }

    [Fact]
    public void Teleporters_cannot_be_changed_by_the_caller()
    {
        var teleporters = (IList<TeleporterPair>)Map.Create(ExampleRegions(), ExampleStart, ExampleEnd, teleporters: [Pair(0, 1, 2, 2)]).Teleporters;

        Assert.Throws<NotSupportedException>(() => teleporters[0] = Pair(1, 0, 1, 1));
    }
}
