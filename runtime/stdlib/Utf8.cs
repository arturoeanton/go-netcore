namespace GoCLR.Stdlib;

using System.Text;
using GoCLR.Runtime;

/// <summary>Shim for Go's <c>unicode/utf8</c> package.</summary>
public static class Utf8
{
    public static long RuneCountInString(GoString s)
    {
        long n = 0;
        foreach (var _ in s.ToDotNetString().EnumerateRunes()) n++;
        return n;
    }

    public static long RuneCount(GoSlice b)
    {
        var bytes = new byte[b.Len];
        for (int i = 0; i < b.Len; i++) bytes[i] = System.Convert.ToByte(b.Data[b.Off + i]);
        long n = 0;
        foreach (var _ in Encoding.UTF8.GetString(bytes).EnumerateRunes()) n++;
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

    public static object?[] DecodeRuneInString(GoString s)
    {
        var str = s.ToDotNetString();
        if (str.Length == 0) return new object?[] { (int)0xFFFD, 0L };
        var e = str.EnumerateRunes().GetEnumerator();
        e.MoveNext();
        var r = e.Current;
        return new object?[] { r.Value, (long)r.Utf8SequenceLength };
    }

    public static object?[] DecodeRune(GoSlice b)
    {
        var bytes = new byte[b.Len];
        for (int i = 0; i < b.Len; i++) bytes[i] = (byte)System.Convert.ToInt64(b.Data[b.Off + i]);
        return DecodeRuneInString(GoString.FromBytes(bytes));
    }

    public static object?[] DecodeLastRuneInString(GoString s)
    {
        var str = s.ToDotNetString();
        bool any = false;
        System.Text.Rune last = default;
        foreach (var r in str.EnumerateRunes()) { last = r; any = true; }
        if (!any) return new object?[] { (int)0xFFFD, 0L };
        return new object?[] { last.Value, (long)last.Utf8SequenceLength };
    }

    public static object?[] DecodeLastRune(GoSlice b)
    {
        var bytes = new byte[b.Len];
        for (int i = 0; i < b.Len; i++) bytes[i] = (byte)System.Convert.ToInt64(b.Data[b.Off + i]);
        return DecodeLastRuneInString(GoString.FromBytes(bytes));
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
