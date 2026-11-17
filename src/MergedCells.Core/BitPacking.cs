namespace MergedCells.Core;

// Bits packed least significant first, the last byte padded with zeros: the share code's bit stream
internal sealed class BitWriter
{
    private readonly List<byte> bytes = new();
    private int bitCount;

    internal void Write(int value, int width)
    {
        for (var bit = 0; bit < width; bit++)
        {
            if (bitCount % 8 == 0)
            {
                bytes.Add(0);
            }
            if (((value >> bit) & 1) != 0)
            {
                bytes[^1] |= (byte)(1 << (bitCount % 8));
            }
            bitCount++;
        }
    }

    internal void Write(bool value) => Write(value ? 1 : 0, 1);

    internal IReadOnlyList<byte> Bytes => bytes;
}

// Reads what BitWriter wrote. Running past the end, or a set pad bit at the end, throws FormatException
internal sealed class BitReader(byte[] bytes, int start, int end)
{
    private int bit = start * 8;

    internal int Read(int width)
    {
        var value = 0;
        for (var index = 0; index < width; index++)
        {
            if (bit >= end * 8)
            {
                throw new FormatException("The bit stream ran out.");
            }
            if ((bytes[bit / 8] & (1 << (bit % 8))) != 0)
            {
                value |= 1 << index;
            }
            bit++;
        }
        return value;
    }

    internal bool ReadBool() => Read(1) == 1;

    // The stream must fill its bytes exactly, with only zero bits after its last value
    internal void CheckEnd()
    {
        if ((bit + 7) / 8 != end)
        {
            throw new FormatException("The bit stream has bytes left over.");
        }
        while (bit % 8 != 0)
        {
            if (Read(1) != 0)
            {
                throw new FormatException("A pad bit is set.");
            }
        }
    }
}
