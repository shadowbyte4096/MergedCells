using System.Text;

namespace MergedCells.Core;

/// <summary>
/// Everything the game sends to the server, one request at a time: each round sends the pending deletions, then the
/// campaign bests, then the play records, each only while its kind is given at the moment of sending. The caller sends
/// what <see cref="Next"/> returns and passes the answer to <see cref="Reply"/>. A failed round stops, and other chances
/// to send are ignored for <see cref="ServerConfig.RetrySeconds"/> unless forced. Main thread only.
/// </summary>
public sealed class ServerQueue
{
    private enum Kind
    {
        Delete,
        SignIn,
        Refresh,
        Scores,
        Records,
    }

    private readonly ServerConfig config;
    private readonly SaveGame save;
    private readonly IReadOnlyList<Level> levels;
    private readonly Func<long, int, IReadOnlyList<RecordLine>> readRecords;
    private readonly Func<bool> writeSave;
    private readonly Func<Guid> newPlayer;
    private readonly Func<DateTimeOffset> now;
    private readonly Action<string> log;

    // What the server has confirmed this run, and for which player: a new player starts with nothing confirmed
    private readonly Dictionary<int, LevelScore> confirmed = new();
    private Guid? confirmedFor;

    // The access token lives only in memory; the save keeps the refresh token it came from
    private ServerSession? session;
    private string? refusedRefresh;

    private bool active;
    private bool requested;
    private bool forcedDuringRound;
    private DateTimeOffset? failedAt;
    private bool retriedUnauthorised;

    // Set when the save couldn't keep a new session or player ID: anything sent after that would sit on the server under
    // an ID or sign-in the next run doesn't know, so "Delete my data" could never reach it
    private bool stopped;

    private Kind? outstanding;
    private Guid outstandingPlayer;
    private string? outstandingRefresh;
    private IReadOnlyList<LevelScore> outstandingScores = [];
    private long outstandingEnd;

