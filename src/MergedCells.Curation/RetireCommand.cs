using System.Text;
using MergedCells.Core;

namespace MergedCells.Curation;

// retire <id>: takes a level out of the campaign, backs its file up into builds/retired/ and records its ID as never to
// be used again. Every new text is checked together before any file is written
internal static class RetireCommand
{
    public const string Usage = "Usage: retire <id>\n";

    public static CommandResult Run(Workspace workspace, IReadOnlyList<string> args)
    {
        int id;
        try
        {
            var arguments = Arguments.Parse(args);
            if (arguments.Positional.Count != 1)
            {
                throw new UsageException("retire needs one level ID.");
            }
            id = Arguments.Whole(arguments.Positional[0], "The level ID", 1);
        }
        catch (UsageException mistake)
        {
            return new CommandResult(CommandResult.UsageMistake, $"{mistake.Message}\n{Usage}");
        }

        try
        {
            return Retire(workspace, id);
        }
        catch (Exception problem) when (CampaignFiles.IsBadData(problem))
        {
            return Refuse($"{problem.Message}\nNothing was written.");
        }
    }

    private static CommandResult Retire(Workspace workspace, int id)
    {
        var files = CampaignFiles.Load(workspace);
        if (files.Levels.FirstOrDefault(onDisk => onDisk.Level.Id == id) is not { } retiring)
        {
            return Refuse($"No level file has ID {id}.");
        }
        var level = retiring.Level;
        var chapters = files.Campaign.Chapters.Select(ChapterEntry.From).ToList();
        var chapter = chapters[level.Chapter - 1];
        if (chapter.LevelIds.Count == 1)
        {
            return Refuse(
                $"Level {id} is chapter {level.Chapter}'s only level, and a chapter needs at least one. Promote another level into it first.");
        }
        chapters[level.Chapter - 1] = chapter with { LevelIds = chapter.LevelIds.Where(other => other != id).ToArray() };

        var chaptersText = ChapterFile.Write(chapters);
        var configText = LevelStarsEdit.Remove(files.ConfigText, id);
        var retiredText = RetiredLevels.Write(files.Retired.Append(id));
        var levels = files.Levels.Where(onDisk => onDisk != retiring).Select(onDisk => onDisk.Level).ToArray();
        var (campaign, config) = CampaignFiles.Check(levels, chaptersText, configText);
        _ = RetiredLevels.Parse(retiredText);

        // The move first, because it's the likeliest to fail and the easiest to undo
        Directory.CreateDirectory(workspace.RetiredBackupFolder);
        var backup = Path.Combine(workspace.RetiredBackupFolder, $"{id}-{Path.GetFileName(retiring.Path)}");
        File.Move(retiring.Path, backup, overwrite: true);
        try
        {
            CampaignFiles.WriteAll(
                workspace,
                [
                    (workspace.ChaptersFile, chaptersText),
                    (workspace.ConfigFile, configText),
                    (workspace.RetiredFile, retiredText),
                    (workspace.ServerLevelsFile, ServerLevels.Write(levels)),
                ]);
        }
        catch (IOException)
        {
            File.Move(backup, retiring.Path);
            throw;
        }

        var output = new StringBuilder()
            .Append($"Retired level {id}, \"{level.Name}\": its file is backed up at {workspace.Relative(backup)}, and ID {id} will never be used again.\n")
            .Append(PromoteCommand.Gates(CampaignFiles.GatesMoved(files.Campaign, files.Config, campaign, config)))
            .Append(ServerLevelsCommand.RunOnServer)
            .Append("Next: run dotnet test. To rework the level, promote the new version to give it a new ID.\n");
        return new CommandResult(CommandResult.Done, output.ToString());
    }

    private static CommandResult Refuse(string message) => new(CommandResult.Refused, message + "\n");
}
