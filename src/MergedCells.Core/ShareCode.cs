using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MergedCells.Core;

public enum ShareCodeProblem
{
    None,
    NoCode,
    Damaged,
    NewerGame,
}

/// <summary>A share code read from text: the level, or the problem that stopped it (the level is then null).</summary>
public sealed record ShareCodeRead(CustomLevel? Level, ShareCodeProblem Problem);

/// <summary>
/// A custom level as text to copy and paste: <c>MC1-</c> and the level packed into base64url, with a CRC-32. The version 1
/// layout is pinned forever, so every code ever shared keeps importing; a new layout is <c>MC2-</c>. Packing hides
/// nothing: what makes a hostile code harmless is that <see cref="Read"/> refuses anything that isn't a valid level.
/// </summary>
public static partial class ShareCode
{
    public const int CurrentVersion = 1;

    // Bounds on hostile input, not tunable numbers: far past any code the editor makes
    private const int MaxPayloadCharacters = 65536;
    private const int MaxVarintBytes = 10;
    private const ulong MaxUnixSeconds = 253402300799;
    private const int MaxSide = 255;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Whether a code can hold the level: no sticky square, no side over 255, at most 45 teleporter pairs, a name of 1 to
    /// <see cref="CustomLevelNames.MaxBytes"/> bytes, and an edit time from 1970 on. The editor makes nothing else, but a
    /// hand-edited file can.
    /// </summary>
    public static bool CanWrite(CustomLevel level) => Unwritable(level) is null;

    /// <summary>The level's code. Throws <see cref="ArgumentException"/> when <see cref="CanWrite"/> is false.</summary>
    public static string Write(CustomLevel level)
    {
        if (Unwritable(level) is { } problem)
        {
            throw new ArgumentException(problem, nameof(level));
        }
        var map = level.Map;
        var name = Encoding.UTF8.GetBytes(level.Name);
        var seconds = level.Edited.ToUnixTimeSeconds();

        var bytes = new List<byte>();
        var id = new byte[16];
        level.Id.TryWriteBytes(id, bigEndian: true, out _);
        bytes.AddRange(id);
        WriteVarint(bytes, (ulong)seconds);
        WriteVarint(bytes, (ulong)name.Length);
        bytes.AddRange(name);
        bytes.Add((byte)map.Rows);
        bytes.Add((byte)map.Columns);
        bytes.Add((byte)map.Teleporters.Count);
        bytes.Add(map.HasTiles ? (byte)1 : (byte)0);

        var width = IndexWidth(map.Rows, map.Columns);
        var stream = new BitWriter();
        stream.Write(Index(map.Start, map.Columns), width);
        stream.Write(Index(map.End, map.Columns), width);
        for (var row = 0; row < map.Rows; row++)
        {
            for (var column = 0; column < map.Columns; column++)
            {
                var region = map.RegionAt(new Cell(row, column));
                if (column + 1 < map.Columns)
                {
                    stream.Write(map.RegionAt(new Cell(row, column + 1)) == region);
                }
                if (row + 1 < map.Rows)
                {
                    stream.Write(map.RegionAt(new Cell(row + 1, column)) == region);
                }
            }
        }
        if (map.HasTiles)
        {
            for (var row = 0; row < map.Rows; row++)
            {
                for (var column = 0; column < map.Columns; column++)
                {
                    stream.Write((int)map.TileAt(new Cell(row, column)), 2);
                }
            }
        }
        foreach (var pair in map.Teleporters)
        {
            stream.Write(Index(pair.First, map.Columns), width);
            stream.Write(Index(pair.Second, map.Columns), width);
        }
        bytes.AddRange(stream.Bytes);

        var crc = Crc32.Of(bytes.ToArray());
        bytes.AddRange([(byte)crc, (byte)(crc >> 8), (byte)(crc >> 16), (byte)(crc >> 24)]);
        return string.Create(CultureInfo.InvariantCulture, $"MC{CurrentVersion}-{Base64Url(bytes.ToArray())}");
    }

