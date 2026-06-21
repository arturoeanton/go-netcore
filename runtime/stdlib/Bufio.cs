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

/// <summary>A bufio.Reader over an underlying runtime reader.</summary>
public sealed class GoBufReader { public object? R; }

/// <summary>A bufio.ReadWriter pairing a Reader and a Writer.</summary>
public sealed class GoBufReadWriter { public object R = new GoBufReader(); public object W = new GoBufWriter(); }

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

    // bufio.NewReader / NewReaderSize: buffered reader over an underlying reader.
    public static object NewReader(object? r) => new GoBufReader { R = r };
    public static object NewReaderSize(object? r, long size) => new GoBufReader { R = r };
    public static object?[] Reader_Read(object br, GoSlice p) => Io.ReadFull(((GoBufReader)br).R, p);
    public static object?[] Reader_ReadByte(object br)
    {
        var b = (GoBufReader)br;
        var one = new GoSlice { Data = new object?[1], Off = 0, Len = 1, Cap = 1 };
        var r = Io.ReadFull(b.R, one);
        if (r[1] != null) return new object?[] { 0, r[1] };
        return new object?[] { one.Data![0], null };
    }
    public static void Reader_Reset(object br, object? r) => ((GoBufReader)br).R = r;
    public static long Reader_Buffered(object br) => 0;

    // bufio.ReadWriter (a Reader+Writer pair; h2c prior-knowledge path, dead under goclr).
    public static object RW_Reader(object rw) => ((GoBufReadWriter)rw).R;
    public static object RW_Writer(object rw) => ((GoBufReadWriter)rw).W;
    public static object? RW_Flush(object rw) => Writer_Flush(((GoBufReadWriter)rw).W);
    public static object?[] RW_Read(object rw, GoSlice p) => Reader_Read(((GoBufReadWriter)rw).R, p);

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
