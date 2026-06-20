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

    public static long RuneCountInString2(GoString s) => RuneCountInString(s);
}
