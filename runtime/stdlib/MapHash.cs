namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>A hash/maphash.Hash. goja uses it only as a fast, internally-consistent
/// 64-bit hasher for map keys; FNV-1a backs it (the exact maphash algorithm and its
/// random seed are not observable to JS).</summary>
public sealed class GoMapHash { public ulong H = 14695981039346656037UL; }

public static class MapHash
{
    private const ulong Prime = 1099511628211UL;
    private const ulong Basis = 14695981039346656037UL;

    public static object New() => new GoMapHash();

    public static object? WriteByte(object h, int b)
    {
        var g = (GoMapHash)h;
        g.H = (g.H ^ (byte)b) * Prime;
        return null; // error
    }
    public static object?[] Write(object h, GoSlice p)
    {
        var g = (GoMapHash)h;
        for (int i = 0; i < p.Len; i++)
            g.H = (g.H ^ (byte)(System.Convert.ToInt64(p.Data![p.Off + i]) & 0xff)) * Prime;
        return new object?[] { (long)p.Len, null };
    }
    public static object?[] WriteString(object h, GoString s)
    {
        var by = s.Bytes;
        var g = (GoMapHash)h;
        foreach (var b in by) g.H = (g.H ^ b) * Prime;
        return new object?[] { (long)by.Length, null };
    }
    public static ulong Sum64(object h) => ((GoMapHash)h).H;
    public static void Reset(object h) => ((GoMapHash)h).H = Basis;
    public static long Size(object h) => 8;
    public static long BlockSize(object h) => 8;
}
