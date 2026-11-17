namespace MergedCells.Core.Tests;

public class EditHistoryTests
{
    private static readonly Map First = LevelEdits.Blank(2, 2);

    private static readonly Map Second = LevelEdits.Blank(2, 3);

    [Fact]
    public void Undo_then_redo_returns_to_the_same_map()
    {
        var history = new EditHistory(First);
        history.Apply(Second);

        Assert.True(history.Undo());
        Assert.Same(First, history.Current);
        Assert.True(history.Redo());
        Assert.Same(Second, history.Current);
    }

    [Fact]
    public void New_edit_clears_redo()
    {
        var history = new EditHistory(First);
        history.Apply(Second);
        history.Undo();

        history.Apply(LevelEdits.Blank(3, 3));

        Assert.False(history.CanRedo);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Nothing_to_undo_or_redo_is_false(bool undo)
    {
        var history = new EditHistory(First);

        Assert.False(undo ? history.Undo() : history.Redo());
    }

    [Fact]
    public void Only_the_last_200_maps_are_kept()
    {
        var history = new EditHistory(First);
        for (var edit = 0; edit <= EditHistory.Depth; edit++)
        {
            history.Apply(LevelEdits.Blank(2, 2));
        }

        var undone = 0;
        while (history.Undo())
        {
            undone++;
        }

        Assert.Equal(EditHistory.Depth, undone);
    }
}
