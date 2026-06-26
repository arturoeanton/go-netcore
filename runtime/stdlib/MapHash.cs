namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>A hash/maphash.Hash. goja uses it only as a fast, internally-consistent
/// 64-bit hasher for map keys; FNV-1a (seeded) backs it. Go's maphash uses a random
/// seed, so the absolute Sum64 is not observable/byte-exact across runs — only the
/// relative properties (same seed+bytes -> same hash) hold, which this reproduces.</summary>
public sealed class GoMapHash { public ulong H = 14695981039346656037UL; public ulong Seed = 14695981039346656037UL; }

/// <summary>A hash/maphash.Seed (opaque): a 64-bit seed value.</summary>
[GoShim("hash/maphash.Seed")]
public sealed class GoMapSeed { public ulong S; }

public static class MapHash
{
    private const ulong Prime = 1099511628211UL;
    private const ulong Basis = 14695981039346656037UL;

    public static object New() => new GoMapHash();

    // maphash.MakeSeed() Seed — a fresh, non-zero seed (random like Go; not reproducible).
    public static object MakeSeed()
    {
        ulong s = ((ulong)System.Random.Shared.NextInt64() << 1) | 1; // ensure non-zero
        return new GoMapSeed { S = s };
    }

    // (*Hash).SetSeed(seed) / (*Hash).Seed() Seed — the seed becomes the FNV start state,
    // so SetSeed+WriteString matches String(seed, …) one-shot.
    public static void SetSeed(object h, object seed) { var g = (GoMapHash)h; g.Seed = ((GoMapSeed)seed).S; g.H = g.Seed; }
    public static object Seed(object h) => new GoMapSeed { S = ((GoMapHash)h).Seed };

    // maphash.String(seed, s) / maphash.Bytes(seed, b) uint64 — one-shot seeded FNV-1a.
    public static ulong StringHash(object seed, GoString s)
    {
        ulong h = ((GoMapSeed)seed).S;
        foreach (var b in s.Bytes) h = (h ^ b) * Prime;
        return h;
    }
    public static ulong BytesHash(object seed, GoSlice p)
    {
        ulong h = ((GoMapSeed)seed).S;
        for (int i = 0; i < p.Len; i++) h = (h ^ (byte)(System.Convert.ToInt64(p.Data![p.Off + i]) & 0xff)) * Prime;
        return h;
    }

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
    public static void Reset(object h) { var g = (GoMapHash)h; g.H = g.Seed; }
    public static long Size(object h) => 8;
    public static long BlockSize(object h) => 128; // Go's maphash buffer size
}
