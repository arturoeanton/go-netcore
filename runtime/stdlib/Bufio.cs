namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>A bufio.Scanner over an in-memory snapshot of a reader's bytes.</summary>
public sealed class GoScanner
{
    public byte[] Data = System.Array.Empty<byte>();
    public int Pos;
    public int Mode; // 0 = lines, 1 = words, 2 = runes, 3 = bytes, -1 = custom SplitFunc
    public byte[] Cur = System.Array.Empty<byte>();
    public GoClosure? Split; // a user SplitFunc when Mode == -1
    public int Empties;      // consecutive empty tokens at EOF without progress
    public long Max = 64 * 1024; // bufio.MaxScanTokenSize; Buffer(buf, max) overrides it
    public object? Err;      // ErrTooLong once a token exceeds Max
}

/// <summary>A bufio.Reader over an underlying runtime reader.</summary>
public sealed class GoBufReader
{
    public object? R;                                          // underlying reader
    public System.Collections.Generic.List<byte> Buf = new();  // bytes read from R, not yet consumed
    public int Pos;                                            // read cursor into Buf
    public int Size = 4096;                                    // logical buffer size (bufio default 4096)
    public int LastRuneSize = -1;                              // size of the rune from the last ReadRune, else -1
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
    public static readonly GoError ErrInvalidUnreadByteSentinel = new(GoString.FromDotNetString("bufio: invalid use of UnreadByte"));
    public static object ErrInvalidUnreadByte() => ErrInvalidUnreadByteSentinel;
    public static readonly GoError ErrInvalidUnreadRuneSentinel = new(GoString.FromDotNetString("bufio: invalid use of UnreadRune"));
    public static object ErrInvalidUnreadRune() => ErrInvalidUnreadRuneSentinel;
    public static readonly GoError ErrTooLongSentinel = new(GoString.FromDotNetString("bufio.Scanner: token too long"));
    public static object ErrTooLong() => ErrTooLongSentinel;
    public static readonly GoError ErrNegativeAdvanceSentinel = new(GoString.FromDotNetString("bufio.Scanner: SplitFunc returns negative advance count"));
    public static object ErrNegativeAdvance() => ErrNegativeAdvanceSentinel;
    public static readonly GoError ErrAdvanceTooFarSentinel = new(GoString.FromDotNetString("bufio.Scanner: SplitFunc returns advance count beyond input"));
    public static object ErrAdvanceTooFar() => ErrAdvanceTooFarSentinel;
    public static readonly GoError ErrBadReadCountSentinel = new(GoString.FromDotNetString("bufio.Scanner: Read returned impossible count"));
    public static object ErrBadReadCount() => ErrBadReadCountSentinel;
    public static readonly GoError ErrFinalTokenSentinel = new(GoString.FromDotNetString("final token"));
    public static object ErrFinalToken() => ErrFinalTokenSentinel;

    public static object NewScanner(object? r) => new GoScanner { Data = Readers.Drain(r) };

