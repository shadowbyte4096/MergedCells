using System;
using System.Text.Json;
using Godot;
using MergedCells.Core;

namespace MergedCells;

public static class GameConfigFile
{
    private const string FilePath = "res://config/game_config.json";

    private static GameConfig current;

    // Loaded once, on first use; a missing or invalid file stops the game rather than running on guessed numbers
    public static GameConfig Current => current ??= Load();

    private static GameConfig Load()
    {
        var text = FileAccess.GetFileAsString(FilePath);
        var openError = FileAccess.GetOpenError();
        string problem;
        if (openError != Error.Ok)
        {
            problem = $"can't be read ({openError}).";
        }
        else
        {
            try
            {
                return GameConfig.Parse(text);
            }
            catch (JsonException exception)
            {
                problem = exception.Message;
            }
        }

        var message = $"{FilePath}: {problem}";
        GD.PushError(message);
        ((SceneTree)Engine.GetMainLoop()).Quit(1);
        throw new InvalidOperationException(message);
    }
}
