using System.Globalization;
using System.Text.Json;

namespace MergedCells.Core;

public enum ReportReason
{
    Name,
    Picture,
    Other,
}

public enum PublishOutcome
{
    Published,

    // Another player published this level's ID first
    Taken,

    // Another player's level in the library has this layout
    Copy,

    // A shipped campaign level has this layout
    Campaign,

    // The level was removed from the library, so its ID can't come back
    Removed,

    // The developer stopped this player publishing
    Stopped,

    // The player has as many levels in the library as it takes
    Full,

    // The player ID was deleted
    Deleted,
}

/// <summary>
/// One library level as the server listed it, unchecked: the phone re-checks the code against the rest
/// (<see cref="LibraryEntries.Check"/>). A tombstone (<paramref name="Removed"/>) has no code, name or layout.
/// </summary>
public sealed record LibraryRow(
    Guid Id,
    string? Code,
    string? Name,
    string? Layout,
    int Rows,
    int Columns,
    int Par,
    int Score,
    int Finished,
    int Likes,
    bool Mine,
    bool Hidden,
    bool Removed);

public sealed record LibraryPage(IReadOnlyList<LibraryRow> Rows, int Blocked);

public sealed record PublishReply(PublishOutcome Outcome);

/// <summary>How many players have beaten a level and liked it, and whether this player has; nothing when it isn't listed.</summary>
public sealed record LevelCounts(bool Listed, bool Mine, int Finished, int Likes, bool Liked);

public sealed record ReportReply(bool Reported);

public sealed record BlockReply(int Blocked);

public sealed record RemoveReply(bool Removed);