    // bufio.NewReader / NewReaderSize: buffered reader over an underlying reader.
    public static object NewReader(object? r) => new GoBufReader { R = r };
    public static object NewReaderSize(object? r, long size) => new GoBufReader { R = r, Size = size < 16 ? 16 : (int)size };

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
        var b = AsReader(br);
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
        var b = AsReader(br);
        var e = Fill(b, 1);
        if (Avail(b) == 0) return new object?[] { 0, e ?? Io.EOFSentinel };
        return new object?[] { (int)b.Buf[b.Pos++], null };
    }
    // bufio.Reader.UnreadByte: step the cursor back so the next ReadByte returns the last byte.
    public static object? Reader_UnreadByte(object br)
    {
        var b = AsReader(br);
        if (b.Pos == 0) return ErrInvalidUnreadByteSentinel;
        b.Pos--;
        return null;
    }
    // bufio.Reader.Peek(n) ([]byte, error): return the next n bytes without consuming them.
    public static object?[] Reader_Peek(object br, long n)
    {
        var b = AsReader(br);
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
        var b = AsReader(br);
        int want = (int)n;
        var e = Fill(b, want);
        int got = System.Math.Min(want, Avail(b));
        b.Pos += got;
        return new object?[] { (long)got, got < want ? (e ?? Io.EOFSentinel) : null };
    }
    public static void Reader_Reset(object br, object? r) { var b = AsReader(br); b.R = r; b.Buf.Clear(); b.Pos = 0; b.LastRuneSize = -1; }
    public static long Reader_Buffered(object br) => Avail(AsReader(br));

    // bufio.Reader.Size() int: the logical buffer size.
    public static long Reader_Size(object br) => (AsReader(br)).Size;

    // bufio.Reader.ReadRune() (r rune, size int, err error): decode one UTF-8 rune.
    public static object?[] Reader_ReadRune(object br)
    {
        var b = AsReader(br);
        b.LastRuneSize = -1;
        var e = Fill(b, 1);
        if (Avail(b) == 0) return new object?[] { 0, 0L, e ?? Io.EOFSentinel };
        // Ensure up to a full 4-byte sequence is buffered (best effort at EOF).
        Fill(b, 4);
        int avail = Avail(b);
        int take = avail < 4 ? avail : 4;
        var data = new object?[take];
        for (int i = 0; i < take; i++) data[i] = (int)b.Buf[b.Pos + i];
        var rs = Utf8.DecodeRune(new GoSlice { Data = data, Off = 0, Len = take, Cap = take });
        int size = (int)System.Convert.ToInt64(rs[1] ?? 0L);
        if (size == 0) size = 1; // empty handled above; guard
        b.Pos += size;
        b.LastRuneSize = size;
        return new object?[] { (int)System.Convert.ToInt64(rs[0] ?? 0L), (long)size, null };
    }

    // bufio.Reader.UnreadRune() error: undo the rune from the most recent ReadRune.
    public static object? Reader_UnreadRune(object br)
    {
        var b = AsReader(br);
        if (b.LastRuneSize < 0) return ErrInvalidUnreadRuneSentinel;
        b.Pos -= b.LastRuneSize;
        b.LastRuneSize = -1;
        return null;
    }

    // bufio.Reader.ReadSlice(delim) ([]byte, error): read up to and including delim. With an
    // unbounded backing buffer ErrBufferFull never triggers; EOF before delim yields io.EOF.
    public static object?[] Reader_ReadSlice(object br, int delim)
    {
        var b = AsReader(br);
        b.LastRuneSize = -1;
        var (bytes, err) = ReadUntil(b, (byte)delim);
        var data = new object?[bytes.Length];
        for (int i = 0; i < bytes.Length; i++) data[i] = (int)bytes[i];
        return new object?[] { new GoSlice { Data = data, Off = 0, Len = bytes.Length, Cap = bytes.Length }, err };
    }

    // bufio.Reader.ReadLine() (line []byte, isPrefix bool, err error): a low-level line read.
    // Strips a trailing \n and a preceding \r. isPrefix is always false (unbounded buffer).
    public static object?[] Reader_ReadLine(object br)
    {
        var b = AsReader(br);
        b.LastRuneSize = -1;
        var (bytes, err) = ReadUntil(b, (byte)'\n');
        int n = bytes.Length;
        if (err != null && n == 0) return new object?[] { EmptyByteSlice(), false, err };
        // Go's ReadLine drops a trailing \n (and a \r before it); a bare line at EOF keeps its bytes.
        if (n > 0 && bytes[n - 1] == (byte)'\n')
        {
            n--;
            if (n > 0 && bytes[n - 1] == (byte)'\r') n--;
            err = null; // a full line was read
        }
        var data = new object?[n];
        for (int i = 0; i < n; i++) data[i] = (int)bytes[i];
        return new object?[] { new GoSlice { Data = data, Off = 0, Len = n, Cap = n }, false, err };
    }

    // bufio.Reader.WriteTo(w) (n int64, err error): drain everything to w.
    public static object?[] Reader_WriteTo(object br, object? w)
    {
        var b = AsReader(br);
        long total = 0;
        // Flush whatever is buffered, then keep reading the underlying reader to exhaustion.
        while (true)
        {
            int avail = Avail(b);
            if (avail > 0)
            {
                var chunk = new byte[avail];
                for (int i = 0; i < avail; i++) chunk[i] = (byte)b.Buf[b.Pos + i];
                b.Pos += avail;
                Compress.WriteRaw(w, chunk);
                total += avail;
            }
            var e = Fill(b, 1);
            if (Avail(b) == 0)
            {
                if (e != null && !ReferenceEquals(e, Io.EOFSentinel)) return new object?[] { total, e };
                return new object?[] { total, null };
            }
        }
    }

    private static GoSlice EmptyByteSlice() => new() { Data = System.Array.Empty<object?>(), Off = 0, Len = 0, Cap = 0 };

    // bufio.Reader.ReadString(delim) (string, error) / ReadBytes(delim) ([]byte, error):
    // read up to and including the first delim. On EOF before delim, returns the data
    // read so far with io.EOF (matching Go).
    public static object?[] Reader_ReadString(object br, int delim)
    {
        var (bytes, err) = ReadUntil(AsReader(br), (byte)delim);
        return new object?[] { GoString.FromBytesOwned(bytes), err };
    }
    public static object?[] Reader_ReadBytes(object br, int delim)
    {
        var (bytes, err) = ReadUntil(AsReader(br), (byte)delim);
        var data = new object?[bytes.Length];
        for (int i = 0; i < bytes.Length; i++) data[i] = (int)bytes[i];
        return new object?[] { new GoSlice { Data = data, Off = 0, Len = bytes.Length, Cap = bytes.Length }, err };
    }
    private static (byte[], object?) ReadUntil(GoBufReader b, byte delim)
    {
        var acc = new System.Collections.Generic.List<byte>();
        while (true)
        {
            if (Avail(b) == 0) { var e = Fill(b, 1); if (Avail(b) == 0) return (acc.ToArray(), e ?? Io.EOFSentinel); }
            byte c = (byte)b.Buf[b.Pos++];
            acc.Add(c);
            if (c == delim) return (acc.ToArray(), null);
        }
    }

    // A bufio.ReadWriter promotes the embedded *Reader / *Writer methods, so a Reader_*/
    // Writer_* shim may receive the ReadWriter as its receiver — unwrap to the embedded part.
    private static GoBufWriter AsWriter(object o) => o is GoBufReadWriter rw ? (GoBufWriter)rw.W : (GoBufWriter)o;
    private static GoBufReader AsReader(object o) => o is GoBufReadWriter rw ? (GoBufReader)rw.R : (GoBufReader)o;

    // bufio.ReadWriter (a Reader+Writer pair; h2c prior-knowledge path, dead under goclr).
    public static object RW_Reader(object rw) => ((GoBufReadWriter)rw).R;
    public static object RW_Writer(object rw) => ((GoBufReadWriter)rw).W;
    public static object? RW_Flush(object rw) => Writer_Flush(((GoBufReadWriter)rw).W);
    public static object?[] RW_Read(object rw, GoSlice p) => Reader_Read(((GoBufReadWriter)rw).R, p);

    // bufio.NewWriter / NewWriterSize.
    public static object NewWriter(object? w) => new GoBufWriter { W = w };
    public static object NewWriterSize(object? w, long size) => new GoBufWriter { W = w, Size = size > 0 ? (int)size : 4096 };

    public static long Writer_Available(object bw) { var b = AsWriter(bw); return b.Size - b.Buf.Count; }
    public static long Writer_Buffered(object bw) => (AsWriter(bw)).Buf.Count;
    public static object?[] Writer_Write(object bw, GoSlice p)
    {
        var b = AsWriter(bw);
        for (int i = 0; i < p.Len; i++) b.Buf.Add((byte)System.Convert.ToInt64(p.Data![p.Off + i]));
        return new object?[] { (long)p.Len, null };
    }
    public static object? Writer_WriteByte(object bw, int c) { (AsWriter(bw)).Buf.Add((byte)c); return null; }
    public static object?[] Writer_WriteString(object bw, GoString s)
    {
        var b = AsWriter(bw);
        var by = s.Bytes;
        b.Buf.AddRange(by);
        return new object?[] { (long)by.Length, null };
    }
    public static object? Writer_Flush(object bw)
    {
        var b = AsWriter(bw);
        if (b.Buf.Count > 0) { Compress.WriteRaw(b.W, b.Buf.ToArray()); b.Buf.Clear(); }
        return null;
    }
    public static void Writer_Reset(object bw, object? w) { var b = AsWriter(bw); b.W = w; b.Buf.Clear(); }

    /// <summary>Flush w to its sink if (and only if) it is a bufio.Writer; a no-op for a
    /// plain sink. Used by shims (net/textproto) that wrap a *bufio.Writer and, like Go,
    /// must flush it after writing so buffered bytes actually reach the underlying writer.</summary>
    public static void FlushIfBuffered(object? w) { if (w is GoBufWriter) Writer_Flush(w); }

    // bufio.Writer.Size() int: the buffer size.
    public static long Writer_Size(object bw) => (AsWriter(bw)).Size;

    // bufio.Writer.WriteRune(r) (size int, err error): encode r as UTF-8 and buffer it.
    public static object?[] Writer_WriteRune(object bw, int r)
    {
        var b = AsWriter(bw);
        var rune = new System.Text.Rune(r >= 0 && (r < 0xD800 || (r > 0xDFFF && r <= 0x10FFFF)) ? r : 0xFFFD);
        System.Span<byte> tmp = stackalloc byte[4];
        int n = rune.EncodeToUtf8(tmp);
        for (int i = 0; i < n; i++) b.Buf.Add(tmp[i]);
        return new object?[] { (long)n, null };
    }

    // bufio.Writer.AvailableBuffer() []byte: an empty slice with spare capacity (Go returns
    // b.buf[b.n:][:0]). Callers append to it then pass it to Write; an empty slice suffices.
    public static GoSlice Writer_AvailableBuffer(object bw)
    {
        int cap = (int)Writer_Available(bw);
        return new GoSlice { Data = new object?[cap], Off = 0, Len = 0, Cap = cap };
    }

    // bufio.Writer.ReadFrom(r) (n int64, err error): copy everything from r into the buffer.
    public static object?[] Writer_ReadFrom(object bw, object? r)
    {
        var b = AsWriter(bw);
        var bytes = Readers.Drain(r);
        b.Buf.AddRange(bytes);
        return new object?[] { (long)bytes.Length, null };
    }

    // bufio.NewReadWriter(r, w) *ReadWriter.
    public static object NewReadWriter(object? r, object? w) => new GoBufReadWriter { R = r!, W = w! };

    // Scanner.Split(bufio.ScanLines|ScanWords|ScanRunes|ScanBytes): the SplitFunc value
    // lowers to a closure returning its mode marker (a long); invoke it to set the mode.
    public static void Scanner_Split(object s, GoClosure split)
    {
        var sc = (GoScanner)s;
        // A built-in split (ScanLines/Words/Runes/Bytes) lowers to a 0-arg marker closure
        // returning its mode; a user SplitFunc takes (data, atEOF) and indexes both, so
        // invoking it with no args throws. Use that to tell them apart.
        try
        {
            var r = GoRuntime.Invoke(split);
            if (r is long lng) { sc.Mode = (int)lng; sc.Split = null; return; }
            if (r is int it) { sc.Mode = it; sc.Split = null; return; }
        }
        catch (System.IndexOutOfRangeException) { /* a custom SplitFunc: needs (data, atEOF) */ }
        sc.Split = split;
        sc.Mode = -1;
    }
    // Scanner.Buffer(buf, max): the scanner reads a fully-drained in-memory buffer, so the
    // initial buf is irrelevant, but `max` caps the token size — a token longer than max
    // fails Scan() with ErrTooLong, as in Go.
    public static void Scanner_Buffer(object s, GoSlice buf, long max) { ((GoScanner)s).Max = max; }
    public static long ScanLinesMarker() => 0;
    public static long ScanWordsMarker() => 1;
    public static long ScanRunesMarker() => 2;
    public static long ScanBytesMarker() => 3;

    public static bool Scanner_Scan(object so)
    {
        var s = (GoScanner)so;
        if (s.Err != null) return false;
        bool ok = ScanInner(s);
        // Go's ErrTooLong: a token that fills the buffer to its max without room for the
        // delimiter/lookahead — empirically a token length >= max (a token of exactly max
        // already cannot fit alongside the byte that ends it).
        if (ok && s.Cur.Length >= s.Max) { s.Err = ErrTooLong(); s.Cur = System.Array.Empty<byte>(); return false; }
        return ok;
    }

    private static bool ScanInner(GoScanner s)
    {
        if (s.Mode == -1) return ScanCustom(s); // user SplitFunc
        if (s.Mode == 1) // words: skip leading spaces, take until space
        {
            while (s.Pos < s.Data.Length && IsSpace(s.Data[s.Pos])) s.Pos++;
            if (s.Pos >= s.Data.Length) return false;
            int start = s.Pos;
            while (s.Pos < s.Data.Length && !IsSpace(s.Data[s.Pos])) s.Pos++;
            s.Cur = Sub(s.Data, start, s.Pos);
            return true;
        }
        if (s.Mode == 3) // bytes: one byte per token
        {
            if (s.Pos >= s.Data.Length) return false;
            s.Cur = Sub(s.Data, s.Pos, s.Pos + 1);
            s.Pos++;
            return true;
        }
        if (s.Mode == 2) // runes: one UTF-8 rune per token
        {
            if (s.Pos >= s.Data.Length) return false;
            int rs = s.Pos;
            byte b0 = s.Data[s.Pos];
            int n = b0 < 0x80 ? 1 : b0 >= 0xF0 ? 4 : b0 >= 0xE0 ? 3 : b0 >= 0xC0 ? 2 : 1;
            s.Pos = System.Math.Min(s.Data.Length, s.Pos + n);
            s.Cur = Sub(s.Data, rs, s.Pos);
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
    // The in-memory scanner only errors on ErrTooLong (a token exceeding the max buffer).
    public static object? Scanner_Err(object so) => ((GoScanner)so).Err;
    public static GoSlice Scanner_Bytes(object so)
    {
        var c = ((GoScanner)so).Cur;
        var d = new object?[c.Length];
        for (int i = 0; i < c.Length; i++) d[i] = (int)c[i];
        return new GoSlice { Data = d, Off = 0, Len = c.Length, Cap = c.Length };
    }

    private static bool IsSpace(byte b) => b == ' ' || b == '\t' || b == '\n' || b == '\r' || b == '\f' || b == '\v';
    private static byte[] Sub(byte[] b, int from, int to) { var r = new byte[to - from]; System.Array.Copy(b, from, r, 0, to - from); return r; }

    // Runs Go's bufio.Scanner protocol for a user SplitFunc: all input is already buffered,
    // so atEOF is always true; call split(data[Pos:], true) -> (advance, token, err), advance
    // the cursor, and yield the token. A nil token with advance>0 means "skip and continue";
    // advance==0 with no token ends the scan.
    private static bool ScanCustom(GoScanner s)
    {
        while (true)
        {
            int n = s.Data.Length - s.Pos;
            var d = new object?[n];
            for (int i = 0; i < n; i++) d[i] = (int)s.Data[s.Pos + i];
            var data = new GoSlice { Data = d, Off = 0, Len = n, Cap = n };
            if (GoRuntime.InvokeArgs(s.Split!, data, true) is not object?[] res || res.Length == 0) return false;
            long advance = System.Convert.ToInt64(res[0] ?? 0L);
            object? token = res.Length > 1 ? res[1] : null;
            object? err = res.Length > 2 ? res[2] : null;
            if (advance < 0 || advance > n) return false; // SplitFunc protocol violation
            s.Pos += (int)advance;
            if (token is GoSlice ts && ts.Data != null)
            {
                // A non-nil token that does not advance the input (always at EOF here) is
                // only allowed a bounded number of times, like Go, else a buggy SplitFunc
                // would loop forever.
                if (advance > 0) s.Empties = 0;
                else if (++s.Empties > 100)
                    throw new GoPanicException(GoString.FromDotNetString("bufio.Scan: too many empty tokens without progressing"));
                s.Cur = SliceBytes(ts);
                return true;
            }
            if (err != null) return false;
            if (advance == 0) return false; // no token and no progress at EOF: done
        }
    }

    private static byte[] SliceBytes(GoSlice s)
    {
        var r = new byte[s.Len];
        for (int i = 0; i < s.Len; i++) r[i] = (byte)System.Convert.ToInt64(s.Data![s.Off + i]);
        return r;
    }
}
