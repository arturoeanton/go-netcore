namespace GoCLR.Stdlib;

using System.Text;
using GoCLR.Runtime;

/// <summary>Shim for Go's <c>unicode/utf8</c> package.</summary>
public static class Utf8
{
    public static long RuneCountInString(GoString s) => RuneCountBytes(s.Bytes);

    public static long RuneCount(GoSlice b)
    {
        var bytes = new byte[b.Len];
        for (int i = 0; i < b.Len; i++) bytes[i] = System.Convert.ToByte(b.Data[b.Off + i]);
        return RuneCountBytes(bytes);
    }

    // Counts runes the way Go does: each invalid byte counts as one rune (RuneError),
    // matching DecodeRune's 1-byte advance, not the .NET string's merged replacement.
    private static long RuneCountBytes(byte[] p)
    {
        long n = 0;
        for (int i = 0; i < p.Length;)
        {
            var (_, size) = DecodeRuneBytes(p, i, p.Length - i);
            i += size < 1 ? 1 : size;
            n++;
        }
        return n;
    }

    public static bool ValidString(GoString s)
    {
        var bytes = s.Bytes;
        try { var d = new UTF8Encoding(false, true).GetString(bytes); return true; }
        catch { return false; }
    }

    public static bool ValidRune(int r) => System.Text.Rune.IsValid(r);

    public static long RuneLen(int r)
    {
        if (!System.Text.Rune.IsValid(r)) return -1;
        return new System.Text.Rune(r).Utf8SequenceLength;
    }

    public static bool Valid(GoSlice b)
    {
        var bytes = new byte[b.Len];
        for (int i = 0; i < b.Len; i++) bytes[i] = (byte)System.Convert.ToInt64(b.Data[b.Off + i]);
        try { new UTF8Encoding(false, true).GetString(bytes); return true; } catch { return false; }
    }

    // utf8.EncodeRune(p []byte, r rune) int — write r's UTF-8 into p, return count.
    public static long EncodeRune(GoSlice p, int r)
    {
        var rune = System.Text.Rune.IsValid(r) ? new System.Text.Rune(r) : System.Text.Rune.ReplacementChar;
        var bytes = Encoding.UTF8.GetBytes(rune.ToString());
        for (int i = 0; i < bytes.Length && i < p.Len; i++) p.Data[p.Off + i] = (int)bytes[i];
        return bytes.Length;
    }

    // utf8.AppendRune(p []byte, r rune) []byte — append r's UTF-8 encoding to p.
    public static GoSlice AppendRune(GoSlice p, int r)
    {
        var rune = System.Text.Rune.IsValid(r) ? new System.Text.Rune(r) : System.Text.Rune.ReplacementChar;
        var bytes = Encoding.UTF8.GetBytes(rune.ToString());
        var add = new object?[bytes.Length];
        for (int i = 0; i < bytes.Length; i++) add[i] = (int)bytes[i];
        return Rt.AppendSlice(p, new GoSlice { Data = add, Off = 0, Len = bytes.Length, Cap = bytes.Length });
    }

