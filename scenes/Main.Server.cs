using System;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// The server: campaign bests, play records and "Delete my data", sent only while the player's answers allow (see
// docs/systems/server.md). Off when the config has no url or the save can't be written, since a run that can't keep its
// session or player ID would make ones nobody can delete
public partial class Main
{
    private static ServerQueue server;

    // The "Privacy and data" page whose delete line waits to hear from the server, and what it says then
    private static SettingsSheet deletionSheet;
    private static Guid deletionPlayer;
    private static string deletionConfirmedLine;
    private static string deletionOfflineLine;

    private void StartServer()
    {
        var config = GameConfigFile.Current.Server;
        if (!config.IsOn)
        {
            GD.Print("Server: off (no url in the config)");
            return;
        }
        if (!SaveGameFile.CanWrite)
        {
            GD.Print("Server: off (the save can't be written)");
            return;
        }
        server = new ServerQueue(
            config,
            SaveGameFile.Current,
            campaign.Levels,
            PlayRecordFiles.ReadFrom,
            SaveGameFile.Save,
            Guid.NewGuid,
            () => DateTimeOffset.UtcNow,
            line => GD.Print($"Server: {line}"));
        server.DeletionConfirmed += OnServerDeleted;
        server.RoundEnded += OnServerRoundEnded;
        var link = new ServerLink { Name = "ServerLink" };
        AddChild(link);
        link.Start(server, config);
        GD.Print($"Server: on ({config.Url})");
        WakeServer();
    }

    // The results card's chart: only while leaderboards are shared and the server is on and not stopped for the run, since
    // otherwise nothing could ever load. The save's best is already this finish's, as RecordFinish runs first
    private static LeaderboardSource LeaderboardFor(Level level)
    {
        if (server is null || server.IsStopped
            || !Consents.IsGiven(SaveGameFile.Current.ConsentTo(ConsentKind.Leaderboards), ConsentWordings.Leaderboards)
            || SaveGameFile.Current.BestMoves(level) is not { } best)
        {
            return null;
        }
        var axis = SpreadAxis.Of(level.Par, best, GameConfigFile.Current.Leaderboard.Columns);
        return new LeaderboardSource(server.Fetch(level.Id, LevelFingerprint.Of(level)), axis);
    }

    // force: the player asked for it (a yes, "Delete my data"), so it doesn't wait out a failed round's gap
    private static void WakeServer(bool force = false) => server?.Wake(force);

    // Off at once when there's no queue for the run (no url, or the save can't be written)
    private static CallHandle<T> SendCall<T>(ServerCall<T> call)
        where T : class => server is null ? CallHandle<T>.Off() : server.Send(call);

    private static void AwaitServerDeletion(SettingsSheet sheet, Guid player, string confirmed, string offline)
    {
        deletionSheet = sheet;
        deletionPlayer = player;
        deletionConfirmedLine = confirmed;
        deletionOfflineLine = offline;
        WakeServer(force: true);
    }

    private static void OnServerDeleted(Guid player)
    {
        if (player == deletionPlayer)
        {
            ShowServerDeletion(deletionConfirmedLine);
        }
    }

    // A deletion goes first in its round, so a round that finished has confirmed it
    private static void OnServerRoundEnded(bool finished)
    {
        if (!finished)
        {
            ShowServerDeletion(deletionOfflineLine);
        }
        deletionSheet = null;
    }

    private static void ShowServerDeletion(string line)
    {
        if (deletionSheet is not null && GodotObject.IsInstanceValid(deletionSheet))
        {
            deletionSheet.UpdateDeleteResult(line);
        }
        deletionSheet = null;
    }
}
