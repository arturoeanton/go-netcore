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
    public static object Fnv32() => new GoHash32 { H = 2166136261u, Algo = "fnv32" };
    public static object Fnv32a() => new GoHash32 { H = 2166136261u, Algo = "fnv32a" };
    public static object Fnv64() => new GoHash64 { H = 14695981039346656037ul, Algo = "fnv64" };
    public static object Fnv64a() => new GoHash64 { H = 14695981039346656037ul, Algo = "fnv64a" };

    public static object?[] H32_Write(object ho, GoSlice p)
    {
        var h = (GoHash32)ho;
        foreach (byte b in B(p))
        {
            if (h.Algo == "fnv32a") { h.H ^= b; h.H *= 16777619u; }
            else { h.H *= 16777619u; h.H ^= b; }
        }
        return new object?[] { (long)p.Len, null };
    }
    public static uint H32_Sum32(object ho) => ((GoHash32)ho).H;
    public static long H32_Size(object ho) => 4;
    public static void H32_Reset(object ho) => ((GoHash32)ho).H = 2166136261u;

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

    // ---- CRC32 (IEEE) ----
    private static readonly uint[] CrcTable = BuildCrc();
    private static uint[] BuildCrc()
    {
        var t = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            t[i] = c;
        }
        return t;
    }
    public static uint Crc32ChecksumIEEE(GoSlice data)
    {
        uint c = 0xFFFFFFFFu;
        foreach (byte b in B(data)) c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }

    // ---- Adler32 ----
    public static uint Adler32Checksum(GoSlice data)
    {
        uint a = 1, b = 0;
        foreach (byte by in B(data)) { a = (a + by) % 65521; b = (b + a) % 65521; }
        return (b << 16) | a;
    }
}
