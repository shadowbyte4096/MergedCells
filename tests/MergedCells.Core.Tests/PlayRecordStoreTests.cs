namespace MergedCells.Core.Tests;

// Each test gets its own folder of real files
public sealed class PlayRecordStoreTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "MergedCellsTests", Guid.NewGuid().ToString("N"));

    public PlayRecordStoreTests()
    {
        Directory.CreateDirectory(folder);
    }

    private string FilePath => Path.Combine(folder, "playtest-records.jsonl");

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static PlayRecord NewRecord() => PlayRecords.Record(PlayRecords.Infinite(TestMaps.Example()));

    private static IEnumerable<Guid> Ids(PlayRecordLoad load) => load.Records.Select(record => record.Start.Id);

    [Fact]
    public void Append_to_a_missing_file_creates_it()
    {
        Assert.Null(new PlayRecordStore(FilePath).Append(NewRecord()));
    }

    [Fact]
    public void Appended_records_load_in_order()
    {
        var store = new PlayRecordStore(FilePath);
        var first = NewRecord();
        var second = NewRecord();
        store.Append(first);
        store.Append(second);

        Assert.Equal([first.Start.Id, second.Start.Id], Ids(new PlayRecordStore(FilePath).Load()));
    }

    // Android can kill the app mid-append and leave half a line with no line break after it
    [Fact]
    public void Append_after_a_torn_last_line_starts_a_new_line()
    {
        var kept = NewRecord();
        File.WriteAllText(FilePath, PlayRecordFile.Write(kept) + "\n" + "{\"version\":1,\"id");
        var added = NewRecord();

        new PlayRecordStore(FilePath).Append(added);
        var load = new PlayRecordStore(FilePath).Load();

        Assert.Equal([kept.Start.Id, added.Start.Id], Ids(load));
        Assert.Equal(1, load.Skipped);
    }

    [Fact]
    public void Load_skips_bad_lines_and_counts_them()
    {
        var kept = NewRecord();
        var newer = PlayRecordFile.Write(NewRecord()).Replace($"\"version\":{PlayRecordFile.CurrentVersion}", $"\"version\":{PlayRecordFile.CurrentVersion + 1}");
        var badMap = PlayRecordFile.Write(NewRecord()).Replace("[7,7,7,8]", "[7,7,7,7]");
        File.WriteAllText(FilePath, string.Join("\n", "rubbish", newer, PlayRecordFile.Write(kept), badMap) + "\n");

        var load = new PlayRecordStore(FilePath).Load();

        Assert.Equal([kept.Start.Id], Ids(load));
        Assert.Equal(3, load.Skipped);
    }

    [Fact]
    public void Text_holds_only_readable_lines()
    {
        var kept = NewRecord();
        File.WriteAllText(FilePath, "rubbish\n" + PlayRecordFile.Write(kept) + "\n");

        Assert.Equal(PlayRecordFile.Write(kept) + "\n", new PlayRecordStore(FilePath).Load().Text);
    }

    [Fact]
    public void Count_rises_with_each_append()
    {
        File.WriteAllText(FilePath, PlayRecordFile.Write(NewRecord()) + "\n");
        var store = new PlayRecordStore(FilePath);
        var before = store.Count;

        store.Append(NewRecord());

        Assert.Equal((1, 2), (before, store.Count));
    }

    // Appending happens as a level is won, so it mustn't read every record first: the count is worked out when it's asked for
    [Fact]
    public void Append_leaves_counting_to_the_first_use()
    {
        var store = new PlayRecordStore(FilePath);
        store.Append(NewRecord());
        File.AppendAllText(FilePath, PlayRecordFile.Write(NewRecord()) + "\n" + PlayRecordFile.Write(NewRecord()) + "\n");

        Assert.Equal(3, store.Count);
    }

    [Fact]
    public void Missing_file_loads_empty()
    {
        var load = new PlayRecordStore(FilePath).Load();

        Assert.Equal((0, 0, "", (string?)null), (load.Records.Count, load.Skipped, load.Text, load.Problem));
    }

    [Fact]
    public void Unreadable_file_loads_with_a_problem()
    {
        Directory.CreateDirectory(FilePath);

        Assert.NotNull(new PlayRecordStore(FilePath).Load().Problem);
    }

    [Fact]
    public void Append_that_cannot_write_returns_a_problem()
    {
        Directory.CreateDirectory(FilePath);

        Assert.NotNull(new PlayRecordStore(FilePath).Append(NewRecord()));
    }

    [Fact]
    public void Failed_append_leaves_the_count_alone()
    {
        Directory.CreateDirectory(FilePath);
        var store = new PlayRecordStore(FilePath);

        store.Append(NewRecord());

        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void Delete_removes_the_file()
    {
        var store = new PlayRecordStore(FilePath);
        store.Append(NewRecord());

        Assert.Null(store.Delete());
        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public void Count_is_zero_after_delete()
    {
        var store = new PlayRecordStore(FilePath);
        store.Append(NewRecord());
        store.Append(NewRecord());
        _ = store.Count;

        store.Delete();

        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void Delete_with_no_file_is_fine()
    {
        Assert.Null(new PlayRecordStore(FilePath).Delete());
    }

    [Fact]
    public void Delete_with_no_folder_is_fine()
    {
        Assert.Null(new PlayRecordStore(Path.Combine(folder, "gone", "records.jsonl")).Delete());
    }

    [Fact]
    public void Delete_reports_a_file_it_cannot_delete()
    {
        Directory.CreateDirectory(FilePath);

        Assert.NotNull(new PlayRecordStore(FilePath).Delete());
    }

    // The sheet's count after a delete then a new record must be 1, not the old count plus 1
    [Fact]
    public void Append_after_delete_counts_from_zero()
    {
        var store = new PlayRecordStore(FilePath);
        store.Append(NewRecord());
        store.Append(NewRecord());
        _ = store.Count;
        store.Delete();

        store.Append(NewRecord());

        Assert.Equal(1, store.Count);
    }
}
