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

/// <summary>A bufio.Writer buffering bytes over an underlying writer.</summary>
public sealed class GoBufWriter
{
    public object? W;
    public int Size = 4096;
    public readonly System.Collections.Generic.List<byte> Buf = new();
}

/// <summary>Shim for a subset of Go's <c>bufio</c> (Scanner over a runtime reader,
/// Writer over a runtime writer).</summary>
public static class Bufio
{
    public static object NewScanner(object? r) => new GoScanner { Data = Readers.Drain(r) };

    // bufio.NewWriter / NewWriterSize.
    public static object NewWriter(object? w) => new GoBufWriter { W = w };
    public static object NewWriterSize(object? w, long size) => new GoBufWriter { W = w, Size = size > 0 ? (int)size : 4096 };

    public static long Writer_Available(object bw) { var b = (GoBufWriter)bw; return b.Size - b.Buf.Count; }
    public static long Writer_Buffered(object bw) => ((GoBufWriter)bw).Buf.Count;
    public static object?[] Writer_Write(object bw, GoSlice p)
    {
        var b = (GoBufWriter)bw;
        for (int i = 0; i < p.Len; i++) b.Buf.Add((byte)System.Convert.ToInt64(p.Data![p.Off + i]));
        return new object?[] { (long)p.Len, null };
    }
    public static object? Writer_WriteByte(object bw, int c) { ((GoBufWriter)bw).Buf.Add((byte)c); return null; }
    public static object?[] Writer_WriteString(object bw, GoString s)
    {
        var b = (GoBufWriter)bw;
        var by = s.Bytes;
        b.Buf.AddRange(by);
        return new object?[] { (long)by.Length, null };
    }
    public static object? Writer_Flush(object bw)
    {
        var b = (GoBufWriter)bw;
        if (b.Buf.Count > 0) { Compress.WriteRaw(b.W, b.Buf.ToArray()); b.Buf.Clear(); }
        return null;
    }
    public static void Writer_Reset(object bw, object? w) { var b = (GoBufWriter)bw; b.W = w; b.Buf.Clear(); }

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
