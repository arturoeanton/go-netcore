namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>A hash.Hash32/Hash64 handle (FNV / CRC32 / CRC64 / Adler32).</summary>
public sealed class GoHash32 { public uint H; public string Algo = ""; public GoPtr? Tab; }
public sealed class GoHash64 { public ulong H; public string Algo = ""; public GoCLR.Runtime.GoPtr? Tab; }

/// <summary>Shims for hash/fnv, hash/crc32, hash/crc64, hash/adler32.</summary>
public static class Hashes
{
    private static byte[] B(GoSlice s) { var b = new byte[s.Len]; for (int i = 0; i < s.Len; i++) b[i] = (byte)System.Convert.ToInt64(s.Data![s.Off + i]); return b; }

    // ---- FNV ----
    public static object Crc32NewIEEE() => new GoHash32 { H = 0, Algo = "crc32" };
    // crc32.New(tab *Table): a running CRC-32 digest over the given polynomial table.
    public static object Crc32New(GoPtr tab) => new GoHash32 { H = 0, Algo = "crc32", Tab = tab };
    // adler32.New(): a running digest. H packs (b<<16)|a; the initial value is 1 (a=1, b=0).
    public static object Adler32New() => new GoHash32 { H = 1, Algo = "adler32" };
    public static object Fnv32() => new GoHash32 { H = 2166136261u, Algo = "fnv32" };
    public static object Fnv32a() => new GoHash32 { H = 2166136261u, Algo = "fnv32a" };
    public static object Fnv64() => new GoHash64 { H = 14695981039346656037ul, Algo = "fnv64" };
    public static object Fnv64a() => new GoHash64 { H = 14695981039346656037ul, Algo = "fnv64a" };
    // fnv.New128/New128a return a 128-bit hash.Hash; reuse the GoHash buffer plumbing
    // (Crypto.Hash_Write/Hash_Sum), with the FNV-128 digest computed in Crypto.Digest.
    public static object Fnv128() => new GoHash { Algo = "fnv128", Size = 16, Block = 1 };
    public static object Fnv128a() => new GoHash { Algo = "fnv128a", Size = 16, Block = 1 };

    public static object?[] H32_Write(object ho, GoSlice p)
    {
        var h = (GoHash32)ho;
        if (h.Algo == "crc32") { h.H = Crc32Update(h.H, h.Tab!, p); return new object?[] { (long)p.Len, null }; }
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
        if (h.Algo == "crc64") { h.H = Crc64Update(h.H, h.Tab!, p); return new object?[] { (long)p.Len, null }; }
        foreach (byte b in B(p))
        {
            if (h.Algo == "fnv64a") { h.H ^= b; h.H *= 1099511628211ul; }
            else { h.H *= 1099511628211ul; h.H ^= b; }
        }
        return new object?[] { (long)p.Len, null };
    }
    public static ulong H64_Sum64(object ho) => ((GoHash64)ho).H;
    public static long H64_Size(object ho) => 8;
    public static void H64_Reset(object ho) => ((GoHash64)ho).H = ((GoHash64)ho).Algo == "crc64" ? 0ul : 14695981039346656037ul;

    // ---- CRC64 (hash/crc64) — mirrors CRC32 with 64-bit reversed polynomials ----
    private static ulong[] BuildCrc64(ulong poly)
    {
        var t = new ulong[256];
        for (uint i = 0; i < 256; i++)
        {
            ulong c = i;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? poly ^ (c >> 1) : c >> 1;
            t[i] = c;
        }
        return t;
    }
    private static ulong[] Table64Of(GoCLR.Runtime.GoPtr? tab) => tab?.Value as ulong[] ?? BuildCrc64(0xC96C5795D7870F42ul);
    // crc64.MakeTable(poly *Table): the poly constants are ISO=0xD800000000000000, ECMA=0xC96C5795D7870F42.
    public static GoPtr Crc64MakeTable(ulong poly) => new() { Value = BuildCrc64(poly) };
    public static object Crc64New(GoPtr tab) => new GoHash64 { H = 0, Algo = "crc64", Tab = tab };
    public static ulong Crc64Checksum(GoSlice data, GoCLR.Runtime.GoPtr tab) => Crc64Update(0, tab, data);
    public static ulong Crc64Update(ulong crc, GoCLR.Runtime.GoPtr tab, GoSlice p)
    {
        var t = Table64Of(tab);
        crc = ~crc;
        foreach (byte b in B(p)) crc = t[(byte)(crc ^ b)] ^ (crc >> 8);
        return ~crc;
    }

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
    public static object Crc32IEEETable() => new GoPtr { Value = CrcTable }; // a var accessor: object
    public static GoPtr Crc32MakeTable(uint poly) => new() { Value = BuildCrc(poly) };

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
