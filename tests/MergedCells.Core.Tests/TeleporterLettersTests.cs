namespace MergedCells.Core.Tests;

public class TeleporterLettersTests
{
    [Theory]
    [InlineData(0, "α")]
    [InlineData(1, "β")]
    [InlineData(22, "ω")]
    [InlineData(23, "Α")]
    [InlineData(44, "Ω")]
    public void Pairs_take_the_small_letters_then_the_capitals(int pairIndex, string letter)
    {
        Assert.Equal(letter, TeleporterLetters.For(pairIndex));
    }

    [Theory]
    [InlineData(45, "α")]
    [InlineData(91, "β")]
    public void Letters_start_again_after_the_45th(int pairIndex, string letter)
    {
        Assert.Equal(letter, TeleporterLetters.For(pairIndex));
    }

    [Fact]
    public void The_45_letters_are_all_different()
    {
        Assert.Equal(TeleporterLetters.Count, Enumerable.Range(0, TeleporterLetters.Count).Select(TeleporterLetters.For).Distinct().Count());
    }

    // χ and Χ look like a trap's ×, and Ο is ο made bigger
    [Theory]
    [InlineData("χ")]
    [InlineData("Χ")]
    [InlineData("Ο")]
    public void Letters_that_could_be_mistaken_never_appear(string letter)
    {
        Assert.DoesNotContain(letter, Enumerable.Range(0, TeleporterLetters.Count).Select(TeleporterLetters.For));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Negative_index_throws(int pairIndex)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TeleporterLetters.For(pairIndex));
    }
}
