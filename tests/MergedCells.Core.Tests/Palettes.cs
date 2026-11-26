namespace MergedCells.Core.Tests;

// Teal's palettes as shipped (the default theme files' colours), for tests that need a real palette
internal static class Palettes
{
    public static Palette TealLight { get; } = Make(
        "#e3f1ed", "#235e56", "#ffffff", "#3a7068", "#3a7068", "#e5eceb", "#8dafa9", "#497b74", "#349f8b",
        ["#f2f5f5", "#e0e8e7", "#cfdcda", "#bdcfcc", "#abc2bf"],
        "#235e56", "#235e56", "#235e56", "#a9c5c0", "#235e56", "#235e56", "#235e56");

    public static Palette TealDark { get; } = Make(
        "#0e1f1c", "#5ed3bf", "#16312c", "#4aa696", "#62bdac", "#1f443e", "#327065", "#4eaf9e", "#9cf2e2",
        ["#132a26", "#193833", "#204740", "#26554d", "#2c635a"],
        "#5ed3bf", "#5ed3bf", "#5ed3bf", "#26554d", "#5ed3bf", "#5ed3bf", "#5ed3bf");

    private static Palette Make(
        string background, string ink, string card, string muted, string chart, string placeholder, string disabled, string pressed,
        string @new, string[] fills, string outline, string end, string wall, string wallMiddle, string trap, string teleporter, string player) =>
        new(Rgb.Parse(background), Rgb.Parse(ink), Rgb.Parse(card), Rgb.Parse(muted), Rgb.Parse(chart), Rgb.Parse(placeholder),
            Rgb.Parse(disabled), Rgb.Parse(pressed), Rgb.Parse(@new), fills.Select(Rgb.Parse).ToArray(), Rgb.Parse(outline),
            Rgb.Parse(end), Rgb.Parse(wall), Rgb.Parse(wallMiddle), Rgb.Parse(trap), Rgb.Parse(teleporter), Rgb.Parse(player));
}
