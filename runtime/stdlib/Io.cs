namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>Shim for a subset of Go's <c>io</c> package.</summary>
public static class Io
{
    /// <summary>The single io.EOF sentinel, so `err == io.EOF` works everywhere.</summary>
    public static readonly GoError EOFSentinel = new(GoString.FromDotNetString("EOF"));
    public static object EOF() => EOFSentinel;

    public static object?[] WriteString(object? w, GoString s)
    {
        long n = Fmt.WriteTo(w, s.ToDotNetString());
        return new object?[] { n, null };
    }
}
