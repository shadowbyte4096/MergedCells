using AutoFixture;
using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public sealed class CustomLevelStoreTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "merged-cells-custom-" + Guid.NewGuid().ToString("N"));

    private readonly Fixture fixture = new();

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
        else if (File.Exists(folder))
        {
            File.Delete(folder);
        }
    }

    private CustomLevelStore Store() => new(Path.Combine(folder, "custom-levels"));

    private CustomLevel Level(string? name = null, DateTimeOffset? edited = null, Guid? id = null, DateTimeOffset? imported = null) =>
        new(
            id ?? fixture.Create<Guid>(),
            name ?? fixture.Create<string>(),
            Map.Create(new[,] { { 1, 2, 3 }, { 4, 5, 6 } }, new(0, 0), new(1, 2)),
            edited ?? new DateTimeOffset(2026, 11, 17, 10, 0, 0, TimeSpan.Zero),
            imported);

    // A folder that exists but can't be listed, as a broken permission would leave it: the list must still open
    [Fact]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public void Folder_that_can_t_be_listed_loads_nothing_and_says_why()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Denying a folder's listing uses Windows access rules.");
        }
        var store = Store();
        store.Write(Level());
        var info = new DirectoryInfo(store.Folder);
        var rule = new System.Security.AccessControl.FileSystemAccessRule(
            System.Security.Principal.WindowsIdentity.GetCurrent().User!,
            System.Security.AccessControl.FileSystemRights.ListDirectory,
            System.Security.AccessControl.AccessControlType.Deny);
        var security = info.GetAccessControl();
        security.AddAccessRule(rule);
        info.SetAccessControl(security);
        try
        {
            var load = store.LoadAll();

            Assert.Empty(load.Levels);
            Assert.Single(load.Skipped);
            Assert.False(load.Listed);
        }
        finally
        {
            security.RemoveAccessRule(rule);
            info.SetAccessControl(security);
        }
    }

    [Fact]
    public void Missing_folder_loads_nothing()
    {
        var load = Store().LoadAll();

        Assert.Empty(load.Levels);
        Assert.Empty(load.Skipped);
    }

    [Fact]
    public void Written_level_loads_back()
    {
        var store = Store();
        var level = Level();

        Assert.Null(store.Write(level));

        var loaded = Assert.Single(store.LoadAll().Levels);
        Assert.Equal((level.Id, level.Name, level.Edited), (loaded.Id, loaded.Name, loaded.Edited));
    }

    [Fact]
    public void Newest_edit_comes_first()
    {
        var store = Store();
        var older = Level(edited: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var newer = Level(edited: new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));
        store.Write(older);
        store.Write(newer);

        Assert.Equal([newer.Id, older.Id], store.LoadAll().Levels.Select(level => level.Id));
    }

    // An imported level's edit time is its author's, so a fresh import would otherwise sink below the player's own levels
    [Fact]
    public void Imported_level_sorts_by_its_import_time()
    {
        var store = Store();
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var own = Level(edited: start.AddHours(1));
        var imported = Level(edited: start, imported: start.AddHours(2));
        store.Write(own);
        store.Write(imported);

        Assert.Equal([imported.Id, own.Id], store.LoadAll().Levels.Select(level => level.Id));
    }

    public static TheoryData<string> UnreadableTexts => new()
    {
        "not json",
        """{"version": 99}""",
    };

    [Theory]
    [MemberData(nameof(UnreadableTexts))]
    public void Skipped_file_named_for_an_id_is_unreadable(string text)
    {
        var store = Store();
        Directory.CreateDirectory(store.Folder);
        var id = fixture.Create<Guid>();
        File.WriteAllText(Path.Combine(store.Folder, $"{id:D}.json"), text);

        Assert.Equal([id], store.LoadAll().Unreadable);
    }

    // A file named for one ID but holding another level is skipped; writing that ID's file would replace it
    [Fact]
    public void Misnamed_file_s_name_is_unreadable()
    {
        var store = Store();
        Directory.CreateDirectory(store.Folder);
        var name = fixture.Create<Guid>();
        File.WriteAllText(Path.Combine(store.Folder, $"{name:D}.json"), CustomLevelFile.Write(Level()));

        Assert.Equal([name], store.LoadAll().Unreadable!);
    }

    // On Windows a write to the lowercase name would replace it
    [Fact]
    public void Uppercase_named_file_is_unreadable()
    {
        var store = Store();
        Directory.CreateDirectory(store.Folder);
        var id = fixture.Create<Guid>();
        File.WriteAllText(Path.Combine(store.Folder, $"{id.ToString("D").ToUpperInvariant()}.json"), CustomLevelFile.Write(Level(id: id)));

        Assert.Equal([id], store.LoadAll().Unreadable!);
    }

    [Fact]
    public void Same_edit_time_goes_by_name()
    {
        var store = Store();
        var b = Level("B");
        var a = Level("A");
        store.Write(b);
        store.Write(a);

        Assert.Equal(["A", "B"], store.LoadAll().Levels.Select(level => level.Name));
    }

    [Fact]
    public void Same_edit_time_and_name_goes_by_id()
    {
        var store = Store();
        var second = Level("A", id: Guid.Parse("20000000-0000-0000-0000-000000000000"));
        var first = Level("A", id: Guid.Parse("10000000-0000-0000-0000-000000000000"));
        store.Write(second);
        store.Write(first);

        Assert.Equal([first.Id, second.Id], store.LoadAll().Levels.Select(level => level.Id));
    }

    public static TheoryData<string> SkippedFiles => new()
    {
        "broken",
        "newer",
        "misnamed",
    };

    [Theory]
    [MemberData(nameof(SkippedFiles))]
    public void Skipped_files_are_listed_and_left_byte_for_byte(string kind)
    {
        var store = Store();
        Directory.CreateDirectory(store.Folder);
        var level = Level();
        var (name, text) = kind switch
        {
            "broken" => ($"{level.Id:D}.json", "not json"),
            "newer" => ($"{level.Id:D}.json", """{"version": 99}"""),
            _ => ($"{Guid.NewGuid():D}.json", CustomLevelFile.Write(level)),
        };
        var path = Path.Combine(store.Folder, name);
        File.WriteAllText(path, text);
        var before = File.ReadAllBytes(path);

        var load = store.LoadAll();

        Assert.Empty(load.Levels);
        Assert.Single(load.Skipped);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Theory]
    [AutoData]
    public void Write_replaces_the_level_with_the_same_id(string renamed)
    {
        var store = Store();
        var level = Level();
        store.Write(level);

        store.Write(level with { Name = renamed });

        Assert.Equal(renamed, Assert.Single(store.LoadAll().Levels).Name);
    }

    [Fact]
    public void Write_leaves_no_temp_file()
    {
        var store = Store();

        store.Write(Level());

        Assert.Empty(Directory.GetFiles(store.Folder, "*.tmp"));
    }

    // A crash between writing the temporary file and the rename leaves one behind
    [Fact]
    public void Leftover_temp_file_is_ignored()
    {
        var store = Store();
        var level = Level();
        store.Write(level);
        File.WriteAllText(Path.Combine(store.Folder, $"{level.Id:D}.json.tmp"), "half written");

        var load = store.LoadAll();

        Assert.Single(load.Levels);
        Assert.Empty(load.Skipped);
    }

    [Fact]
    public void Delete_removes_the_file()
    {
        var store = Store();
        var level = Level();
        store.Write(level);

        Assert.Null(store.Delete(level.Id));

        Assert.Empty(store.LoadAll().Levels);
    }

    [Fact]
    public void Delete_of_a_missing_file_is_fine()
    {
        Assert.Null(Store().Delete(fixture.Create<Guid>()));
    }

    [Fact]
    public void Write_into_a_blocked_folder_returns_a_problem()
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "custom-levels"), "in the way");

        Assert.NotNull(Store().Write(Level()));
    }
}
