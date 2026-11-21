using System.Text;
using System.Text.Json;

namespace MergedCells.Core;

/// <summary>
/// One request to the server, always a POST of JSON. <paramref name="Bearer"/> is the session's access token, or null for a
/// request made with the publishable key alone.
/// </summary>
public sealed record ServerRequest(string Path, string Body, string? Bearer)
{
    public string Url(string baseUrl) => baseUrl + "/" + Path;

    /// <summary>The key always goes in <c>apikey</c>; only a session's access token goes in <c>Authorization</c>.</summary>
    public string[] Headers(string publishableKey) =>
        Bearer is null
            ? [$"apikey: {publishableKey}", "Content-Type: application/json"]
            : [$"apikey: {publishableKey}", "Content-Type: application/json", $"Authorization: Bearer {Bearer}"];
}

/// <summary>A reply's status and body. Status 0 is no reply: no connection, a timeout or a failed handshake.</summary>
public sealed record ServerReply(int Status, string Body)
{
    public bool IsOk => Status is >= 200 and <= 299;
}

public sealed record ServerSession(string AccessToken, string RefreshToken, DateTimeOffset Expires);

/// <summary>A campaign best: the level's ID, its layout fingerprint and the moves.</summary>
public sealed record LevelScore(int Level, string Layout, int Moves);

public sealed record ScoreReply(int Accepted, IReadOnlyList<int> Unknown, IReadOnlyList<int> BelowPar, bool PlayerDeleted);

public sealed record UploadReply(int Stored, int Duplicates, int Skipped, bool PlayerDeleted);

/// <summary>
/// Every request the game makes to the server, and what each reply means. A reader gives null for anything but a 2xx
/// reply in the expected shape, so a captive portal's page never reads as a success; it never throws.
/// </summary>
public static class ServerCalls
{
    public static ServerRequest SignIn() => new("auth/v1/signup", "{}", null);

    public static ServerRequest Refresh(string refreshToken) =>
        new("auth/v1/token?grant_type=refresh_token", Json(writer => writer.WriteString("refresh_token", refreshToken)), null);

    public static ServerRequest SubmitScores(Guid player, IReadOnlyList<LevelScore> scores, string bearer) =>
        new("rest/v1/rpc/submit_scores", Json(writer =>
        {
            writer.WriteString("player", player.ToString("D"));
            writer.WriteStartArray("scores");
            foreach (var score in scores)
            {
                writer.WriteStartObject();
                writer.WriteNumber("level", score.Level);
                writer.WriteString("layout", score.Layout);
                writer.WriteNumber("moves", score.Moves);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }), bearer);

    // Each line goes in as it is: the store only hands out lines PlayRecordFile.Parse accepted
    public static ServerRequest UploadRecords(Guid player, IReadOnlyList<string> lines, string bearer) =>
        new("rest/v1/rpc/upload_records", $"{{\"player\":\"{player:D}\",\"records\":[{string.Join(",", lines)}]}}", bearer);

    public static ServerRequest DeletePlayer(Guid player) =>
        new("rest/v1/rpc/delete_player", Json(writer => writer.WriteString("player", player.ToString("D"))), null);

    public static ServerRequest BestMovesSpread(int level, string layout, string bearer) =>
        new("rest/v1/rpc/best_moves_spread", Json(writer =>
        {
            writer.WriteNumber("level", level);
            writer.WriteString("layout", layout);
        }), bearer);

    // Counts strictly ascending by moves, each at least one player, as the server sends them, and a total that fits an int, so
    // nothing that sums them can overflow
    public static SpreadCounts? ReadSpread(ServerReply reply) =>
        Read(reply, root =>
        {
            if (!root.TryGetProperty("counts", out var counts) || counts.ValueKind != JsonValueKind.Array
                || Int(root, "more") is not { } more)
            {
                return null;
            }
            var read = new List<MoveCount>();
            long total = more;
            foreach (var pair in counts.EnumerateArray())
            {
                if (pair.ValueKind != JsonValueKind.Array || pair.GetArrayLength() != 2
                    || pair[0].ValueKind != JsonValueKind.Number || !pair[0].TryGetInt32(out var moves) || moves < 1
                    || pair[1].ValueKind != JsonValueKind.Number || !pair[1].TryGetInt32(out var players) || players < 1
                    || (read.Count > 0 && moves <= read[^1].Moves))
                {
                    return null;
                }
                read.Add(new MoveCount(moves, players));
                total += players;
            }
            return total <= int.MaxValue ? new SpreadCounts(read, more) : null;
        });

    public static ServerSession? ReadSession(ServerReply reply, DateTimeOffset now) =>
        Read(reply, root =>
            String(root, "access_token") is { } access && String(root, "refresh_token") is { } refresh
                && root.TryGetProperty("expires_in", out var expires) && expires.ValueKind == JsonValueKind.Number
                && expires.TryGetInt32(out var seconds) && seconds > 0
                ? new ServerSession(access, refresh, now.AddSeconds(seconds))
                : null);

    public static ScoreReply? ReadScores(ServerReply reply) =>
        Read(reply, root =>
            Int(root, "accepted") is { } accepted && Ints(root, "unknown") is { } unknown && Ints(root, "belowPar") is { } belowPar
                && Bool(root, "playerDeleted") is { } deleted
                ? new ScoreReply(accepted, unknown, belowPar, deleted)
                : null);

    public static UploadReply? ReadUpload(ServerReply reply) =>
        Read(reply, root =>
            Int(root, "stored") is { } stored && OptionalInt(root, "duplicates") is { } duplicates
                && OptionalInt(root, "skipped") is { } skipped && Bool(root, "playerDeleted") is { } deleted
                ? new UploadReply(stored, duplicates, skipped, deleted)
                : null);

    public static bool? ReadDeleted(ServerReply reply) =>
        Read(reply, root => root.TryGetProperty("deleted", out var deleted) && deleted.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? (bool?)deleted.GetBoolean()
            : null);

    internal static string Json(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            write(writer);
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    internal static T? Read<T>(ServerReply reply, Func<JsonElement, T?> read)
    {
        if (!reply.IsOk)
        {
            return default;
        }
        try
        {
            using var document = JsonDocument.Parse(reply.Body);
            return document.RootElement.ValueKind == JsonValueKind.Object ? read(document.RootElement) : default;
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static string? String(JsonElement root, string key) =>
        root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text
            ? text
            : null;

    private static int? Int(JsonElement root, string key) =>
        root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && number >= 0
            ? number
            : null;

    // Missing reads as 0: a deleted player's reply carries no other counts
    private static int? OptionalInt(JsonElement root, string key) => root.TryGetProperty(key, out _) ? Int(root, key) : 0;

    // Missing reads as false; anything but true or false is unreadable
    private static bool? Bool(JsonElement root, string key) =>
        !root.TryGetProperty(key, out var value) ? false
        : value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean()
        : null;

    // Missing reads as empty
    private static IReadOnlyList<int>? Ints(JsonElement root, string key)
    {
        if (!root.TryGetProperty(key, out var value))
        {
            return [];
        }
        if (value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        var numbers = new List<int>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Number || !item.TryGetInt32(out var number))
            {
                return null;
            }
            numbers.Add(number);
        }
        return numbers;
    }
}
