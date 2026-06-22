namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>Shim for a subset of Go's <c>io</c> package.</summary>
public static class Io
{
    /// <summary>The single io.EOF sentinel, so `err == io.EOF` works everywhere.</summary>
    public static readonly GoError EOFSentinel = new(GoString.FromDotNetString("EOF"));
    public static object EOF() => EOFSentinel;

    // io.Discard (io.Writer): a sink that swallows writes. A throwaway bytes buffer
    // serves — io.Copy(io.Discard, r) just drains r.
    public static object Discard() => new GoBuffer();

    /// <summary>The single io.ErrUnexpectedEOF sentinel.</summary>
    public static readonly GoError ErrUnexpectedEOFSentinel = new(GoString.FromDotNetString("unexpected EOF"));
    public static object ErrUnexpectedEOF() => ErrUnexpectedEOFSentinel;

    public static readonly GoError ErrShortWriteSentinel = new(GoString.FromDotNetString("short write"));
    public static object ErrShortWrite() => ErrShortWriteSentinel;
    public static readonly GoError ErrShortBufferSentinel = new(GoString.FromDotNetString("short buffer"));
    public static object ErrShortBuffer() => ErrShortBufferSentinel;
    public static readonly GoError ErrClosedPipeSentinel = new(GoString.FromDotNetString("io: read/write on closed pipe"));
    public static object ErrClosedPipe() => ErrClosedPipeSentinel;
    public static readonly GoError ErrNoProgressSentinel = new(GoString.FromDotNetString("multiple Read calls return no data or error"));
    public static object ErrNoProgress() => ErrNoProgressSentinel;

    // io.ReadFull(r, buf) (n int, err error): read exactly len(buf) bytes into buf.
    // Returns io.EOF if nothing was read, io.ErrUnexpectedEOF on a short read.
    // io.NopCloser(r): the reader unchanged (Close becomes a no-op via Body_Close).
    public static object? NopCloser(object? r) => r;

    public static object?[] ReadFull(object? r, GoSlice buf)
    {
        int want = buf.Len, got;
        switch (r)
        {
            case GoReader gr:
                { int avail = gr.Data.Length - gr.Pos; got = System.Math.Min(want, avail);
                  for (int i = 0; i < got; i++) buf.Data![buf.Off + i] = (int)gr.Data[gr.Pos + i]; gr.Pos += got; break; }
            case GoBuffer gb:
                { int avail = gb.B.Count - gb.Pos; got = System.Math.Min(want, avail);
                  for (int i = 0; i < got; i++) buf.Data![buf.Off + i] = (int)gb.B[gb.Pos + i]; gb.Pos += got; break; }
            case not null when Bridge.HasMethod(r, "Read"):
                { // A user io.Reader: drive its own Read through the callback bridge, filling
                  // buf in place (the chunk GoSlice aliases buf's backing array).
                  got = 0;
                  while (got < want)
                  {
                      var chunk = new GoSlice { Data = buf.Data, Off = buf.Off + got, Len = want - got, Cap = want - got };
                      var res = Bridge.CallMethod(r, "Read", new object?[] { chunk }) as object?[];
                      int n = res != null && res.Length > 0 && res[0] != null ? (int)System.Convert.ToInt64(res[0]) : 0;
                      object? rerr = res != null && res.Length > 1 ? res[1] : null;
                      got += n;
                      if (rerr != null || n == 0) break;
                  }
                  break; }
            default:
                { var data = Readers.Drain(r); got = System.Math.Min(want, data.Length);
                  for (int i = 0; i < got; i++) buf.Data![buf.Off + i] = (int)data[i]; break; }
        }
        object? err = got < want ? (got == 0 ? EOFSentinel : ErrUnexpectedEOFSentinel) : null;
        return new object?[] { (long)got, err };
    }

    public static object?[] WriteString(object? w, GoString s)
    {
        long n = Fmt.WriteTo(w, s.ToDotNetString());
        return new object?[] { n, null };
    }
}
