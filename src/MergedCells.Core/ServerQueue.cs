using System.Text;

namespace MergedCells.Core;

/// <summary>
/// Everything the game sends to the server, one request at a time: each round sends the pending deletions, then the
/// calls a screen is waiting on (<see cref="Send{T}"/>), then the campaign bests, then a results card's spread fetch, then
/// the play records, each only while its kind is given at the moment of sending. The caller sends what <see cref="Next"/>
/// returns and passes the answer to <see cref="Reply"/>. A failed round stops, and other chances to send are ignored for
/// <see cref="ServerConfig.RetrySeconds"/> unless forced. Main thread only.
/// </summary>
public sealed class ServerQueue
{
    private enum Kind
    {
        Delete,
        SignIn,
        Refresh,
        Scores,
        Spread,
        Records,
        Call,
    }

    // A ServerCall waiting in line, without its reply's type, so the queue can hold calls of every type in one line
    private interface IPendingCall
    {
        bool IsWaiting { get; }

        ConsentKind? Needs { get; }

        bool Session { get; }

        string Function { get; }

        // The save's player when the call was asked for, or null when there was none yet
        Guid? QueuedFor { get; }

        ServerRequest Build(Guid player, string? bearer);

        // Reads the reply and keeps it; false when it isn't a 2xx in the expected shape
        bool TryRead(ServerReply reply);

        // Answers the handle with what TryRead kept and applies the save change; returns whether the save changed
        bool Complete(SaveGame save, Guid player);

        void Fail(bool off = false);
    }

