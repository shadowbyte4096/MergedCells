namespace MergedCells.Core.Tests;

public class PercentageTests
{
    [Theory]
    [InlineData(40, 10, 4)]
    [InlineData(40, 7, 3)]
    [InlineData(40, 2, 1)]
    [InlineData(50, 18, 9)]
    [InlineData(100, 99, 99)]
    [InlineData(0, 10, 0)]
    public void Percentage_rounds_up(int percent, int length, int expected)
    {
        Assert.Equal(expected, Percentage.Of(percent, length));
    }
}
