using System;
using System.Text.Json;
using Godot;
using MergedCells.Core;

namespace MergedCells;

public static class CampaignFiles
{
    private const string FilePath = "res://campaign/chapters.json";

    // The level files and the chapter file. A broken one stops the game, as a broken level file does:
    // dotnet test checks the shipped files, so this only happens to a file edited without running the tests.
    public static Campaign Load()
    {
        var levels = LevelFiles.LoadAll();
        var text = FileAccess.GetFileAsString(FilePath);
        var openError = FileAccess.GetOpenError();
        if (openError != Error.Ok)
        {
            throw Fail($"{FilePath}: can't be read ({openError}).");
        }
        try
        {
            return Campaign.Parse(text, levels);
        }
        catch (JsonException exception)
        {
            throw Fail($"{FilePath}: {exception.Message}");
        }
    }

    private static InvalidOperationException Fail(string message)
    {
        GD.PushError(message);
        ((SceneTree)Engine.GetMainLoop()).Quit(1);
        return new InvalidOperationException(message);
    }
}
