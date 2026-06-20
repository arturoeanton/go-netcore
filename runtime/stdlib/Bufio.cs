namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>A bufio.Scanner over an in-memory snapshot of a reader's bytes.</summary>
public sealed class GoScanner
{
    public byte[] Data = System.Array.Empty<byte>();
    public int Pos;
    public int Mode; // 0 = lines, 1 = words, 2 = runes
    public byte[] Cur = System.Array.Empty<byte>();
}

/// <summary>Shim for a subset of Go's <c>bufio</c> (Scanner over a runtime reader).</summary>
public static class Bufio
{
    public static object NewScanner(object? r) => new GoScanner { Data = Readers.Drain(r) };

    public static void Scanner_Split(object s, object split)
    {
        // split is bufio.ScanLines/ScanWords/ScanRunes (a shim var marker).
        ((GoScanner)s).Mode = split is long m ? (int)m : 0;
    }
    public static long ScanLinesMarker() => 0;
    public static long ScanWordsMarker() => 1;
    public static long ScanRunesMarker() => 2;

    public static bool Scanner_Scan(object so)
    {
        var s = (GoScanner)so;
        if (s.Mode == 1) // words: skip leading spaces, take until space
        {
            while (s.Pos < s.Data.Length && IsSpace(s.Data[s.Pos])) s.Pos++;
            if (s.Pos >= s.Data.Length) return false;
            int start = s.Pos;
            while (s.Pos < s.Data.Length && !IsSpace(s.Data[s.Pos])) s.Pos++;
            s.Cur = Sub(s.Data, start, s.Pos);
            return true;
        }
        // lines
        if (s.Pos >= s.Data.Length) return false;
        int ls = s.Pos;
        while (s.Pos < s.Data.Length && s.Data[s.Pos] != (byte)'\n') s.Pos++;
        int le = s.Pos;
        if (le > ls && s.Data[le - 1] == (byte)'\r') le--; // strip trailing \r
        s.Cur = Sub(s.Data, ls, le);
        if (s.Pos < s.Data.Length) s.Pos++; // skip the \n
        return true;
    }

    public static GoString Scanner_Text(object so) => GoString.FromBytes(((GoScanner)so).Cur);
    // The scanner reads from a fully-drained in-memory buffer, so it never errors.
    public static object? Scanner_Err(object so) => null;
    public static GoSlice Scanner_Bytes(object so)
    {
        var c = ((GoScanner)so).Cur;
        var d = new object?[c.Length];
        for (int i = 0; i < c.Length; i++) d[i] = (int)c[i];
        return new GoSlice { Data = d, Off = 0, Len = c.Length, Cap = c.Length };
    }

    private static bool IsSpace(byte b) => b == ' ' || b == '\t' || b == '\n' || b == '\r' || b == '\f' || b == '\v';
    private static byte[] Sub(byte[] b, int from, int to) { var r = new byte[to - from]; System.Array.Copy(b, from, r, 0, to - from); return r; }
}
