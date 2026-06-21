namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>Shim for a subset of Go's <c>io</c> package.</summary>
public static class Io
{
    /// <summary>The single io.EOF sentinel, so `err == io.EOF` works everywhere.</summary>
    public static readonly GoError EOFSentinel = new(GoString.FromDotNetString("EOF"));
    public static object EOF() => EOFSentinel;

    /// <summary>The single io.ErrUnexpectedEOF sentinel.</summary>
    public static readonly GoError ErrUnexpectedEOFSentinel = new(GoString.FromDotNetString("unexpected EOF"));
    public static object ErrUnexpectedEOF() => ErrUnexpectedEOFSentinel;

    // io.ReadFull(r, buf) (n int, err error): read exactly len(buf) bytes into buf.
    // Returns io.EOF if nothing was read, io.ErrUnexpectedEOF on a short read.
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
