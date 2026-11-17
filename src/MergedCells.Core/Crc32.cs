namespace MergedCells.Core;

// CRC-32 as zlib and Ethernet compute it (reflected polynomial 0xEDB88320), for share codes. Part of their pinned layout
internal static class Crc32
{
    private static readonly uint[] Table = BuildTable();

    internal static uint Of(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in data)
        {
            crc = Table[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }
        return crc ^ 0xFFFFFFFFu;
    }

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (var index = 0u; index < 256; index++)
        {
            var value = index;
            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? 0xEDB88320u ^ (value >> 1) : value >> 1;
            }
            table[index] = value;
        }
        return table;
    }
}
