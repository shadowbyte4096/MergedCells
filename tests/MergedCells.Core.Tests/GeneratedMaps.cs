namespace MergedCells.Core.Tests;

// Generated maps for tests that need a realistic map rather than a hand-made one, from the tests' own generator numbers
internal static class GeneratedMaps
{
    public static readonly TwistDensities EveryTwist = new(6, 4, 3, 2);

    public static Map Make(MapRequest request, ulong seed)
    {
        var generated = new MapGenerator(GeneratorConfigs.With()).Generate(request, seed);
        Assert.NotNull(generated);
        return generated.Map;
    }
}