    // Decode the first rune from p[off..off+len) using Go's exact UTF-8 rules: an invalid
    // leading/continuation byte, truncated, overlong, surrogate, or out-of-range sequence
    // yields (RuneError, 1) — never the .NET string round-trip, which mangles invalid bytes.
    private static (int r, int size) DecodeRuneBytes(byte[] p, int off, int len)
    {
        if (len < 1) return (0xFFFD, 0);
        byte p0 = p[off];
        if (p0 < 0x80) return (p0, 1); // ASCII
        int sz, mask, lo = 0x80, hi = 0xBF;
        if (p0 < 0xC2) return (0xFFFD, 1);          // continuation byte, or 0xC0/0xC1 overlong
        else if (p0 < 0xE0) { sz = 2; mask = 0x1F; }
        else if (p0 < 0xF0)
        {
            sz = 3; mask = 0x0F;
            if (p0 == 0xE0) lo = 0xA0;              // guard overlong
            else if (p0 == 0xED) hi = 0x9F;         // guard surrogates
        }
        else if (p0 < 0xF5)
        {
            sz = 4; mask = 0x07;
            if (p0 == 0xF0) lo = 0x90;
            else if (p0 == 0xF4) hi = 0x8F;
        }
        else return (0xFFFD, 1);                     // 0xF5..0xFF invalid
        if (len < sz) return (0xFFFD, 1);
        byte b1 = p[off + 1];
        if (b1 < lo || b1 > hi) return (0xFFFD, 1);
        int r = (p0 & mask) << 6 | (b1 & 0x3F);
        if (sz == 2) return (r, 2);
        byte b2 = p[off + 2];
        if (b2 < 0x80 || b2 > 0xBF) return (0xFFFD, 1);
        r = r << 6 | (b2 & 0x3F);
        if (sz == 3) return (r, 3);
        byte b3 = p[off + 3];
        if (b3 < 0x80 || b3 > 0xBF) return (0xFFFD, 1);
        return (r << 6 | (b3 & 0x3F), 4);
    }

    public static object?[] DecodeRuneInString(GoString s)
    {
        var b = s.Bytes;
        var (r, size) = DecodeRuneBytes(b, 0, b.Length);
        return new object?[] { r, (long)size };
    }

    public static object?[] DecodeRune(GoSlice b)
    {
        var bytes = new byte[b.Len];
        for (int i = 0; i < b.Len; i++) bytes[i] = (byte)System.Convert.ToInt64(b.Data[b.Off + i]);
        var (r, size) = DecodeRuneBytes(bytes, 0, bytes.Length);
        return new object?[] { r, (long)size };
    }

    public static object?[] DecodeLastRuneInString(GoString s) => DecodeLastRuneBytes(s.Bytes);
    public static object?[] DecodeLastRune(GoSlice b)
    {
        var bytes = new byte[b.Len];
        for (int i = 0; i < b.Len; i++) bytes[i] = (byte)System.Convert.ToInt64(b.Data[b.Off + i]);
        return DecodeLastRuneBytes(bytes);
    }

    // Go's DecodeLastRune: scan back to the rune start (at most 4 bytes), decode, and
    // require it to end exactly at the input's end (else the trailing bytes are invalid).
    private static object?[] DecodeLastRuneBytes(byte[] p)
    {
        int end = p.Length;
        if (end == 0) return new object?[] { 0xFFFD, 0L };
        int start = end - 1;
        if (p[start] < 0x80) return new object?[] { (int)p[start], 1L }; // ASCII
        int lim = end - 4; if (lim < 0) lim = 0;
        for (start--; start >= lim; start--) if ((p[start] & 0xC0) != 0x80) break; // a non-continuation byte
        if (start < 0) start = 0;
        var (r, size) = DecodeRuneBytes(p, start, end - start);
        if (start + size != end) return new object?[] { 0xFFFD, 1L };
        return new object?[] { r, (long)size };
    }

    private static int RuneByteLen(byte b0)
    {
        if (b0 < 0x80) return 1;
        if ((b0 & 0xE0) == 0xC0) return 2;
        if ((b0 & 0xF0) == 0xE0) return 3;
        if ((b0 & 0xF8) == 0xF0) return 4;
        return 1; // invalid lead byte: a single (error) rune
    }
    // RuneStart reports whether b is the first byte of an encoded rune.
    public static bool RuneStart(int b) => (b & 0xC0) != 0x80;

    public static bool FullRune(GoSlice p)
    {
        if (p.Len == 0) return false;
        int need = RuneByteLen((byte)(System.Convert.ToInt64(p.Data![p.Off]) & 0xff));
        return p.Len >= need;
    }
    public static bool FullRuneInString(GoString s)
    {
        var b = s.Bytes;
        if (b.Length == 0) return false;
        return b.Length >= RuneByteLen(b[0]);
    }

    public static long RuneCountInString2(GoString s) => RuneCountInString(s);
}
