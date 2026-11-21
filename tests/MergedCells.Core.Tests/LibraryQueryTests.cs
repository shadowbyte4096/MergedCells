namespace MergedCells.Core.Tests;

public class LibraryQueryTests
{
    private static readonly CustomLevelsConfig CustomLevels = new()
    {
        NewSide = 7,
        MinSide = 2,
        MaxSide = 30,
        MaxNameLength = 16,
        UndoLimit = 5,
        Bands =
        [
            new DifficultyBand { Name = "Easy", MinScore = 0 },
            new DifficultyBand { Name = "Medium", MinScore = 20 },
            new DifficultyBand { Name = "Hard", MinScore = 40 },
        ],
    };

    private static readonly LibraryConfig Library = new() { PageSize = 20, SizeSteps = [10, 20] };

    [Theory]
    [InlineData(0, 0, 19)]
    [InlineData(1, 20, 39)]
    [InlineData(2, 40, 100)]
    public void Band_gives_its_score_range(int band, int min, int max)
    {
        Assert.Equal((min, max), new LibraryQuery(LibraryOrder.Newest, Band: band).ScoreRange(CustomLevels));
    }

    [Theory]
    [InlineData(0, 1, 10)]
    [InlineData(1, 11, 20)]
    [InlineData(2, 21, 30)]
    public void Size_step_gives_its_side_range(int step, int min, int max)
    {
        Assert.Equal((min, max), new LibraryQuery(LibraryOrder.Newest, SizeStep: step).SideRange(Library, CustomLevels));
    }

    [Fact]
    public void Any_difficulty_has_no_score_range()
    {
        Assert.Null(new LibraryQuery(LibraryOrder.Newest).ScoreRange(CustomLevels));
    }

    [Fact]
    public void Any_size_has_no_side_range()
    {
        Assert.Null(new LibraryQuery(LibraryOrder.Newest).SideRange(Library, CustomLevels));
    }
}
