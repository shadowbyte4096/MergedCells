using System.Globalization;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// A library level's box on the results card: how many players have beaten it and liked it, with Like (a second tap
// unlikes) unless it's the player's own. It hides its panel when the library doesn't list the level or the server is off
public partial class LibraryBox : HBoxContainer
{
    private Label line;
    private Button like;
    private LibraryCardSource source;
    private CallHandle<LevelCounts> current;
    private LevelCounts shown;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ThemeTypeVariation = "PillRow";
        line = new Label
        {
            ThemeTypeVariation = "Muted",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(1, 0),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        like = new Button { FocusMode = FocusModeEnum.None, Visible = false, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        like.Pressed += OnLikePressed;
        AddChild(line);
        AddChild(like);
        SetProcess(false);
    }

    // A new card's source, or null when the card hides. The finish was sent as the card appeared and isn't cancelled:
    // the player beat the level whether or not they wait for the answer
    public void Watch(LibraryCardSource next)
    {
        source = next;
        current = next?.Finish;
        shown = null;
        like.Visible = false;
        line.Text = "…";
        SetProcess(next is not null);
        Poll();
    }

    // Polled rather than subscribed: the card can go before the answer, and an event would then call into a freed node
    public override void _Process(double delta) => Poll();

    private void Poll()
    {
        if (current is null || current.State == CallState.Waiting)
        {
            return;
        }
        var state = current.State;
        var counts = current.Answer;
        current = null;
        SetProcess(false);
        if (state == CallState.Answered && counts is { Listed: true })
        {
            ShowCounts(counts);
            return;
        }
        // A like that didn't go through leaves the counts as they were
        if (shown is not null)
        {
            ShowCounts(shown);
            return;
        }
        if (state == CallState.Unavailable)
        {
            line.Text = "Can't reach the library right now";
            return;
        }
        GetParent<Control>().Visible = false;
    }

    private void ShowCounts(LevelCounts counts)
    {
        shown = counts;
        var beaten = counts.Finished switch
        {
            0 => "Nobody has beaten it yet",
            1 => "1 player has beaten it",
            _ => string.Create(CultureInfo.InvariantCulture, $"{counts.Finished} players have beaten it"),
        };
        line.Text = beaten + (counts.Likes == 1 ? " · 1 like" : string.Create(CultureInfo.InvariantCulture, $" · {counts.Likes} likes"));
        like.Visible = !counts.Mine;
        like.Disabled = false;
        like.Text = counts.Liked ? "Liked" : "Like";
        like.ThemeTypeVariation = counts.Liked ? "PillPrimary" : "PillButton";
    }

    // One call at a time: presses wait for the answer
    private void OnLikePressed()
    {
        if (source is null || shown is null || current is not null)
        {
            return;
        }
        like.Disabled = true;
        current = source.Like(!shown.Liked);
        SetProcess(true);
    }
}
