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
}
