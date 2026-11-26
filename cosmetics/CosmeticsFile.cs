using System;
using System.Text.Json;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// The shop's catalogue, read once. A broken file stops the game, as a broken level file does: dotnet test checks the
// shipped file, so this only happens to a file edited without running the tests.
public static class CosmeticsFile
{
    private const string FilePath = "res://cosmetics/cosmetics.json";

    private static Cosmetics current;

    public static Cosmetics Current => current ??= Load();

    private static Cosmetics Load()
    {
        var text = FileAccess.GetFileAsString(FilePath);
        var openError = FileAccess.GetOpenError();
        if (openError != Error.Ok)
        {
            throw Fail($"{FilePath}: can't be read ({openError}).");
        }
        try
        {
            return Cosmetics.Parse(text);
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
