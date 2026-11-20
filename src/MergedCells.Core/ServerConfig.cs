namespace MergedCells.Core;

/// <summary>
/// The config's <c>server</c> group: the Supabase project the game sends scores and play records to, and how often and
/// how much it sends. An empty <see cref="Url"/> turns the server off.
/// </summary>
public sealed record ServerConfig
{
    // The SQL functions' own caps (supabase/migrations): a batch over them would be refused whole
    public const int MaxScoresPerRequest = 500;

    public const int MaxRecordsPerRequest = 100;

    // The project's address, https with no trailing slash, or empty for no server
    public required string Url { get; init; }

    // Meant to be public; never the secret key
    public required string PublishableKey { get; init; }

    public required double TimeoutSeconds { get; init; }

    // After a failed round, other chances to send are ignored this long
    public required double RetrySeconds { get; init; }

    // An access token this close to expiring is renewed first
    public required double RefreshMarginSeconds { get; init; }

    public required int ScoresPerRequest { get; init; }

    public required int RecordsPerRequest { get; init; }

    // A record bigger than this goes alone
    public required int RecordBytesPerRequest { get; init; }

    public bool IsOn => Url.Length > 0;

    /// <summary>
    /// Throws <see cref="ArgumentOutOfRangeException"/> naming the first value out of range. <see cref="GameConfig.CheckRanges"/>
    /// calls it at start-up.
    /// </summary>
    public void CheckRanges()
    {
        if (Url.Length > 0 && (!Uri.TryCreate(Url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || uri.Host.Length == 0
            || Url.EndsWith('/')))
        {
            throw new ArgumentOutOfRangeException("server.url", Url, "Must be empty, or an https address with no trailing slash.");
        }
        if ((PublishableKey.Length == 0) != (Url.Length == 0))
        {
            throw new ArgumentOutOfRangeException("server.publishableKey", "Must be empty exactly when the url is.");
        }
        Positive(TimeoutSeconds, "server.timeoutSeconds");
        Positive(RetrySeconds, "server.retrySeconds");
        Positive(RefreshMarginSeconds, "server.refreshMarginSeconds");
        if (ScoresPerRequest is < 1 or > MaxScoresPerRequest)
        {
            throw new ArgumentOutOfRangeException("server.scoresPerRequest", ScoresPerRequest, $"Must be from 1 to {MaxScoresPerRequest}.");
        }
        if (RecordsPerRequest is < 1 or > MaxRecordsPerRequest)
        {
            throw new ArgumentOutOfRangeException("server.recordsPerRequest", RecordsPerRequest, $"Must be from 1 to {MaxRecordsPerRequest}.");
        }
        ArgumentOutOfRangeException.ThrowIfLessThan(RecordBytesPerRequest, 1024, "server.recordBytesPerRequest");
    }

    private static void Positive(double seconds, string name)
    {
        if (!double.IsFinite(seconds) || seconds <= 0)
        {
            throw new ArgumentOutOfRangeException(name, seconds, "Must be positive.");
        }
    }
}

/// <summary>The config's <c>privacy</c> group.</summary>
public sealed record PrivacyConfig
{
    // Presses on a consent ask this soon after it appears are ignored, so a double tap can't answer the next ask unread
    public required double AskGuardSeconds { get; init; }

    public void CheckRanges()
    {
        if (!double.IsFinite(AskGuardSeconds) || AskGuardSeconds < 0)
        {
            throw new ArgumentOutOfRangeException("privacy.askGuardSeconds", AskGuardSeconds, "Must be 0 or more.");
        }
    }
}
