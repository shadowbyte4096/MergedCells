using AutoFixture;

namespace MergedCells.Core.Tests;

public class LibraryConfigTests
{
    private static readonly CustomLevelsConfig CustomLevels = new()
    {
        NewSide = 7,
        MinSide = 2,
        MaxSide = 30,
        MaxNameLength = 16,
        UndoLimit = 5,
        Bands = [new DifficultyBand { Name = "Easy", MinScore = 0 }],
    };

    public static TheoryData<int, int[]> OutOfRange => new()
    {
        { 0, [10, 20] },
        { LibraryConfig.MaxPageSize + 1, [10, 20] },
        { 20, [20, 10] },
        { 20, [10, 10] },
        { 20, [0, 10] },
        { 20, [10, 30] },
        { 20, [10, 31] },
    };

    [Theory]
    [MemberData(nameof(OutOfRange))]
    public void Value_out_of_range_is_refused(int pageSize, int[] sizeSteps)
    {
        var config = new LibraryConfig { PageSize = pageSize, SizeSteps = sizeSteps };

        Assert.Throws<ArgumentOutOfRangeException>(() => config.CheckRanges(CustomLevels));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(LibraryConfig.MaxPageSize)]
    public void Values_in_range_pass(int pageSize)
    {
        var config = new LibraryConfig { PageSize = pageSize, SizeSteps = [1, 29] };

        config.CheckRanges(CustomLevels);
    }

    // The library refuses a longer code, so the editor's worst level at the shipped size must still fit
    [Fact]
    public void Worst_code_at_the_shipped_max_side_fits_the_library()
    {
        var shipped = GameConfig.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "game_config.json")));
        var name = CustomLevelNamesTests.NameOfBytes(CustomLevelNames.MaxBytes);
        var level = new CustomLevel(new Fixture().Create<Guid>(), name, ShareCodeTests.Worst(shipped.CustomLevels.MaxSide), DateTimeOffset.UnixEpoch);

        Assert.InRange(ShareCode.Write(level).Length, 1, LibraryConfig.MaxCodeCharacters);
    }
}
