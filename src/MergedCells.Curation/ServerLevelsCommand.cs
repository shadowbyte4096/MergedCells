namespace MergedCells.Curation;

// Rewrites supabase/levels.sql from the level files, for after a hand edit
internal static class ServerLevelsCommand
{
    public const string RunOnServer = "Run supabase/levels.sql on the server (supabase db query --remote --file supabase/levels.sql, or the SQL editor).\n";

    public static CommandResult Run(Workspace workspace, IReadOnlyList<string> args)
    {
        if (args.Count > 0)
        {
            return new CommandResult(CommandResult.UsageMistake, "server-levels takes no arguments.\n\n" + Program.HelpText);
        }
        try
        {
            var levels = CampaignFiles.Load(workspace).Levels.Select(onDisk => onDisk.Level).ToArray();
            CampaignFiles.WriteAll(workspace, [(workspace.ServerLevelsFile, ServerLevels.Write(levels))]);
            return new CommandResult(
                CommandResult.Done,
                $"Wrote {workspace.Relative(workspace.ServerLevelsFile)} with {levels.Length} levels.\n" + RunOnServer);
        }
        catch (Exception problem) when (CampaignFiles.IsBadData(problem))
        {
            return new CommandResult(CommandResult.Refused, $"{problem.Message}\nNothing was written.\n");
        }
    }
}