    public ServerQueue(
        ServerConfig config,
        SaveGame save,
        IReadOnlyList<Level> levels,
        Func<long, int, IReadOnlyList<RecordLine>> readRecords,
        Func<bool> writeSave,
        Func<Guid> newPlayer,
        Func<DateTimeOffset> now,
        Action<string> log)
    {
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        this.save = save ?? throw new ArgumentNullException(nameof(save));
        this.levels = levels ?? throw new ArgumentNullException(nameof(levels));
        this.readRecords = readRecords ?? throw new ArgumentNullException(nameof(readRecords));
        this.writeSave = writeSave ?? throw new ArgumentNullException(nameof(writeSave));
        this.newPlayer = newPlayer ?? throw new ArgumentNullException(nameof(newPlayer));
        this.now = now ?? throw new ArgumentNullException(nameof(now));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>The server confirmed a pending deletion, and the save no longer lists it.</summary>
    public event Action<Guid>? DeletionConfirmed;

    /// <summary>A round ended: true when it sent everything due, false when it failed.</summary>
    public event Action<bool>? RoundEnded;

    /// <summary>
    /// Asks for a round. Within the retry gap after a failed round it's ignored unless forced. A round already running
    /// picks up new work itself; a forced wake during one that then fails starts another.
    /// </summary>
    public void Wake(bool force = false)
    {
        if (active)
        {
            forcedDuringRound |= force;
            return;
        }
        if (!force && failedAt is { } at && now() < at.AddSeconds(config.RetrySeconds))
        {
            return;
        }
        requested = true;
    }

    /// <summary>The next request of the round, or null when there's nothing to send. Throws while one is unanswered.</summary>
    public ServerRequest? Next()
    {
        if (outstanding is not null)
        {
            throw new InvalidOperationException("The last request hasn't been answered.");
        }
        if (!config.IsOn || stopped)
        {
            return null;
        }
        if (!active)
        {
            if (!requested)
            {
                return null;
            }
            requested = false;
            active = true;
            retriedUnauthorised = false;
        }
        if (save.PendingDeletions.Count > 0)
        {
            var id = save.PendingDeletions.Order().First();
            outstandingPlayer = id;
            return Send(Kind.Delete, ServerCalls.DeletePlayer(id));
        }
        var scores = NextScores();
        if (stopped)
        {
            return null;
        }
        if (scores is not null)
        {
            if (Access() is not { } bearer)
            {
                return SessionRequest();
            }
            outstandingPlayer = save.Player!.Value;
            outstandingScores = scores;
            return Send(Kind.Scores, ServerCalls.SubmitScores(outstandingPlayer, scores, bearer));
        }
        if (NextRecords() is { } records)
        {
            if (Access() is not { } bearer)
            {
                return SessionRequest();
            }
            outstandingPlayer = save.Player!.Value;
            outstandingEnd = records.End;
            return Send(Kind.Records, ServerCalls.UploadRecords(outstandingPlayer, records.Lines, bearer));
        }
        EndRound(finished: true);
        return null;
    }

    /// <summary>The answer to the request <see cref="Next"/> last returned. Throws when none is outstanding.</summary>
    public void Reply(ServerReply reply)
    {
        ArgumentNullException.ThrowIfNull(reply);
        if (outstanding is not { } kind)
        {
            throw new InvalidOperationException("No request is waiting for an answer.");
        }
        outstanding = null;
        switch (kind)
        {
            case Kind.Delete:
                Deleted(reply);
                break;
            case Kind.SignIn:
            case Kind.Refresh:
                SessionReply(kind, reply);
                break;
            case Kind.Scores:
                ScoresReply(reply);
                break;
            case Kind.Records:
                RecordsReply(reply);
                break;
        }
    }

    private ServerRequest Send(Kind kind, ServerRequest request)
    {
        outstanding = kind;
        return request;
    }

    // The bests to send now, or null. Makes a player first when there's a best and "Delete my data" left none
    private IReadOnlyList<LevelScore>? NextScores()
    {
        if (!Consents.IsGiven(save.ConsentTo(ConsentKind.Leaderboards), ConsentWordings.Leaderboards))
        {
            return null;
        }
        var bests = levels
            .Select(level => save.BestMoves(level) is { } best ? new LevelScore(level.Id, LevelFingerprint.Of(level), best) : null)
            .OfType<LevelScore>()
            .ToArray();
        if (bests.Length == 0)
        {
            return null;
        }
        if (save.Player is null)
        {
            save.Player = newPlayer();
            if (!writeSave())
            {
                Stop("the new player ID couldn't be saved");
                return null;
            }
            log("made a new player ID for the leaderboards");
        }
        if (confirmedFor != save.Player)
        {
            confirmed.Clear();
            confirmedFor = save.Player;
        }
        var due = bests
            .Where(score => !confirmed.TryGetValue(score.Level, out var sent) || sent != score)
            .OrderBy(score => score.Level)
            .Take(config.ScoresPerRequest)
            .ToArray();
        return due.Length == 0 ? null : due;
    }

    // The next batch of records to send, or null. A stretch with nothing eligible is passed over without a request
    private (IReadOnlyList<string> Lines, long End)? NextRecords()
    {
        if (!Consents.MayRecord(save) || save.Player is not { } player)
        {
            return null;
        }
        while (true)
        {
            var from = save.RecordsSent;
            var lines = readRecords(from, config.RecordsPerRequest);
            if (lines.Count == 0)
            {
                return null;
            }
            var batch = new List<string>();
            var bytes = 0;
            var end = from;
            foreach (var line in lines)
            {
                if (line.Record is { } record && record.Start.Consent >= 1 && record.Start.Player == player)
                {
                    var size = Encoding.UTF8.GetByteCount(line.Text);
                    // A record over the limit goes alone rather than never
                    if (batch.Count > 0 && bytes + size > config.RecordBytesPerRequest)
                    {
                        break;
                    }
                    batch.Add(line.Text);
                    bytes += size;
                }
                end = line.End;
            }
            if (batch.Count > 0)
            {
                return (batch, end);
            }
            if (end == from)
            {
                return null;
            }
            save.RecordsSent = end;
            writeSave();
        }
    }

    // The access token when it has long enough left and still belongs to the save's session, else null
    private string? Access() =>
        session is { } current && current.RefreshToken == save.SessionToken
            && current.Expires.AddSeconds(-config.RefreshMarginSeconds) > now()
            ? current.AccessToken
            : null;

    // Remembers the save's session and player as sent, so an answer that comes back after "Delete my data" is ignored
    private ServerRequest SessionRequest()
    {
        outstandingRefresh = save.SessionToken;
        outstandingPlayer = save.Player!.Value;
        if (save.SessionToken is { } token && token != refusedRefresh)
        {
            return Send(Kind.Refresh, ServerCalls.Refresh(token));
        }
        return Send(Kind.SignIn, ServerCalls.SignIn());
    }

    private void Deleted(ServerReply reply)
    {
        if (ServerCalls.ReadDeleted(reply) is not { } held)
        {
            Fail("delete", reply);
            return;
        }
        save.RemovePendingDeletion(outstandingPlayer);
        writeSave();
        log($"deleted a player ID ({(held ? "it held data" : "nothing was held")})");
        DeletionConfirmed?.Invoke(outstandingPlayer);
    }

    private void SessionReply(Kind kind, ServerReply reply)
    {
        // "Delete my data" dropped the session the request was for: keeping this one would tie the old ID to the next
        if (save.SessionToken != outstandingRefresh || save.Player != outstandingPlayer)
        {
            log("a session answered after its player ID was deleted; not kept");
            return;
        }
        if (ServerCalls.ReadSession(reply, now()) is { } signedIn)
        {
            session = signedIn;
            save.SessionToken = signedIn.RefreshToken;
            refusedRefresh = null;
            if (!writeSave())
            {
                Stop("the session couldn't be saved");
                return;
            }
            log(kind == Kind.SignIn ? "signed in" : "renewed the session");
            return;
        }
        // A refresh token the server won't take (reused, revoked, or its user deleted) means signing in anew
        if (kind == Kind.Refresh && reply.Status is 400 or 401 or 403)
        {
            refusedRefresh = outstandingRefresh;
            log($"the session was refused ({reply.Status}); signing in anew");
            return;
        }
        Fail(kind == Kind.SignIn ? "sign in" : "renew", reply);
    }

    private void ScoresReply(ServerReply reply)
    {
        if (save.Player != outstandingPlayer)
        {
            log("scores: the player ID was deleted while they were sent");
            return;
        }
        if (Unauthorised(reply))
        {
            return;
        }
        if (ServerCalls.ReadScores(reply) is not { } read)
        {
            Fail("scores", reply);
            return;
        }
        foreach (var score in outstandingScores)
        {
            confirmed[score.Level] = score;
        }
        log($"scores: {read.Accepted} accepted, {read.Unknown.Count} for levels the server doesn't list, {read.BelowPar.Count} below par"
            + (read.PlayerDeleted ? ", the player ID was deleted" : ""));
    }

    private void RecordsReply(ServerReply reply)
    {
        if (save.Player != outstandingPlayer)
        {
            log("records: the player ID was deleted while they were sent");
            return;
        }
        if (Unauthorised(reply))
        {
            return;
        }
        if (ServerCalls.ReadUpload(reply) is not { } read)
        {
            Fail("records", reply);
            return;
        }
        save.RecordsSent = outstandingEnd;
        writeSave();
        log($"records: {read.Stored} stored, {read.Duplicates} already held, {read.Skipped} skipped"
            + (read.PlayerDeleted ? ", the player ID was deleted" : ""));
    }

    // A 401 means the access token was refused: get a new one and try once more in this round
    private bool Unauthorised(ServerReply reply)
    {
        if (reply.Status != 401)
        {
            return false;
        }
        if (retriedUnauthorised)
        {
            Fail("a second refused token", reply);
            return true;
        }
        retriedUnauthorised = true;
        session = null;
        log("the access token was refused; renewing it");
        return true;
    }

    private void Fail(string what, ServerReply reply)
    {
        log($"{what} failed ({(reply.Status == 0 ? "no reply" : reply.Status.ToString(System.Globalization.CultureInfo.InvariantCulture))})");
        failedAt = now();
        EndRound(finished: false);
    }

    private void Stop(string why)
    {
        log($"stopped for this run: {why}");
        stopped = true;
        failedAt = now();
        EndRound(finished: false);
    }

    private void EndRound(bool finished)
    {
        active = false;
        requested = forcedDuringRound && !finished;
        forcedDuringRound = false;
        RoundEnded?.Invoke(finished);
    }
}
