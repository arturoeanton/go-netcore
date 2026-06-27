namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>math/rand/v2.PCG: the 128-bit PCG-DXSM generator (Go's pcg.go), deterministic
/// given its seed.</summary>
[GoShim("math/rand/v2.PCG")]
public sealed class GoPCG { public ulong Hi, Lo; }

/// <summary>math/rand/v2.Rand: wraps a Source (a *PCG, or any value with a Uint64 method)
/// and derives bounded/typed draws byte-identically to Go's v2 rand.go.</summary>
[GoShim("math/rand/v2.Rand")]
public sealed class GoRandV2 { public object? Src; }

/// <summary>Deterministic byte-exact slice of <c>math/rand/v2</c>: the PCG generator and a
/// Rand wrapping a Source. (The package-level funcs in Rand2.cs use an auto-seeded global
/// and are not byte-reproducible — only the explicitly-seeded generators are.)</summary>
public static partial class Rand2
{
    // ---- PCG generator (pcg.go) ----
    public static object NewPCG(ulong seed1, ulong seed2) => new GoPCG { Hi = seed1, Lo = seed2 };
    public static void PCG_Seed(object po, ulong seed1, ulong seed2) { var p = (GoPCG)po; p.Hi = seed1; p.Lo = seed2; }

    private static (ulong hi, ulong lo) PcgNext(GoPCG p)
    {
        const ulong mulHi = 2549297995355413924UL, mulLo = 4865540595714422341UL;
        const ulong incHi = 6364136223846793005UL, incLo = 1442695040888963407UL;
        // state = state*mul + inc, 128-bit.
        ulong hi = System.Math.BigMul(p.Lo, mulLo, out ulong lo); // bits.Mul64(p.lo, mulLo)
        hi += p.Hi * mulLo + p.Lo * mulHi;
        ulong nlo = unchecked(lo + incLo);
        ulong c = nlo < lo ? 1UL : 0UL;
        ulong nhi = unchecked(hi + incHi + c);
        p.Lo = nlo; p.Hi = nhi;
        return (nhi, nlo);
    }
    public static ulong PCG_Uint64(object po)
    {
        var p = (GoPCG)po;
        var (hi, lo) = PcgNext(p);
        const ulong cheapMul = 0xda942042e4dd58b5UL; // DXSM
        hi ^= hi >> 32;
        hi = unchecked(hi * cheapMul);
        hi ^= hi >> 48;
        hi = unchecked(hi * (lo | 1));
        return hi;
    }

    // PCG.MarshalBinary/AppendBinary/UnmarshalBinary: "pcg:" + BE(hi) + BE(lo).
    private static byte[] PcgBytes(GoPCG p)
    {
        var b = new byte[20];
        b[0] = (byte)'p'; b[1] = (byte)'c'; b[2] = (byte)'g'; b[3] = (byte)':';
        PutBE(b, 4, p.Hi); PutBE(b, 12, p.Lo);
        return b;
    }
    private static void PutBE(byte[] b, int o, ulong v) { for (int i = 0; i < 8; i++) b[o + i] = (byte)(v >> (56 - 8 * i)); }
    private static ulong GetBE(byte[] b, int o) { ulong v = 0; for (int i = 0; i < 8; i++) v = (v << 8) | b[o + i]; return v; }
    private static GoSlice Bytes(byte[] b) { var d = new object?[b.Length]; for (int i = 0; i < b.Length; i++) d[i] = (int)b[i]; return new GoSlice { Data = d, Off = 0, Len = b.Length, Cap = b.Length }; }
    private static byte[] Raw(GoSlice s) { var b = new byte[s.Len]; for (int i = 0; i < s.Len; i++) b[i] = (byte)System.Convert.ToInt64(s.Data![s.Off + i]); return b; }

    public static object?[] PCG_MarshalBinary(object po) => new object?[] { Bytes(PcgBytes((GoPCG)po)), null };
    public static object?[] PCG_AppendBinary(object po, GoSlice dst)
    {
        var head = Raw(dst); var tail = PcgBytes((GoPCG)po);
        var all = new byte[head.Length + tail.Length];
        head.CopyTo(all, 0); tail.CopyTo(all, head.Length);
        return new object?[] { Bytes(all), null };
    }
    public static object? PCG_UnmarshalBinary(object po, GoSlice data)
    {
        var b = Raw(data);
        if (b.Length != 20 || b[0] != 'p' || b[1] != 'c' || b[2] != 'g' || b[3] != ':')
            return new GoError(GoString.FromDotNetString("invalid PCG encoding"));
        var p = (GoPCG)po; p.Hi = GetBE(b, 4); p.Lo = GetBE(b, 12);
        return null;
    }

    // ---- rand/v2.New(src) + the source-driven Rand methods (rand.go) ----
    public static object NewV2(object? src) => new GoRandV2 { Src = src };
    private static ulong SrcUint64(object? src) => src switch
    {
        GoPCG p => PCG_Uint64(p),
        GoChaCha8 c => ChaCha8_Uint64(c),
        _ => src != null && Bridge.HasMethod(src, "Uint64")
            ? System.Convert.ToUInt64(Bridge.CallMethod(src, "Uint64"))
            : 0UL,
    };
    private static ulong Su(object r) => SrcUint64(((GoRandV2)r).Src);

    public static ulong RandV2_Uint64(object r) => Su(r);
    public static long RandV2_Int64(object r) => (long)(Su(r) & ~(1UL << 63));
    public static uint RandV2_Uint32(object r) => (uint)(Su(r) >> 32);
    public static int RandV2_Int32(object r) => (int)(Su(r) >> 33);
    public static long RandV2_Int(object r) => (long)((Su(r) << 1) >> 1);
    public static ulong RandV2_Uint(object r) => Su(r);
    public static double RandV2_Float64(object r) => (double)((Su(r) << 11) >> 11) / (double)(1UL << 53);
    public static float RandV2_Float32(object r) { uint u = (uint)(Su(r) >> 32); return (float)((u << 8) >> 8) / (float)(1 << 24); }

    // uint64n (64-bit target: is32bit==false, so the uint32n branch is never taken).
    private static ulong Uint64n(object r, ulong n)
    {
        if ((n & (n - 1)) == 0) return Su(r) & (n - 1);
        ulong hi = System.Math.BigMul(Su(r), n, out ulong lo);
        if (lo < n)
        {
            ulong thresh = unchecked(0UL - n) % n;
            while (lo < thresh) hi = System.Math.BigMul(Su(r), n, out lo);
        }
        return hi;
    }
    // uint32n (always the Mul32 path, matching Go's exact 32-bit output sequence).
    private static uint Uint32n(object r, uint n)
    {
        if ((n & (n - 1)) == 0) return (uint)Su(r) & (n - 1);
        ulong x = Su(r);
        ulong p0 = (ulong)(uint)x * n; uint lo0 = (uint)p0, lo1a = (uint)(p0 >> 32);
        ulong p1 = (ulong)(uint)(x >> 32) * n; uint lo1b = (uint)p1, hi = (uint)(p1 >> 32);
        ulong s = (ulong)lo1a + lo1b; uint lo1 = (uint)s, c = (uint)(s >> 32);
        hi += c;
        if (lo1 == 0 && lo0 < n)
        {
            ulong n64 = n; uint thresh = (uint)(unchecked(0UL - n64) % n64);
            while (lo1 == 0 && lo0 < thresh)
            {
                x = Su(r);
                p0 = (ulong)(uint)x * n; lo0 = (uint)p0; lo1a = (uint)(p0 >> 32);
                p1 = (ulong)(uint)(x >> 32) * n; lo1b = (uint)p1; hi = (uint)(p1 >> 32);
                s = (ulong)lo1a + lo1b; lo1 = (uint)s; c = (uint)(s >> 32);
                hi += c;
            }
        }
        return hi;
    }

    // v2 ziggurat (normal.go/exp.go): same tables as v1 (reuse GoRand.*) but draws ONE
    // Uint64 per iteration, splitting it into the value (low 32) and index (high bits).
    private const double RnV2 = 3.442619855899, ReV2 = 7.69711747013104972;
    private static uint AbsI32(int i) => i < 0 ? (uint)(-i) : (uint)i;
    private static double F64(object r) => (double)((Su(r) << 11) >> 11) / (double)(1UL << 53);
    public static double RandV2_NormFloat64(object r)
    {
        while (true)
        {
            ulong u = Su(r);
            int j = (int)u;
            int i = (int)((u >> 32) & 0x7F);
            double x = (double)j * (double)GoRand.Wn[i];
            if (AbsI32(j) < GoRand.Kn[i]) return x;
            if (i == 0)
            {
                double y;
                do { x = -System.Math.Log(F64(r)) * (1.0 / RnV2); y = -System.Math.Log(F64(r)); } while (y + y < x * x);
                return j > 0 ? RnV2 + x : -RnV2 - x;
            }
            if (GoRand.Fn[i] + (float)F64(r) * (GoRand.Fn[i - 1] - GoRand.Fn[i]) < (float)System.Math.Exp(-.5 * x * x)) return x;
        }
    }
    public static double RandV2_ExpFloat64(object r)
    {
        while (true)
        {
            ulong u = Su(r);
            uint j = (uint)u;
            int i = (int)(byte)(u >> 32);
            double x = (double)j * (double)GoRand.We[i];
            if (j < GoRand.Ke[i]) return x;
            if (i == 0) return ReV2 - System.Math.Log(F64(r));
            if (GoRand.Fe[i] + (float)F64(r) * (GoRand.Fe[i - 1] - GoRand.Fe[i]) < (float)System.Math.Exp(-x)) return x;
        }
    }

    // v2 Shuffle/Perm: single-phase Fisher-Yates consuming uint64n(i+1) per step.
    public static void RandV2_Shuffle(object r, long n, GoClosure swap)
    {
        if (n < 0) throw Panic("invalid argument to Shuffle");
        for (long i = n - 1; i > 0; i--) GoRuntime.InvokeArgs(swap, i, (long)Uint64n(r, (ulong)(i + 1)));
    }
    public static GoSlice RandV2_Perm(object r, long n)
    {
        var p = new object?[n];
        for (long i = 0; i < n; i++) p[i] = i;
        for (long i = n - 1; i > 0; i--) { long j = (long)Uint64n(r, (ulong)(i + 1)); var t = p[i]; p[i] = p[j]; p[j] = t; }
        return new GoSlice { Data = p, Off = 0, Len = (int)n, Cap = (int)n };
    }

    private static GoPanicException Panic(string m) => new(GoString.FromDotNetString(m));
    public static ulong RandV2_Uint64N(object r, ulong n) { if (n == 0) throw Panic("invalid argument to Uint64N"); return Uint64n(r, n); }
    public static long RandV2_Int64N(object r, long n) { if (n <= 0) throw Panic("invalid argument to Int64N"); return (long)Uint64n(r, (ulong)n); }
    public static uint RandV2_Uint32N(object r, uint n) { if (n == 0) throw Panic("invalid argument to Uint32N"); return Uint32n(r, n); }
    public static int RandV2_Int32N(object r, int n) { if (n <= 0) throw Panic("invalid argument to Int32N"); return (int)Uint32n(r, (uint)n); }
    public static long RandV2_IntN(object r, long n) { if (n <= 0) throw Panic("invalid argument to IntN"); return (long)Uint64n(r, (ulong)n); }
    public static ulong RandV2_UintN(object r, ulong n) { if (n == 0) throw Panic("invalid argument to UintN"); return Uint64n(r, n); }
}
