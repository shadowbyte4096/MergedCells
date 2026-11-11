using System.Text.Json;
using static MergedCells.Core.Tests.TestMaps;

namespace MergedCells.Core.Tests;

public class LevelFileTeleportersTests
{
    // The example level in section 7 of the design doc, with its teleporters swapped in
    private const string Level =
        """{"id":3,"name":"Linked","chapter":1,"regions":[[1,1,2,3],[4,5,2,3],[4,5,6,6],[7,7,7,8]],"tiles":null,"teleporters":TELEPORTERS,"start":[0,0],"end":[3,3],"par":3}""";

    private static string WithTeleporters(string teleporters) => Level.Replace("TELEPORTERS", teleporters);

    [Fact]
    public void Pairs_are_read_in_file_order()
    {
        var map = LevelFile.Parse(WithTeleporters("[[[0,1],[3,2]],[[1,1],[2,0]]]")).Map;

        Assert.Equal([Pair(0, 1, 3, 2), Pair(1, 1, 2, 0)], map.Teleporters);
    }

    public static TheoryData<string> NoPairs => new()
    {
        WithTeleporters("null"),
        WithTeleporters("[]"),
        Level.Replace("\"teleporters\":TELEPORTERS,", ""),
    };

    [Theory]
    [MemberData(nameof(NoPairs))]
    public void No_pairs_when_null_empty_or_left_out(string text)
    {
        Assert.Empty(LevelFile.Parse(text).Map.Teleporters);
    }

    [Theory]
    [InlineData("[[[0,1],[3,2]],null]")]
    [InlineData("[[[0,1],[3,2]],[[1,1]]]")]
    [InlineData("[[[0,1],[3,2]],[[1,1],[2,0],[2,1]]]")]
    [InlineData("[[[0,1],[3,2]],[null,[2,0]]]")]
    [InlineData("[[[0,1],[3,2]],[[1],[2,0]]]")]
    [InlineData("[[[0,1],[3,2]],[[1,1],[2,0,0]]]")]
    public void Malformed_pair_is_a_JsonException_naming_it(string teleporters)
    {
        var ex = Assert.Throws<JsonException>(() => LevelFile.Parse(WithTeleporters(teleporters)));

        Assert.Contains("pair 1", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Pair_on_the_start_is_an_InvalidMapException()
    {
        var ex = Assert.Throws<InvalidMapException>(() => LevelFile.Parse(WithTeleporters("[[[0,0],[2,0]]]")));

        Assert.Equal(MapProblemKind.TeleporterOnStart, Assert.Single(ex.Problems).Kind);
    }
}
