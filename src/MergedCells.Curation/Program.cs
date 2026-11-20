using System.Text;

namespace MergedCells.Curation;

internal static class Program
{
    public const string HelpText = """
        Merged Cells curation tool: makes campaign level candidates offline, and promotes and retires levels.
        Run it from the repository root, in Release (the climb is the slow part):
          dotnet run --project src/MergedCells.Curation -c Release -- <command> ...

        Commands:
          generate <chapter> <band> [--seed N] [--count N]
              Climbs candidates for one band of the chapter's recipe (src/MergedCells.Curation/recipes.json)
              into builds/candidates/chapter-<n>-<band>-seed-<N>/, with a page of pictures to pick from.
              N defaults to 1, and --count to the band's candidates.
          concept <name> [--seed N] [--count N]
              Climbs candidates from a concept level's skeleton, src/MergedCells.Curation/concepts/<name>.json,
              toward the band it names, into builds/candidates/concept-<name>-seed-<N>/. The file's "blocks"
              layer fixes blocks (a letter each, E the end, . free) and its "tiles" layer fixes walls (#) and
              traps (x), with - for nothing, . where the tool may add twists, and S for the start or s for
              squares the start may use.
          promote <candidate file> --name "<name>" [--after <id>] [--band <band>]
              Makes the candidate a level file under the next unused ID, adds it to its chapter in
              campaign/chapters.json (at the end, or after level <id>) and sets its star limits from the
              band (read from run.json beside the file, or --band).
          retire <id>
              Takes a level out of the campaign, backs its file up into builds/retired/ and records its ID
              in src/MergedCells.Curation/retired-levels.json, so it's never used again.
          server-levels
              Rewrites supabase/levels.sql, the server's list of every level's ID, layout and par, from the
              level files. Promote and retire rewrite it too; this is for after a hand edit.
          help
              Shows this.

        Every command takes --root <folder> first to name the repository folder; otherwise it's found from
        MergedCells.sln above the current folder.

        Workflow:
          1. Edit the chapter's recipe in src/MergedCells.Curation/recipes.json.
          2. Run generate.
          3. Open the page (builds/candidates/.../index.html) in a browser.
          4. Run promote for each pick, then dotnet test, then run supabase/levels.sql on the server.
          5. Play the chapter.
          6. Reorder levels by hand in campaign/chapters.json if needed.
          7. To rework a level, retire it, then promote the new version.
        For a concept level, draw its file in src/MergedCells.Curation/concepts/, run concept <name> in place
        of generate, then carry on from step 3.

        """;

    public static int Main(string[] args)
    {
        // Gate messages print →, which the Windows console garbles in its default code page
        Console.OutputEncoding = Encoding.UTF8;
        var result = Run(args, Directory.GetCurrentDirectory());
        (result.ExitCode == CommandResult.Done ? Console.Out : Console.Error).Write(result.Output);
        return result.ExitCode;
    }

    public static CommandResult Run(string[] args, string workingFolder)
    {
        var rest = args.ToList();
        string? root = null;
        if (rest.Count > 0 && rest[0] == "--root")
        {
            if (rest.Count < 2)
            {
                return new CommandResult(CommandResult.UsageMistake, "--root needs a folder.\n\n" + HelpText);
            }
            root = Path.GetFullPath(rest[1], workingFolder);
            rest.RemoveRange(0, 2);
        }
        if (rest.Count == 0)
        {
            return new CommandResult(CommandResult.UsageMistake, HelpText);
        }

        var command = rest[0];
        var commandArgs = rest.Skip(1).ToArray();
        return command switch
        {
            "help" => new CommandResult(CommandResult.Done, HelpText),
            "generate" => WithWorkspace(root, workingFolder, workspace => GenerateCommand.Run(workspace, commandArgs)),
            "promote" => WithWorkspace(root, workingFolder, workspace => PromoteCommand.Run(workspace, commandArgs)),
            "concept" => WithWorkspace(root, workingFolder, workspace => ConceptCommand.Run(workspace, commandArgs)),
            "retire" => WithWorkspace(root, workingFolder, workspace => RetireCommand.Run(workspace, commandArgs)),
            "server-levels" => WithWorkspace(root, workingFolder, workspace => ServerLevelsCommand.Run(workspace, commandArgs)),
            _ => new CommandResult(CommandResult.UsageMistake, $"Unknown command \"{command}\".\n\n{HelpText}"),
        };
    }

    // Commands that read the repository need its root, found from the working folder unless --root named it
    private static CommandResult WithWorkspace(string? root, string workingFolder, Func<Workspace, CommandResult> run)
    {
        var found = root ?? Workspace.FindRoot(workingFolder);
        return found is null
            ? new CommandResult(
                CommandResult.UsageMistake,
                $"Can't find MergedCells.sln in {workingFolder} or any folder above it. Run the tool from inside the repository, or pass --root <folder> first.\n")
            : run(new Workspace(found));
    }
}
