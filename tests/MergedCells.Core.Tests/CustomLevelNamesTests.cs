namespace MergedCells.Core.Tests;

public class CustomLevelNamesTests
{
    [Fact]
    public void Trims_and_joins_whitespace()
    {
        Assert.Equal("Long Way", CustomLevelNames.Clean("  Long \t Way  ", 20));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n")]
    public void Blank_is_refused(string text)
    {
        Assert.Null(CustomLevelNames.Clean(text, 20));
    }

    // An e with a combining accent and an emoji outside the basic plane are each one letter to a player, though they take
    // two UTF-16 units each
    [Fact]
    public void Length_counts_text_elements()
    {
        Assert.Equal("Café \U0001F600", CustomLevelNames.Clean("Café \U0001F600", 6));
    }

    [Theory]
    [InlineData("Abcd", "Abcd")]
    [InlineData("Abcde", null)]
    public void At_the_limit_is_kept_and_past_it_is_refused(string text, string? expected)
    {
        Assert.Equal(expected, CustomLevelNames.Clean(text, 4));
    }

    // Names travel to other players inside share codes, so nothing invisible that controls the text or flips its direction
    [Theory]
    [InlineData("a\0b")]
    [InlineData("a\u001Bb")]
    [InlineData("a\u007Fb")]
    [InlineData("a\u0090b")]
    [InlineData("a‮b")]
    [InlineData("a⁦b")]
    public void Hidden_characters_are_refused(string text)
    {
        Assert.Null(CustomLevelNames.Clean(text, 20));
    }

    [Theory]
    [InlineData("a‮b")]
    [InlineData("a\0b")]
    public void Hidden_characters_are_found(string text)
    {
        Assert.True(CustomLevelNames.HasHiddenCharacters(text));
    }

    // Whitespace such as a tab is a control character, but Clean turns it into a space
    [Theory]
    [InlineData("Café ☕")]
    [InlineData("a\tb")]
    public void Plain_name_has_no_hidden_characters(string text)
    {
        Assert.False(CustomLevelNames.HasHiddenCharacters(text));
    }

    // A family emoji is joined by zero-width joiners, which are format characters too, and must stay
    [Fact]
    public void Joined_emoji_are_kept()
    {
        Assert.Equal("Family 👨‍👩‍👧‍👦", CustomLevelNames.Clean("Family 👨‍👩‍👧‍👦", 20));
    }

    // One letter carrying combining accents of 2 bytes each, after a second letter when the count is even: one or two
    // text elements, so only the byte cap can refuse it
    internal static string NameOfBytes(int bytes) =>
        (bytes % 2 == 0 ? "ab" : "a") + new string('́', (bytes - (bytes % 2 == 0 ? 2 : 1)) / 2);

    [Fact]
    public void Name_at_the_byte_cap_is_kept()
    {
        var name = NameOfBytes(512);

        Assert.Equal(name, CustomLevelNames.Clean(name, 16));
    }

    [Fact]
    public void Name_one_byte_over_the_cap_is_refused()
    {
        Assert.Null(CustomLevelNames.Clean(NameOfBytes(513), 16));
    }
}