/// <summary>
/// Every call the level library makes, for <see cref="ServerQueue.Send{T}"/>. Each needs the library's yes, a session and
/// the player's press, except as noted: Remove is a deletion, and a finish is sent at a win without one.
/// </summary>
public static class LibraryCalls
{
    public static ServerCall<LibraryPage> Levels(LibraryQuery query, int page, LibraryConfig library, CustomLevelsConfig customLevels)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(customLevels);
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        var scores = query.ScoreRange(customLevels);
        var sides = query.SideRange(library, customLevels);
        var view = query.Order switch
        {
            LibraryOrder.Played => "played",
            LibraryOrder.Liked => "liked",
            _ => "newest",
        };
        return Call("library_levels", (writer, player) =>
        {
            WritePlayer(writer, player);
            writer.WriteString("view", view);
            WriteNumber(writer, "min_score", scores?.Min);
            WriteNumber(writer, "max_score", scores?.Max);
            WriteNumber(writer, "min_side", sides?.Min);
            WriteNumber(writer, "max_side", sides?.Max);
            writer.WriteNumber("page", page);
            writer.WriteNumber("size", library.PageSize);
        }, ReadPage);
    }

    // Every level the player published, hidden and removed ones included, in one reply
    public static ServerCall<LibraryPage> Mine() =>
        Call("library_levels", (writer, player) =>
        {
            WritePlayer(writer, player);
            writer.WriteString("view", "mine");
            writer.WriteNull("min_score");
            writer.WriteNull("max_score");
            writer.WriteNull("min_side");
            writer.WriteNull("max_side");
            writer.WriteNumber("page", 0);
            writer.WriteNumber("size", LibraryConfig.MaxPageSize);
        }, ReadPage);

    /// <summary>
    /// Publishes the level, or replaces this player's earlier version of it. Published records it in the save with the
    /// version's <see cref="CustomLevel.Edited"/> time, and as shared from this device, so getting it back from the library
    /// makes it the player's own again; removed drops it from the save.
    /// </summary>
    public static ServerCall<PublishReply> Publish(CustomLevel level, int par, int score)
    {
        ArgumentNullException.ThrowIfNull(level);
        var code = ShareCode.Write(level);
        var layout = LevelFingerprint.Of(level.Map, par);
        return Call("library_publish", (writer, player) =>
        {
            WritePlayer(writer, player);
            writer.WriteString("level", level.Id.ToString("D"));
            writer.WriteString("code", code);
            writer.WriteString("name", level.Name);
            writer.WriteString("layout", layout);
            writer.WriteNumber("rows", level.Map.Rows);
            writer.WriteNumber("columns", level.Map.Columns);
            writer.WriteNumber("par", par);
            writer.WriteNumber("score", score);
        }, ReadPublish) with
        {
            Apply = (save, _, reply) =>
            {
                switch (reply.Outcome)
                {
                    case PublishOutcome.Published:
                        save.RecordPublished(level.Id, level.Edited);
                        save.RecordShared(level.Id);
                        return true;
                    case PublishOutcome.Removed:
                        return save.RemovePublished(level.Id);
                    default:
                        return false;
                }
            },
        };
    }

    // A deletion: sent whatever the answers and without a session, as "Delete my data" is; the player ID is the bearer
    public static ServerCall<RemoveReply> Remove(Guid level) =>
        Call("library_remove", (writer, player) => WriteLevel(writer, player, level), root => Bool(root, "removed") is { } removed ? new RemoveReply(removed) : null, needs: null) with
        {
            Session = false,
            Apply = (save, _, _) => save.RemovePublished(level),
        };

    // Sent at a win, not by a press, so like a spread fetch it doesn't go during the gap after a failed round
    public static ServerCall<LevelCounts> Finish(Guid level) =>
        Call("library_finish", (writer, player) => WriteLevel(writer, player, level), ReadCounts) with { Forced = false };

    public static ServerCall<LevelCounts> Like(Guid level, bool liked) =>
        Call("library_like", (writer, player) =>
        {
            WriteLevel(writer, player, level);
            writer.WriteBoolean("liked", liked);
        }, ReadCounts);

    public static ServerCall<ReportReply> Report(Guid level, ReportReason reason)
    {
        var word = reason switch
        {
            ReportReason.Name => "name",
            ReportReason.Picture => "picture",
            _ => "other",
        };
        return Call("library_report", (writer, player) =>
        {
            WriteLevel(writer, player, level);
            writer.WriteString("reason", word);
        }, root => Bool(root, "reported") is { } reported ? new ReportReply(reported) : null);
    }

    // Blocks whoever published the level; the phone never learns who that is
    public static ServerCall<BlockReply> Block(Guid level) =>
        Call("library_block", (writer, player) => WriteLevel(writer, player, level), ReadBlocked);

    public static ServerCall<BlockReply> Unblock() =>
        Call("library_unblock", WritePlayer, ReadBlocked);

    private static ServerCall<T> Call<T>(
        string function, Action<Utf8JsonWriter, Guid> arguments, Func<JsonElement, T?> read, ConsentKind? needs = ConsentKind.Library)
        where T : class =>
        new(function, needs, arguments, read) { Forced = true };

    private static void WritePlayer(Utf8JsonWriter writer, Guid player) => writer.WriteString("player", player.ToString("D"));

    private static void WriteLevel(Utf8JsonWriter writer, Guid player, Guid level)
    {
        WritePlayer(writer, player);
        writer.WriteString("level", level.ToString("D"));
    }

    private static void WriteNumber(Utf8JsonWriter writer, string key, int? value)
    {
        if (value is { } number)
        {
            writer.WriteNumber(key, number);
        }
        else
        {
            writer.WriteNull(key);
        }
    }

    private static LibraryPage? ReadPage(JsonElement root)
    {
        if (!root.TryGetProperty("levels", out var levels) || levels.ValueKind != JsonValueKind.Array || Count(root, "blocked") is not { } blocked)
        {
            return null;
        }
        var rows = new List<LibraryRow>();
        foreach (var level in levels.EnumerateArray())
        {
            if (ReadRow(level) is not { } row)
            {
                return null;
            }
            rows.Add(row);
        }
        return new LibraryPage(rows, blocked);
    }

    private static LibraryRow? ReadRow(JsonElement row)
    {
        if (row.ValueKind != JsonValueKind.Object
            || Text(row, "id") is not { } id || !Guid.TryParseExact(id, "D", out var level)
            || !NullableText(row, "code", out var code) || !NullableText(row, "name", out var name) || !NullableText(row, "layout", out var layout)
            || Count(row, "rows") is not { } rows || Count(row, "columns") is not { } columns || Count(row, "par") is not { } par
            || Count(row, "score") is not { } score || Count(row, "finished") is not { } finished || Count(row, "likes") is not { } likes
            || Bool(row, "mine") is not { } mine || Bool(row, "hidden") is not { } hidden || Bool(row, "removed") is not { } removed)
        {
            return null;
        }
        return new LibraryRow(level, code, name, layout, rows, columns, par, score, finished, likes, mine, hidden, removed);
    }

    private static PublishReply? ReadPublish(JsonElement root) =>
        Text(root, "outcome") switch
        {
            "published" => new PublishReply(PublishOutcome.Published),
            "taken" => new PublishReply(PublishOutcome.Taken),
            "copy" => new PublishReply(PublishOutcome.Copy),
            "campaign" => new PublishReply(PublishOutcome.Campaign),
            "removed" => new PublishReply(PublishOutcome.Removed),
            "stopped" => new PublishReply(PublishOutcome.Stopped),
            "full" => new PublishReply(PublishOutcome.Full),
            "deleted" => new PublishReply(PublishOutcome.Deleted),
            _ => null,
        };

    private static LevelCounts? ReadCounts(JsonElement root)
    {
        if (Bool(root, "listed") is not { } listed)
        {
            return null;
        }
        if (!listed)
        {
            return new LevelCounts(false, false, 0, 0, false);
        }
        return Bool(root, "mine") is { } mine && Count(root, "finished") is { } finished && Count(root, "likes") is { } likes
            && Bool(root, "liked") is { } liked
            ? new LevelCounts(true, mine, finished, likes, liked)
            : null;
    }

    private static BlockReply? ReadBlocked(JsonElement root) => Count(root, "blocked") is { } blocked ? new BlockReply(blocked) : null;

    private static string? Text(JsonElement root, string key) =>
        root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    // Present as a string or null; false when missing or of another type
    private static bool NullableText(JsonElement root, string key, out string? text)
    {
        text = null;
        if (!root.TryGetProperty(key, out var value))
        {
            return false;
        }
        if (value.ValueKind == JsonValueKind.Null)
        {
            return true;
        }
        text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        return text is not null;
    }

    private static int? Count(JsonElement root, string key) =>
        root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && number >= 0
            ? number
            : null;

    private static bool? Bool(JsonElement root, string key) =>
        root.TryGetProperty(key, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;
}
