namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>A hash.Hash32/Hash64 handle (FNV / CRC32 / CRC64 / Adler32).</summary>
public sealed class GoHash32 { public uint H; public string Algo = ""; }
public sealed class GoHash64 { public ulong H; public string Algo = ""; }

/// <summary>Shims for hash/fnv, hash/crc32, hash/crc64, hash/adler32.</summary>
public static class Hashes
{
    private static byte[] B(GoSlice s) { var b = new byte[s.Len]; for (int i = 0; i < s.Len; i++) b[i] = (byte)System.Convert.ToInt64(s.Data![s.Off + i]); return b; }

    // ---- FNV ----
    public static object Crc32NewIEEE() => new GoHash32 { H = 0, Algo = "crc32" };
    // adler32.New(): a running digest. H packs (b<<16)|a; the initial value is 1 (a=1, b=0).
    public static object Adler32New() => new GoHash32 { H = 1, Algo = "adler32" };
    public static object Fnv32() => new GoHash32 { H = 2166136261u, Algo = "fnv32" };
    public static object Fnv32a() => new GoHash32 { H = 2166136261u, Algo = "fnv32a" };
    public static object Fnv64() => new GoHash64 { H = 14695981039346656037ul, Algo = "fnv64" };
    public static object Fnv64a() => new GoHash64 { H = 14695981039346656037ul, Algo = "fnv64a" };

    public static object?[] H32_Write(object ho, GoSlice p)
    {
        var h = (GoHash32)ho;
        if (h.Algo == "crc32") { h.H = Crc32Update(h.H, null, p); return new object?[] { (long)p.Len, null }; }
        if (h.Algo == "adler32")
        {
            uint a = h.H & 0xffff, b = h.H >> 16;
            foreach (byte by in B(p)) { a = (a + by) % 65521; b = (b + a) % 65521; }
            h.H = (b << 16) | a;
            return new object?[] { (long)p.Len, null };
        }
        foreach (byte b in B(p))
        {
            if (h.Algo == "fnv32a") { h.H ^= b; h.H *= 16777619u; }
            else { h.H *= 16777619u; h.H ^= b; }
        }
        return new object?[] { (long)p.Len, null };
    }
    public static uint H32_Sum32(object ho) => ((GoHash32)ho).H;
    public static long H32_Size(object ho) => 4;
    public static void H32_Reset(object ho) => ((GoHash32)ho).H = ((GoHash32)ho).Algo == "crc32" ? 0u : 2166136261u;

    public static object?[] H64_Write(object ho, GoSlice p)
    {
        var h = (GoHash64)ho;
        foreach (byte b in B(p))
        {
            if (h.Algo == "fnv64a") { h.H ^= b; h.H *= 1099511628211ul; }
            else { h.H *= 1099511628211ul; h.H ^= b; }
        }
        return new object?[] { (long)p.Len, null };
    }
    public static ulong H64_Sum64(object ho) => ((GoHash64)ho).H;
    public static long H64_Size(object ho) => 8;
    public static void H64_Reset(object ho) => ((GoHash64)ho).H = 14695981039346656037ul;

    // ---- CRC32 ----
    // Build a 256-entry CRC table for a reversed polynomial (Go's crc32.simpleMakeTable).
    private static uint[] BuildCrc(uint poly)
    {
        var t = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? poly ^ (c >> 1) : c >> 1;
            t[i] = c;
        }
        return t;
    }
    private const uint IEEEPoly = 0xEDB88320u;
    private static readonly uint[] CrcTable = BuildCrc(IEEEPoly);

    // A *crc32.Table handle carries its 256-entry table in the GoPtr cell so Update/Checksum
    // honour the polynomial (fiber builds a custom table via crc32.MakeTable for ETags).
    private static uint[] TableOf(GoCLR.Runtime.GoPtr? tab) => tab?.Value as uint[] ?? CrcTable;
    public static object Crc32IEEETable() => new GoCLR.Runtime.GoPtr { Value = CrcTable };
    public static object Crc32MakeTable(uint poly) => new GoCLR.Runtime.GoPtr { Value = BuildCrc(poly) };

    public static uint Crc32ChecksumIEEE(GoSlice data)
    {
        uint c = 0xFFFFFFFFu;
        foreach (byte b in B(data)) c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }
    // crc32.Checksum(data, tab): CRC of data using tab's polynomial.
    public static uint Crc32Checksum(GoSlice data, GoCLR.Runtime.GoPtr tab)
    {
        var t = TableOf(tab);
        uint c = 0xFFFFFFFFu;
        foreach (byte b in B(data)) c = t[(c ^ b) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }
    // crc32.Update(crc, tab, p): fold p into the running CRC of crc using tab's polynomial.
    public static uint Crc32Update(uint crc, GoCLR.Runtime.GoPtr tab, GoSlice p)
    {
        var t = TableOf(tab);
        crc = ~crc;
        foreach (byte b in B(p)) crc = t[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return ~crc;
    }

    // ---- Adler32 ----
    public static uint Adler32Checksum(GoSlice data)
    {
        uint a = 1, b = 0;
        foreach (byte by in B(data)) { a = (a + by) % 65521; b = (b + a) % 65521; }
        return (b << 16) | a;
    }
}
