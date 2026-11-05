using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using MergedCells.Core;

namespace MergedCells;

public static class LevelFiles
{
    private const string Folder = "res://levels";

    // Every level file, sorted by ID. A missing or broken file stops the game, as a broken config does:
    // dotnet test checks the shipped files, so this only happens to a file edited without running the tests.
    public static IReadOnlyList<Level> LoadAll()
    {
        var names = DirAccess.GetFilesAt(Folder).Where(name => name.EndsWith(".json", StringComparison.Ordinal)).ToArray();
        if (names.Length == 0)
        {
            throw Fail($"{Folder}: no level files.");
        }
        return names.Select(Load).OrderBy(level => level.Id).ToArray();
    }

    private static Level Load(string name)
    {
        var path = $"{Folder}/{name}";
        var text = FileAccess.GetFileAsString(path);
        var openError = FileAccess.GetOpenError();
        if (openError != Error.Ok)
        {
            throw Fail($"{path}: can't be read ({openError}).");
        }
        try
        {
            return LevelFile.Parse(text);
        }
        catch (Exception exception) when (exception is JsonException or InvalidMapException)
        {
            throw Fail($"{path}: {exception.Message}");
        }
    }

    private static InvalidOperationException Fail(string message)
    {
        GD.PushError(message);
        ((SceneTree)Engine.GetMainLoop()).Quit(1);
        return new InvalidOperationException(message);
    }
}
