namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>Shim for encoding/ascii85 (the btoa 4-byte → 5-char base-85 form Go uses,
/// without the Adobe &lt;~ ~&gt; wrapper). Ports Go's Encode/Decode/MaxEncodedLen
/// byte-for-byte, including the 'z' zero-group shortcut and partial-group flush.</summary>
public static class Ascii85
{
    public static long MaxEncodedLen(long n) => (n + 3) / 4 * 5;

    private static byte[] Bytes(GoSlice s)
    {
        var b = new byte[s.Len];
        for (int i = 0; i < s.Len; i++) b[i] = (byte)System.Convert.ToInt64(s.Data![s.Off + i]);
        return b;
    }

    // ascii85.Encode(dst, src) int: encode src into dst (caller-sized via MaxEncodedLen),
    // returning the number of bytes written.
    public static long Encode(GoSlice dst, GoSlice src)
    {
        var s = Bytes(src);
        int n = 0, di = dst.Off, pos = 0, rem = s.Length;
        while (rem > 0)
        {
            uint v = 0;
            switch (rem)
            {
                default: v |= s[pos + 3]; goto case 3;
                case 3: v |= (uint)s[pos + 2] << 8; goto case 2;
                case 2: v |= (uint)s[pos + 1] << 16; goto case 1;
                case 1: v |= (uint)s[pos] << 24; break;
            }
            // A full zero group shortens to a single 'z'.
            if (v == 0 && rem >= 4)
            {
                dst.Data![di++] = (int)(byte)'z';
                pos += 4; rem -= 4; n++;
                continue;
            }
            var enc = new byte[5];
            for (int i = 4; i >= 0; i--) { enc[i] = (byte)('!' + v % 85); v /= 85; }
            int m = 5;
            if (rem < 4) { m -= 4 - rem; rem = 0; }
            else { pos += 4; rem -= 4; }
            for (int i = 0; i < m; i++) dst.Data![di++] = (int)enc[i];
            n += m;
        }
        return n;
    }

    // ascii85.Decode(dst, src, flush) (ndst, nsrc int, err error).
    public static object?[] Decode(GoSlice dst, GoSlice src, bool flush)
    {
        var s = Bytes(src);
        int ndst = 0, nsrc = 0, di = dst.Off;
        uint v = 0;
        int nb = 0;
        for (int i = 0; i < s.Length; i++)
        {
            if (dst.Len - ndst < 4) return new object?[] { (long)ndst, (long)nsrc, null };
            byte b = s[i];
            if (b <= ' ') continue;                         // skip whitespace
            else if (b == 'z' && nb == 0) { nb = 5; v = 0; } // 'z' = four zero bytes
            else if (b >= '!' && b <= 'u') { v = v * 85 + (uint)(b - '!'); nb++; }
            else return new object?[] { (long)ndst, (long)nsrc, CorruptErr(i) };

            if (nb == 5)
            {
                nsrc = i + 1;
                dst.Data![di] = (int)(byte)(v >> 24);
                dst.Data![di + 1] = (int)(byte)(v >> 16);
                dst.Data![di + 2] = (int)(byte)(v >> 8);
                dst.Data![di + 3] = (int)(byte)v;
                di += 4; ndst += 4; nb = 0; v = 0;
            }
        }
        if (flush)
        {
            nsrc = s.Length;
            if (nb > 0)
            {
                for (int i = nb; i < 5; i++) v = v * 85 + 84;
                for (int i = 0; i < nb - 1; i++) { dst.Data![di] = (int)(byte)(v >> 24); v <<= 8; di++; ndst++; }
            }
        }
        return new object?[] { (long)ndst, (long)nsrc, null };
    }

    private static GoError CorruptErr(int i) =>
        new(GoString.FromDotNetString("illegal ascii85 data at input byte " + i));
}
