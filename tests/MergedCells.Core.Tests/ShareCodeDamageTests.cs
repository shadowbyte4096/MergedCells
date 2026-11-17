using System.Text;

namespace MergedCells.Core.Tests;

// Each row breaks one rule in an otherwise good code. Rows about the content re-sign the bytes with a correct checksum,
// so the reader's own check is what refuses them
public class ShareCodeDamageTests
{
    // The pinned code's layout: 0–15 the ID, 16–20 the edited time, 21 the name's length, 22–35 the name, 36 rows,
    // 37 columns, 38 the pair count, 39 the tile flag, 40–41 the bit stream, then the checksum
    private const int NameLength = 21;
    private const int Rows = 36;
    private const int Pairs = 38;
    private const int TileFlag = 39;
    private const int Stream = 40;

    private static List<byte> Payload(string code)
    {
        var text = code[4..].Replace('-', '+').Replace('_', '/');
        var bytes = Convert.FromBase64String(text + new string('=', (4 - text.Length % 4) % 4));
        return bytes[..^4].ToList();
    }

    private static string Signed(IEnumerable<byte> payload, string prefix = "MC1-")
    {
        var bytes = payload.ToList();
        var crc = Crc32.Of(bytes.ToArray());
        bytes.AddRange([(byte)crc, (byte)(crc >> 8), (byte)(crc >> 16), (byte)(crc >> 24)]);
        return prefix + Convert.ToBase64String(bytes.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static List<byte> Pinned() => Payload(ShareCodeTests.PinnedCode);

    private static List<byte> Varint(ulong value)
    {
        var bytes = new List<byte>();
        do
        {
            var low = (byte)(value & 0x7F);
            value >>= 7;
            bytes.Add(value == 0 ? low : (byte)(low | 0x80));
        }
        while (value != 0);
        return bytes;
    }

    private static string WithName(byte[] name)
    {
        var bytes = Pinned();
        bytes.RemoveRange(NameLength, 15);
        bytes.InsertRange(NameLength, Varint((ulong)name.Length).Concat(name));
        return Signed(bytes);
    }

    private static string WithEdited(List<byte> varint)
    {
        var bytes = Pinned();
        bytes.RemoveRange(16, 5);
        bytes.InsertRange(16, varint);
        return Signed(bytes);
    }

    private static string WithByte(int index, byte value)
    {
        var bytes = Pinned();
        bytes[index] = value;
        return Signed(bytes);
    }

    // The pinned map with a trap at [0, 1]: its code, 3, sits in stream bits 15 and 16, so clearing bit 15 makes it 2
    private static string StickyCode()
    {
        var trapped = ShareCodeTests.PinnedLevel() with
        {
            Map = Map.Create(new[,] { { 1, 1, 2 }, { 3, 3, 4 } }, new(0, 0), new(1, 2), TestMaps.Traps(2, 3, (0, 1))),
        };
        var bytes = Payload(ShareCode.Write(trapped));
        bytes[Stream + 1] &= 0x7F;
        return Signed(bytes);
    }

    public static TheoryData<string, string> Damaged => new()
    {
        { "a changed character", ShareCodeTests.PinnedCode.Replace("Gktt", "Gktu") },
        { "cut short", ShareCodeTests.PinnedCode[..^4] },
        // w and x differ only in the last character's two unused bits, which base64 decoding ignores
        { "a changed last character", ShareCodeTests.PinnedCode[..^1] + "x" },
        { "a byte left over", Signed(Pinned().Append((byte)0)) },
        { "version 0", ShareCodeTests.PinnedCode.Replace("MC1-", "MC0-") },
        { "a leading zero", ShareCodeTests.PinnedCode.Replace("MC1-", "MC01-") },
        { "a set pad bit", WithByte(Stream + 1, 0x88) },
        { "an all-zero ID", Signed(Enumerable.Repeat((byte)0, 16).Concat(Pinned().Skip(16))) },
        { "an empty name", WithName([]) },
        { "a name over the byte cap", WithName(Encoding.UTF8.GetBytes(CustomLevelNamesTests.NameOfBytes(513))) },
        { "a name that isn't UTF-8", WithName([0xC3, 0x28]) },
        { "an untrimmed name", WithName(Encoding.UTF8.GetBytes(" Long")) },
        { "a name with a hidden character", WithName(Encoding.UTF8.GetBytes("Long‮Way")) },
        { "no rows", WithByte(Rows, 0) },
        { "46 pairs", WithByte(Pairs, 46) },
        { "tile flag 2", WithByte(TileFlag, 2) },
        { "a sticky tile", StickyCode() },
        // The end's index 5 becomes 6, one past the last square
        { "an index off the map", WithByte(Stream, 0x70) },
        // The square left of the end joins it, so the end isn't a block of its own
        { "a map Map.Create refuses", WithByte(Stream + 1, 0x18) },
        { "an 11-byte varint", WithEdited([.. Enumerable.Repeat((byte)0x80, 10), 0x01]) },
        { "a time past year 9999", WithEdited(Varint(253402300800)) },
        // Refused before decoding (a cost guard: the checksum would refuse it too); Largest_code_the_format_allows_reads keeps
        // the bound above every real code
        { "a payload over the length bound", "MC1-" + new string('A', 65540) },
    };

    [Theory]
    [MemberData(nameof(Damaged))]
    public void Damaged_code_is_refused(string why, string code)
    {
        var read = ShareCode.Read(code);

        Assert.True(read == new ShareCodeRead(null, ShareCodeProblem.Damaged), $"{why}: read as {read.Problem}");
    }

    // The re-signing helper must itself make codes that read, or every row above proves nothing
    [Fact]
    public void Re_signed_pinned_payload_reads()
    {
        Assert.Equal(ShareCodeProblem.None, ShareCode.Read(Signed(Pinned())).Problem);
    }

    [Fact]
    public void Re_signed_trapped_payload_reads_before_its_tile_is_changed()
    {
        var trapped = ShareCodeTests.PinnedLevel() with
        {
            Map = Map.Create(new[,] { { 1, 1, 2 }, { 3, 3, 4 } }, new(0, 0), new(1, 2), TestMaps.Traps(2, 3, (0, 1))),
        };

        Assert.Equal(Tile.Trap, ShareCode.Read(Signed(Payload(ShareCode.Write(trapped)))).Level!.Map.TileAt(new(0, 1)));
    }
}
