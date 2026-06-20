namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>Shim for a subset of Go's <c>io</c> package.</summary>
public static class Io
{
    public static object?[] WriteString(object? w, GoString s)
    {
        long n = Fmt.WriteTo(w, s.ToDotNetString());
        return new object?[] { n, null };
    }
}
