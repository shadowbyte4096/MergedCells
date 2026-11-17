using System.Text;

namespace MergedCells.Core.Tests;

// Each test gets its own folder of real files
public sealed class SaveStoreTests : IDisposable
{
    private static readonly StarsConfig Shipped = new()
    {
        TwoStarMinExtraMoves = 2,
        TwoStarExtraParPercent = 50,
        OneStarMinExtraMoves = 4,
        OneStarExtraParPercent = 100,
    };

    private readonly string folder = Path.Combine(Path.GetTempPath(), "MergedCellsTests", Guid.NewGuid().ToString("N"));

    public SaveStoreTests()
    {
        Directory.CreateDirectory(folder);
    }

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private string SavePath => Path.Combine(folder, "save.json");

    private string BrokenPath => Path.Combine(folder, "save.broken.json");

    private string TempPath => SavePath + ".tmp";

    // Level 4 in SaveGameJsonTests.Valid
    private static Level Four => new(4, "Four", 1, TestMaps.Example(), 3);

    private static Level Five => new(5, "Five", 1, TestMaps.Example(), 3);

    [Fact]
    public void Missing_file_starts_fresh_and_can_write()
    {
        var store = new SaveStore(SavePath);

        var load = store.Load();

        Assert.Equal((SaveOutcome.Missing, (string?)null, true, (int?)null), (load.Outcome, load.Problem, store.CanWrite, load.Save.BestMoves(Four)));
    }

    [Fact]
    public void Valid_file_loads()
    {
        File.WriteAllText(SavePath, SaveGameJsonTests.Valid);
        var store = new SaveStore(SavePath);

        var load = store.Load();

        Assert.Equal((SaveOutcome.Loaded, (int?)7, true), (load.Outcome, load.Save.BestMoves(Four), store.CanWrite));
    }

    [Fact]
    public void Broken_file_is_kept_aside_and_a_fresh_save_starts()
    {
        File.WriteAllText(SavePath, "{");
        var store = new SaveStore(SavePath);

        var load = store.Load();

        Assert.Equal(SaveOutcome.Broken, load.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(load.Problem));
        Assert.Equal("{", File.ReadAllText(BrokenPath));
        Assert.False(File.Exists(SavePath));
        Assert.True(store.CanWrite);
        Assert.Null(load.Save.BestMoves(Four));
    }

    [Fact]
    public void Broken_file_replaces_an_earlier_broken_copy()
    {
        File.WriteAllText(BrokenPath, "older");
        File.WriteAllText(SavePath, "{");

        new SaveStore(SavePath).Load();

        Assert.Equal("{", File.ReadAllText(BrokenPath));
    }

    [Fact]
    public void Write_after_a_broken_file_creates_a_fresh_save()
    {
        File.WriteAllText(SavePath, "{");
        var store = new SaveStore(SavePath);
        var load = store.Load();

        Assert.Null(store.Write(load.Save));

        Assert.Null(Record.Exception(() => SaveGame.Parse(File.ReadAllText(SavePath))));
    }

    [Fact]
    public void Newer_file_is_left_untouched()
    {
        var newer = SaveGameJsonTests.Valid.Replace("\"version\":1", "\"version\":3");
        File.WriteAllText(SavePath, newer);
        var store = new SaveStore(SavePath);

        var load = store.Load();

        Assert.Equal((SaveOutcome.Newer, "3", false), (load.Outcome, load.Problem, store.CanWrite));
        Assert.Throws<InvalidOperationException>(() => store.Write(load.Save));
        Assert.Equal(newer, File.ReadAllText(SavePath));
    }

    [Fact]
    public void Unreadable_file_plays_without_saving()
    {
        Directory.CreateDirectory(SavePath);
        var store = new SaveStore(SavePath);

        var load = store.Load();

        Assert.Equal((SaveOutcome.Unreadable, false), (load.Outcome, store.CanWrite));
        Assert.False(string.IsNullOrWhiteSpace(load.Problem));
    }

    [Fact]
    public void Written_save_loads_back()
    {
        var store = new SaveStore(SavePath);
        var save = store.Load().Save;
        save.RecordFinish(Four, 5, Shipped);

        store.Write(save);

        Assert.Equal((int?)5, new SaveStore(SavePath).Load().Save.BestMoves(Four));
    }

    [Fact]
    public void Write_leaves_no_temporary_file()
    {
        var store = new SaveStore(SavePath);

        store.Write(store.Load().Save);

        Assert.False(File.Exists(TempPath));
    }

    [Fact]
    public void Write_replaces_an_existing_save()
    {
        File.WriteAllText(SavePath, SaveGameJsonTests.Valid);
        var store = new SaveStore(SavePath);
        var save = store.Load().Save;
        save.RecordFinish(Five, 4, Shipped);

        store.Write(save);

        var read = new SaveStore(SavePath).Load().Save;
        Assert.Equal(((int?)7, (int?)4), (read.BestMoves(Four), read.BestMoves(Five)));
    }

    [Fact]
    public void Leftover_temporary_file_is_ignored()
    {
        File.WriteAllText(TempPath, "junk from a write cut short");
        var store = new SaveStore(SavePath);

        var load = store.Load();
        var problem = store.Write(load.Save);

        Assert.Equal((SaveOutcome.Missing, (string?)null, false), (load.Outcome, problem, File.Exists(TempPath)));
    }

    [Fact]
    public void Write_creates_the_folder_when_missing()
    {
        var path = Path.Combine(folder, "not yet made", "save.json");
        var store = new SaveStore(path);

        var problem = store.Write(store.Load().Save);

        Assert.Equal(((string?)null, true), (problem, File.Exists(path)));
    }

    [Fact]
    public void Failed_write_is_reported_not_thrown()
    {
        var store = new SaveStore(SavePath);
        var save = store.Load().Save;
        Directory.CreateDirectory(TempPath);

        var problem = store.Write(save);

        Assert.False(string.IsNullOrWhiteSpace(problem));
    }

    [Fact]
    public void Failed_write_leaves_the_old_save_whole()
    {
        File.WriteAllText(SavePath, SaveGameJsonTests.Valid);
        var store = new SaveStore(SavePath);
        var save = store.Load().Save;
        save.RecordFinish(Five, 4, Shipped);
        Directory.CreateDirectory(TempPath);

        store.Write(save);

        Assert.Equal(SaveGameJsonTests.Valid, File.ReadAllText(SavePath));
    }

    // A fresh save written over a broken file that was never kept would lose it for good
    [Fact]
    public void Broken_file_that_cannot_be_set_aside_is_left_alone_and_nothing_is_written()
    {
        File.WriteAllText(SavePath, "{");
        Directory.CreateDirectory(BrokenPath);
        File.WriteAllText(Path.Combine(BrokenPath, "in the way"), "");
        var store = new SaveStore(SavePath);

        var load = store.Load();

        Assert.Equal((SaveOutcome.Unreadable, false, "{"), (load.Outcome, store.CanWrite, File.ReadAllText(SavePath)));
    }

    [Fact]
    public void File_saved_by_a_text_editor_loads()
    {
        File.WriteAllText(SavePath, SaveGameJsonTests.Valid.Replace(",", ",\r\n"), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        var store = new SaveStore(SavePath);

        var load = store.Load();

        Assert.Equal((SaveOutcome.Loaded, (int?)7), (load.Outcome, load.Save.BestMoves(Four)));
    }

    [Fact]
    public void Write_before_load_throws()
    {
        Assert.Throws<InvalidOperationException>(() => new SaveStore(SavePath).Write(new SaveGame()));
    }
}
