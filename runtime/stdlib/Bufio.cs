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
public sealed class GoBufReader
{
    public object? R;                                          // underlying reader
    public System.Collections.Generic.List<byte> Buf = new();  // bytes read from R, not yet consumed
    public int Pos;                                            // read cursor into Buf
}

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
    // bufio sentinel errors.
    public static readonly GoError ErrBufferFullSentinel = new(GoString.FromDotNetString("bufio: buffer full"));
    public static object ErrBufferFull() => ErrBufferFullSentinel;
    public static readonly GoError ErrNegativeCountSentinel = new(GoString.FromDotNetString("bufio: negative count"));
    public static object ErrNegativeCount() => ErrNegativeCountSentinel;

    public static object NewScanner(object? r) => new GoScanner { Data = Readers.Drain(r) };

    // bufio.NewReader / NewReaderSize: buffered reader over an underlying reader.
    public static object NewReader(object? r) => new GoBufReader { R = r };
    public static object NewReaderSize(object? r, long size) => new GoBufReader { R = r };

    private const int FillBlock = 4096; // bufio's default buffer size
    private static int Avail(GoBufReader b) => b.Buf.Count - b.Pos;

    // ReadOnce performs ONE Read on the underlying reader (up to buf.Len bytes), returning
    // [n, err]. Crucially it is a single Read — NOT Io.ReadFull, which loops until the buffer
    // is full and would block forever on a live socket that delivers a request smaller than
    // the buffer (the fasthttp serving path). Each known reader type and a bridge reader get
    // a single Read; the fallback drains an opaque in-memory reader.
    private static object?[] ReadOnce(object? r, GoSlice buf) => r switch
    {
        GoReader => Readers.Reader_Read(r, buf),
        GoBuffer => BytesBuffer.Read(r, buf),
        GoConn => Net.Conn_Read(r, buf),
        not null when Bridge.HasMethod(r, "Read") => Bridge.CallMethod(r, "Read", new object?[] { buf }) as object?[] ?? new object?[] { 0L, Io.EOFSentinel },
        _ => Io.ReadFull(r, buf),
    };

    // Ensure at least `need` bytes are buffered ahead of Pos. Each iteration does ONE Read of
    // up to a buffer-block (a socket Read returns whatever is available without blocking for
    // more; an in-memory Read returns all it has). Returns the read error if it occurred
    // before reaching `need`, else null.
    private static object? Fill(GoBufReader b, int need)
    {
        while (Avail(b) < need)
        {
            int want = System.Math.Max(need - Avail(b), FillBlock);
            var tmp = new GoSlice { Data = new object?[want], Off = 0, Len = want, Cap = want };
            var r = ReadOnce(b.R, tmp);
            int n = (int)System.Convert.ToInt64(r[0] ?? 0L);
            for (int i = 0; i < n; i++) b.Buf.Add((byte)(System.Convert.ToInt64(tmp.Data![i]) & 0xff));
            object? err = r.Length > 1 ? r[1] : null;
            if (Avail(b) >= need) return null;       // got enough
            if (n == 0 || err != null) return err ?? Io.EOFSentinel;
        }
        return null;
    }

    public static object?[] Reader_Read(object br, GoSlice p)
    {
        var b = (GoBufReader)br;
        if (p.Len == 0) return new object?[] { 0L, null };
        // Serve from the buffer if anything is left; otherwise read one chunk into it.
        if (Avail(b) == 0) { var e = Fill(b, 1); if (Avail(b) == 0) return new object?[] { 0L, e }; }
        int n = System.Math.Min(p.Len, Avail(b));
        for (int i = 0; i < n; i++) p.Data![p.Off + i] = (int)b.Buf[b.Pos + i];
        b.Pos += n;
        return new object?[] { (long)n, null };
    }
    public static object?[] Reader_ReadByte(object br)
    {
        var b = (GoBufReader)br;
        var e = Fill(b, 1);
        if (Avail(b) == 0) return new object?[] { 0, e ?? Io.EOFSentinel };
        return new object?[] { (int)b.Buf[b.Pos++], null };
    }
    // bufio.Reader.UnreadByte: step the cursor back so the next ReadByte returns the last byte.
    public static object? Reader_UnreadByte(object br)
    {
        var b = (GoBufReader)br;
        if (b.Pos == 0) return new GoError(GoString.FromDotNetString("bufio: invalid use of UnreadByte"));
        b.Pos--;
        return null;
    }
    // bufio.Reader.Peek(n) ([]byte, error): return the next n bytes without consuming them.
    public static object?[] Reader_Peek(object br, long n)
    {
        var b = (GoBufReader)br;
        int want = (int)n;
        var e = Fill(b, want);
        int got = System.Math.Min(want, Avail(b));
        var data = new object?[got];
        for (int i = 0; i < got; i++) data[i] = (int)b.Buf[b.Pos + i];
        var slice = new GoSlice { Data = data, Off = 0, Len = got, Cap = got };
        return new object?[] { slice, got < want ? (e ?? Io.EOFSentinel) : null };
    }
    // bufio.Reader.Discard(n) (int, error): skip the next n bytes.
    public static object?[] Reader_Discard(object br, long n)
    {
        var b = (GoBufReader)br;
        int want = (int)n;
        var e = Fill(b, want);
        int got = System.Math.Min(want, Avail(b));
        b.Pos += got;
        return new object?[] { (long)got, got < want ? (e ?? Io.EOFSentinel) : null };
    }
    public static void Reader_Reset(object br, object? r) { var b = (GoBufReader)br; b.R = r; b.Buf.Clear(); b.Pos = 0; }
    public static long Reader_Buffered(object br) => Avail((GoBufReader)br);

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