    /// <summary>
    /// Finds the first code in the text (it may sit inside a longer message) and reads it. Never throws for any text: a
    /// code from a newer layout is <see cref="ShareCodeProblem.NewerGame"/>, whatever follows its version, and anything
    /// else wrong with it is <see cref="ShareCodeProblem.Damaged"/>.
    /// </summary>
    public static ShareCodeRead Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var match = Token().Match(text);
        // A bare "MC1-" (as in "codes start with MC1-") isn't a code, and mustn't hide one later in the message
        var current = CurrentVersion.ToString(CultureInfo.InvariantCulture);
        while (match.Success && match.Groups[1].Value == current && match.Groups[2].Length == 0)
        {
            match = match.NextMatch();
        }
        if (!match.Success)
        {
            return Problem(ShareCodeProblem.NoCode);
        }
        var digits = match.Groups[1].Value;
        if (digits.StartsWith('0'))
        {
            return Problem(ShareCodeProblem.Damaged);
        }
        // Too many digits for an int is a version far past this one
        if (digits.Length > 9 || int.Parse(digits, CultureInfo.InvariantCulture) > CurrentVersion)
        {
            return Problem(ShareCodeProblem.NewerGame);
        }
        var payload = match.Groups[2].Value;
        if (payload.Length > MaxPayloadCharacters || payload.Length % 4 == 1)
        {
            return Problem(ShareCodeProblem.Damaged);
        }
        var base64 = payload.Replace('-', '+').Replace('_', '/') + new string('=', (4 - payload.Length % 4) % 4);
        var bytes = new byte[base64.Length / 4 * 3];
        // Decoding ignores the last character's unused bits, so only the one spelling Write makes is accepted
        if (!Convert.TryFromBase64String(base64, bytes, out var length) || length < 4 || Base64Url(bytes.AsSpan(0, length)) != payload)
        {
            return Problem(ShareCodeProblem.Damaged);
        }
        var data = bytes[..(length - 4)];
        var stored = (uint)(bytes[length - 4] | bytes[length - 3] << 8 | bytes[length - 2] << 16 | bytes[length - 1] << 24);
        if (Crc32.Of(data) != stored)
        {
            return Problem(ShareCodeProblem.Damaged);
        }
        try
        {
            return new ShareCodeRead(ReadLevel(data), ShareCodeProblem.None);
        }
        catch (Exception exception) when (exception is FormatException or InvalidMapException or ArgumentException)
        {
            return Problem(ShareCodeProblem.Damaged);
        }
    }

    private static CustomLevel ReadLevel(byte[] data)
    {
        var position = 0;
        byte Next() => position < data.Length ? data[position++] : throw new FormatException("The code ran out.");

        if (data.Length < 16)
        {
            throw new FormatException("The code ran out.");
        }
        var id = new Guid(data.AsSpan(0, 16), bigEndian: true);
        position = 16;
        if (id == Guid.Empty)
        {
            throw new FormatException("The ID is all zeros.");
        }
        var seconds = ReadVarint(Next);
        if (seconds > MaxUnixSeconds)
        {
            throw new FormatException("The edit time is past year 9999.");
        }
        var nameLength = ReadVarint(Next);
        if (nameLength is 0 or > CustomLevelNames.MaxBytes)
        {
            throw new FormatException("The name's length is out of range.");
        }
        var nameBytes = new byte[nameLength];
        for (var index = 0; index < nameBytes.Length; index++)
        {
            nameBytes[index] = Next();
        }
        // Throws DecoderFallbackException, an ArgumentException, for bytes that aren't UTF-8
        var name = StrictUtf8.GetString(nameBytes);
        if (CustomLevelNames.Clean(name, int.MaxValue) != name)
        {
            throw new FormatException("The name isn't clean.");
        }
        var rows = Next();
        var columns = Next();
        var pairCount = Next();
        var tileFlag = Next();
        if (rows == 0 || columns == 0 || rows * columns < 2)
        {
            throw new FormatException("The map is too small.");
        }
        if (pairCount > TeleporterLetters.Count || tileFlag > 1)
        {
            throw new FormatException("The pair count or tile flag is out of range.");
        }

        var squares = rows * columns;
        var width = IndexWidth(rows, columns);
        var stream = new BitReader(data, position, data.Length);
        Cell ReadCell()
        {
            var index = stream.Read(width);
            return index < squares ? new Cell(index / columns, index % columns) : throw new FormatException("A square is off the map.");
        }

        var start = ReadCell();
        var end = ReadCell();
        var joinsRight = new bool[rows, columns];
        var joinsDown = new bool[rows, columns];
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                joinsRight[row, column] = column + 1 < columns && stream.ReadBool();
                joinsDown[row, column] = row + 1 < rows && stream.ReadBool();
            }
        }
        Tile[,]? tiles = null;
        if (tileFlag == 1)
        {
            tiles = new Tile[rows, columns];
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    var code = stream.Read(2);
                    tiles[row, column] = code == (int)Tile.Sticky ? throw new FormatException("Sticky squares can't be shared.") : (Tile)code;
                }
            }
        }
        var pairs = new TeleporterPair[pairCount];
        for (var index = 0; index < pairs.Length; index++)
        {
            pairs[index] = new TeleporterPair(ReadCell(), ReadCell());
        }
        stream.CheckEnd();

        var map = Map.Create(Regions(joinsRight, joinsDown), start, end, tiles, pairs);
        return new CustomLevel(id, name, map, DateTimeOffset.FromUnixTimeSeconds((long)seconds));
    }

    // Each joined area labelled 1, 2, 3… in reading order of its first square, as RegionGrids.Normalise labels them
    private static int[,] Regions(bool[,] joinsRight, bool[,] joinsDown)
    {
        var rows = joinsRight.GetLength(0);
        var columns = joinsRight.GetLength(1);
        var labels = new int[rows, columns];
        var next = 0;
        var queue = new Queue<Cell>();
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                if (labels[row, column] != 0)
                {
                    continue;
                }
                labels[row, column] = ++next;
                queue.Enqueue(new Cell(row, column));
                while (queue.TryDequeue(out var cell))
                {
                    foreach (var neighbour in Joined(cell, joinsRight, joinsDown))
                    {
                        if (labels[neighbour.Row, neighbour.Column] == 0)
                        {
                            labels[neighbour.Row, neighbour.Column] = next;
                            queue.Enqueue(neighbour);
                        }
                    }
                }
            }
        }
        return labels;
    }

    private static IEnumerable<Cell> Joined(Cell cell, bool[,] joinsRight, bool[,] joinsDown)
    {
        var (row, column) = (cell.Row, cell.Column);
        if (joinsRight[row, column])
        {
            yield return new Cell(row, column + 1);
        }
        if (joinsDown[row, column])
        {
            yield return new Cell(row + 1, column);
        }
        if (column > 0 && joinsRight[row, column - 1])
        {
            yield return new Cell(row, column - 1);
        }
        if (row > 0 && joinsDown[row - 1, column])
        {
            yield return new Cell(row - 1, column);
        }
    }

    // Why a code can't hold the level, or null
    private static string? Unwritable(CustomLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);
        var map = level.Map;
        if (map.Rows > MaxSide || map.Columns > MaxSide)
        {
            return $"A share code holds at most {MaxSide} squares a side.";
        }
        if (map.Teleporters.Count > TeleporterLetters.Count)
        {
            return $"A share code holds at most {TeleporterLetters.Count} teleporter pairs.";
        }
        if (Encoding.UTF8.GetByteCount(level.Name) is 0 or > CustomLevelNames.MaxBytes)
        {
            return $"A share code's name is 1 to {CustomLevelNames.MaxBytes} bytes.";
        }
        if (level.Edited.ToUnixTimeSeconds() < 0)
        {
            return "A share code's edit time is from 1970 on.";
        }
        for (var row = 0; row < map.Rows; row++)
        {
            for (var column = 0; column < map.Columns; column++)
            {
                if (map.TileAt(new Cell(row, column)) == Tile.Sticky)
                {
                    return "Sticky squares can't be shared.";
                }
            }
        }
        return null;
    }

    private static string Base64Url(ReadOnlySpan<byte> bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static int Index(Cell cell, int columns) => cell.Row * columns + cell.Column;

    // Enough bits for the last square's index; at least 1
    private static int IndexWidth(int rows, int columns)
    {
        var last = rows * columns - 1;
        var width = 1;
        while (last >> width != 0)
        {
            width++;
        }
        return width;
    }

    private static void WriteVarint(List<byte> bytes, ulong value)
    {
        do
        {
            var low = (byte)(value & 0x7F);
            value >>= 7;
            bytes.Add(value == 0 ? low : (byte)(low | 0x80));
        }
        while (value != 0);
    }

    private static ulong ReadVarint(Func<byte> next)
    {
        var value = 0ul;
        for (var index = 0; index < MaxVarintBytes; index++)
        {
            var part = next();
            value |= (ulong)(part & 0x7F) << (7 * index);
            if ((part & 0x80) == 0)
            {
                return value;
            }
        }
        throw new FormatException("A number runs past ten bytes.");
    }

    private static ShareCodeRead Problem(ShareCodeProblem problem) => new(null, problem);

    [GeneratedRegex("MC([0-9]+)-([A-Za-z0-9_-]*)")]
    private static partial Regex Token();
}