    private sealed class PendingCall<T>(ServerCall<T> call, CallHandle<T> handle, Guid? queuedFor) : IPendingCall
        where T : class
    {
        private T? read;

        public bool IsWaiting => handle.State == CallState.Waiting;

        public ConsentKind? Needs => call.Needs;

        public bool Session => call.Session;

        public string Function => call.Function;

        public Guid? QueuedFor => queuedFor;

        public ServerRequest Build(Guid player, string? bearer) =>
            new("rest/v1/rpc/" + call.Function, ServerCalls.Json(writer => call.Arguments(writer, player)), bearer);

        public bool TryRead(ServerReply reply)
        {
            read = ServerCalls.Read(reply, root => call.Read(root));
            return read is not null;
        }

        public bool Complete(SaveGame save, Guid player)
        {
            handle.Resolve(read!);
            return call.Apply?.Invoke(save, player, read!) ?? false;
        }

        public void Fail(bool off = false) => handle.Fail(off);
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

    // A results card's request, sent after the scores so the counts include a just-made best
    private SpreadFetch? waitingFetch;
    private SpreadFetch? outstandingFetch;

    // Calls a screen is waiting on, in the order they were sent, ahead of the scores
    private readonly LinkedList<IPendingCall> waitingCalls = new();
    private IPendingCall? outstandingCall;

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

    /// <summary>The save couldn't keep a session or player ID, so nothing more is sent this run.</summary>
    public bool IsStopped => stopped;

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

    /// <summary>
    /// Asks for one level's spread, sent in the next round after the scores. A newer fetch replaces a waiting one, which
    /// becomes unavailable. It's unavailable at once while the server is off, the queue has stopped, or the retry gap after
    /// a failed round is running, so an offline finish doesn't try the network.
    /// </summary>
    public SpreadFetch Fetch(int level, string layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        waitingFetch?.Fail();
        waitingFetch = null;
        var fetch = new SpreadFetch(level, layout);
        if (!config.IsOn || stopped)
        {
            fetch.Fail(off: true);
            return fetch;
        }
        if (!active && failedAt is { } at && now() < at.AddSeconds(config.RetrySeconds))
        {
            fetch.Fail();
            return fetch;
        }
        waitingFetch = fetch;
        Wake();
        return fetch;
    }

    /// <summary>
    /// Asks for one call, sent in the next round after the pending deletions and before the scores. It's off at once while
    /// the server is off or the queue has stopped. A call that isn't <see cref="ServerCall{T}.Forced"/> is unavailable at
    /// once while the retry gap after a failed round is running, as a spread fetch is; a forced one wakes the queue forced.
    /// </summary>
    public CallHandle<T> Send<T>(ServerCall<T> call)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(call);
        var handle = new CallHandle<T>();
        if (!config.IsOn || stopped)
        {
            handle.Fail(off: true);
            return handle;
        }
        if (!call.Forced && !active && failedAt is { } at && now() < at.AddSeconds(config.RetrySeconds))
        {
            handle.Fail();
            return handle;
        }
        waitingCalls.AddLast(new PendingCall<T>(call, handle, save.Player));
        Wake(call.Forced);
        return handle;
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
        var call = NextCall();
        if (stopped)
        {
            return null;
        }
        if (call is not null)
        {
            return call;
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
        // Cancelled: its card went before it was sent
        if (waitingFetch is { State: not SpreadFetchState.Waiting })
        {
            waitingFetch = null;
        }
        if (waitingFetch is { } fetch)
        {
            if (!Consents.IsGiven(save.ConsentTo(ConsentKind.Leaderboards), ConsentWordings.Leaderboards) || save.Player is null)
            {
                log("spread: not fetched, as leaderboards aren't shared or there's no player ID");
                fetch.Fail();
                waitingFetch = null;
            }
            else
            {
                if (Access() is not { } bearer)
                {
                    return SessionRequest();
                }
                waitingFetch = null;
                outstandingFetch = fetch;
                outstandingPlayer = save.Player.Value;
                return Send(Kind.Spread, ServerCalls.BestMovesSpread(fetch.Level, fetch.Layout, bearer));
            }
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
            case Kind.Spread:
                SpreadReply(reply);
                break;
            case Kind.Records:
                RecordsReply(reply);
                break;
            case Kind.Call:
                CallReply(reply);
                break;
        }
    }

    // The first waiting call that can go now, or the session it needs first, or null. A call whose kind isn't given, or
    // that needs a player when there's none and no kind to make one for, is unavailable and passed over
    private ServerRequest? NextCall()
    {
        while (waitingCalls.First is { } first)
        {
            var call = first.Value;
            if (!call.IsWaiting)
            {
                waitingCalls.RemoveFirst();
                continue;
            }
            if (call.Needs is { } kind && !Consents.IsGiven(save.ConsentTo(kind), ConsentWordings.For(kind)))
            {
                log($"{call.Function}: not sent, as {kind} isn't given");
                waitingCalls.RemoveFirst();
                call.Fail();
                continue;
            }
            // "Delete my data" ran since it was asked for: under the next ID a queued publish would put a level back
            if (call.QueuedFor is { } queuedFor && save.Player != queuedFor)
            {
                log($"{call.Function}: not sent, as the player ID was deleted since it was asked for");
                waitingCalls.RemoveFirst();
                call.Fail();
                continue;
            }
            if (save.Player is null)
            {
                if (call.Needs is null)
                {
                    log($"{call.Function}: not sent, as there's no player ID");
                    waitingCalls.RemoveFirst();
                    call.Fail();
                    continue;
                }
                if (!MakePlayer($"the {call.Needs}"))
                {
                    return null;
                }
            }
            string? bearer = null;
            if (call.Session)
            {
                if (Access() is not { } access)
                {
                    return SessionRequest();
                }
                bearer = access;
            }
            waitingCalls.RemoveFirst();
            outstandingCall = call;
            outstandingPlayer = save.Player!.Value;
            return Send(Kind.Call, call.Build(outstandingPlayer, bearer));
        }
        return null;
    }

    // Like a spread fetch, a call moves nothing the queue tracks, so only a server that's down or busy fails the round;
    // any other refusal costs only that call. A reply after "Delete my data" changed the player changes nothing
    private void CallReply(ServerReply reply)
    {
        var call = outstandingCall!;
        outstandingCall = null;
        var samePlayer = save.Player == outstandingPlayer;
        if (call.TryRead(reply))
        {
            if (!samePlayer)
            {
                call.Fail();
                log($"{call.Function}: answered after the player ID was deleted; nothing kept");
                return;
            }
            if (call.Complete(save, outstandingPlayer))
            {
                writeSave();
            }
            log($"{call.Function}: answered");
            return;
        }
        if (!samePlayer)
        {
            call.Fail();
            log($"{call.Function}: no answer, and the player ID was deleted while it was asked");
            return;
        }
        if (Unauthorised(reply))
        {
            if (active)
            {
                waitingCalls.AddFirst(call);
            }
            else
            {
                call.Fail();
            }
            return;
        }
        call.Fail();
        if (reply.Status is 0 or 429 or >= 500)
        {
            Fail(call.Function, reply);
            return;
        }
        var status = reply.Status.ToString(System.Globalization.CultureInfo.InvariantCulture);
        log(reply.IsOk ? $"{call.Function}: the reply didn't read ({status}); the round goes on" : $"{call.Function} refused ({status}); the round goes on");
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
        if (save.Player is null && !MakePlayer("the leaderboards"))
        {
            return null;
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

    // For a yes standing after "Delete my data" left no player ID. False when the save can't keep it, which stops the queue
    private bool MakePlayer(string what)
    {
        save.Player = newPlayer();
        if (!writeSave())
        {
            Stop("the new player ID couldn't be saved");
            return false;
        }
        log($"made a new player ID for {what}");
        return true;
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

    // The counts aren't tied to a player, so a reply after "Delete my data" still answers; but like the other replies,
    // a failure after it never ends the round, whose ending the deletion line reads
    private void SpreadReply(ServerReply reply)
    {
        var fetch = outstandingFetch!;
        outstandingFetch = null;
        if (ServerCalls.ReadSpread(reply) is { } counts)
        {
            fetch.Answer(counts);
            log($"spread for level {fetch.Level}: {counts.Counts.Sum(count => count.Players) + counts.More} players");
            return;
        }
        if (save.Player != outstandingPlayer)
        {
            fetch.Fail();
            log("spread: no answer, and the player ID was deleted while it was asked");
            return;
        }
        if (Unauthorised(reply))
        {
            // Sent again after the renewal, unless the round failed or a newer card has asked since
            if (active && waitingFetch is null)
            {
                waitingFetch = fetch;
            }
            else
            {
                fetch.Fail();
            }
            return;
        }
        fetch.Fail();
        // Down or busy: the records would fail too. Any other refusal costs only the chart, never the uploads
        if (reply.Status is 0 or 429 or >= 500)
        {
            Fail("spread", reply);
            return;
        }
        var status = reply.Status.ToString(System.Globalization.CultureInfo.InvariantCulture);
        log(reply.IsOk ? $"spread: the reply didn't read ({status}); the round goes on" : $"spread refused ({status}); the round goes on");
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
        waitingFetch?.Fail(off: true);
        waitingFetch = null;
        foreach (var call in waitingCalls)
        {
            call.Fail(off: true);
        }
        waitingCalls.Clear();
        failedAt = now();
        EndRound(finished: false);
    }

    private void EndRound(bool finished)
    {
        if (!finished)
        {
            waitingFetch?.Fail();
            waitingFetch = null;
            foreach (var call in waitingCalls)
            {
                call.Fail();
            }
            waitingCalls.Clear();
        }
        active = false;
        requested = forcedDuringRound && !finished;
        forcedDuringRound = false;
        RoundEnded?.Invoke(finished);
    }
}
