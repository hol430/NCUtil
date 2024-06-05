using System.Text;

namespace NCUtil.Core.Interop;

internal static class StringHandling
{
    private static readonly Encoder utfEncoder;
    private static readonly Decoder utfDecoder;

    static StringHandling()
    {
        Encoding encoding = new UTF8Encoding(false);
        utfDecoder = encoding.GetDecoder();
        utfEncoder = encoding.GetEncoder();
    }

    /// <summary>
    /// Read a string from a pointer to a null-terminated sequence of utf-8
    /// bytes.
    /// </summary>
    unsafe public static string? ReadString(IntPtr p)
    {
        if (p == IntPtr.Zero)
            return null;

        byte* b = (byte*)p;
        byte* z = b;
        while (*z != (byte)0)
            z += 1;

        int count = (int)(z - b);
        if (count == 0)
            return string.Empty;

        var chars = new char[utfDecoder.GetCharCount(b, count, true)];
        fixed (char* c = chars)
            utfDecoder.GetChars(b, count, c, chars.Length, true);
        return new string(chars);
    }

    /// <summary>
    /// Writes strings to a buffer as zero-terminated UTF8-encoded strings.
    /// </summary>
    /// <param name="data">An array of strings to write to a buffer.</param>
    /// <returns>A pair of a buffer with zero-terminated UTF8-encoded strings and an array of offsets to the buffer.
    /// An offset of uint.MaxValue represents null in the data.
    /// </returns>
    unsafe public static (byte[], uint[]) WriteStrings(string[] data)
    {
        // Total length of the buffer.
        uint buflen = 0;

        // Length of each buffer.
        int[] bytecounts = new int[data.Length];

        // Compute buffer offsets.
        for (int i = 0; i < data.Length; i++)
        {
            fixed (char* p = data[i])
                bytecounts[i] = utfEncoder.GetByteCount(p, data[i].Length, true);

            // Guard against overflow.
            if (bytecounts[i] > uint.MaxValue - buflen - 1)
                throw new InternalBufferOverflowException("string buffer cannot exceed 4Gbyte in a single NetCDF operation");

            // Extra byte for the null terminator.
            buflen += (uint)bytecounts[i] + 1;
        }

        // Buffer containing the utf8-encoded strings separated by null terminators.
        byte[] buf = new byte[buflen];

        // The offset of each of the strings within the buffer.
        uint[] offsets = new uint[data.Length];

        // Allocate the buffer and write bytes
        fixed (byte* pbuf = buf)
        {
            int charsUsed;
            int bytesUsed;
            bool isCompleted;
            uint offset = 0;
            for (int i = 0; i < data.Length; i++)
            {
                offsets[i] = offset;
                int bc = bytecounts[i];
                fixed (char* p = data[i])
                    utfEncoder.Convert(p, data[i].Length, pbuf + offset, bc, true, out charsUsed, out bytesUsed, out isCompleted);
                System.Diagnostics.Debug.Assert(charsUsed == data[i].Length && bytesUsed == bc && isCompleted);
                offset += (uint)bc;
                *(pbuf + offset) = (byte)0;
                offset += 1;
            }
        }
        return (buf, offsets);
    }
}
