using System.Globalization;

namespace MergedCells.Core;

/// <summary>
/// The default theme's readability rules, which every theme must meet: text at 4.5:1 on what it sits on (the card's coins
/// line, bold at 34, counts as large text at 3:1), and board pieces at 3:1 on every fill, so a bought theme is never
/// harder to read than Teal. Disabled colours have no rule (under 3:1 by chunk 6b's design).
/// </summary>
public static class PaletteContrast
{
    private const double Text = 4.5;

    private const double Large = 3;

    public static IReadOnlyList<string> Problems(Palette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        var problems = new List<string>();
        void Need(string name, Rgb front, string backName, Rgb back, double minimum)
        {
            var ratio = Contrast.Ratio(front, back);
            if (ratio < minimum)
            {
                problems.Add(string.Create(CultureInfo.InvariantCulture, $"{name} on {backName} is {ratio:0.00}:1, below {minimum:0.#}:1"));
            }
        }

        Need("ink", palette.Ink, "background", palette.Background, Text);
        Need("ink", palette.Ink, "card", palette.Card, Text);
        Need("ink", palette.Ink, "placeholder", palette.Placeholder, Text);
        Need("muted", palette.Muted, "background", palette.Background, Text);
        Need("muted", palette.Muted, "card", palette.Card, Text);
        Need("chart", palette.Chart, "placeholder", palette.Placeholder, Text);
        Need("card", palette.Card, "ink", palette.Ink, Text);
        Need("card", palette.Card, "pressed", palette.Pressed, Text);
        Need("new", palette.New, "card", palette.Card, Large);
        (string Name, Rgb Colour)[] pieces =
        [
            ("outline", palette.Outline), ("end", palette.End), ("wall", palette.Wall), ("trap", palette.Trap),
            ("teleporter", palette.Teleporter), ("player", palette.Player),
        ];
        foreach (var (name, colour) in pieces)
        {
            for (var i = 0; i < palette.Fills.Count; i++)
            {
                Need(name, colour, $"fill {i + 1}", palette.Fills[i], Large);
            }
        }
        for (var i = 0; i < palette.Fills.Count; i++)
        {
            for (var j = i + 1; j < palette.Fills.Count; j++)
            {
                if (palette.Fills[i] == palette.Fills[j])
                {
                    problems.Add($"fills {i + 1} and {j + 1} are the same colour");
                }
            }
        }
        return problems;
    }
}
