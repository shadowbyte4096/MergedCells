namespace MergedCells.Core.Tests;

// Chapter 1's levels as chunk 12 built them (copies in Fixtures/levels), measured with the test difficulty numbers, whose
// lookahead is chunk 12's 3 moves
public class Chunk12MeasureTests
{
    private static readonly DifficultyRater Rater = new(DifficultyConfigs.With());

    // Chunk 12's "Greedy par" and "Brute" columns in levels-and-campaign.md, minus par. The values are the point: they pin
    // these definitions to the measures chapter 1 was built with
    [Theory]
    [InlineData(11, 0, 0)]
    [InlineData(12, 0, 0)]
    [InlineData(13, 0, 0)]
    [InlineData(1, 0, 0)]
    [InlineData(2, 0, 0)]
    [InlineData(18, 0, 3)]
    [InlineData(19, 0, 3)]
    [InlineData(20, 2, 2)]
    [InlineData(21, 2, 3)]
    [InlineData(7, 2, 2)]
    [InlineData(23, 2, 3)]
    [InlineData(24, 2, 3)]
    [InlineData(25, 2, 3)]
    [InlineData(22, 2, 3)]
    [InlineData(8, 1, 4)]
    [InlineData(9, 1, 3)]
    [InlineData(10, null, null)]
    public void Metrics_match_chunk_12_s_greedy_par_and_brute_columns(int id, int? greedyGap, int? bruteForce)
    {
        var metrics = Rater.Measure(FixtureLevels.ById(id).Map)!;

        Assert.Equal((greedyGap, bruteForce), (metrics.GreedyGap, metrics.BruteForceExtraMoves));
    }
}
