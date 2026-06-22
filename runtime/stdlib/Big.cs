namespace GoCLR.Stdlib;

using System.Numerics;
using GoCLR.Runtime;

/// <summary>A *big.Int handle (mutable, like Go's receiver-as-destination).</summary>
public sealed class GoBigInt { public BigInteger V; }

/// <summary>A *big.Float handle. Backed by double (best-effort): exact for the JS
/// Number range goja converts through it (integers up to 2^53), lossy beyond.</summary>
public sealed class GoBigFloat { public double V; }

/// <summary>Shim for a subset of Go's <c>math/big</c> (Int). Methods return the
/// receiver (the destination), matching Go's chaining.</summary>
public static class Big
{
    // --- big.Float (double-backed) ---
    private static double F(object o) => ((GoBigFloat)o).V;
    public static object NewFloat(double x) => new GoBigFloat { V = x };
    public static object FloatZero() => new GoBigFloat { V = 0 };
    public static object Float_SetInt(object z, object x) { ((GoBigFloat)z).V = (double)V(x); return z; }
    public static object Float_Sub(object z, object x, object y) { ((GoBigFloat)z).V = F(x) - F(y); return z; }
    public static long Float_Cmp(object z, object y) => F(z).CompareTo(F(y));
    public static long Float_Sign(object z) => System.Math.Sign(F(z));
    public static bool Float_IsInt(object z) { double v = F(z); return !double.IsInfinity(v) && !double.IsNaN(v) && v == System.Math.Truncate(v); }
    public static GoString Float_String(object z) => GoString.FromDotNetString(GoCLR.Runtime.GoFtoa.Shortest(F(z)));
    public static GoString Float_Text(object z, int fmt, long prec) => GoString.FromDotNetString(GoCLR.Runtime.GoFtoa.Shortest(F(z)));
    public static object?[] Float_Int(object z, object? dst)
    {
        var d = dst as GoBigInt ?? new GoBigInt();
        d.V = new BigInteger(System.Math.Truncate(F(z)));
        return new object?[] { d, 0L }; // (*Int, Accuracy=Exact)
    }
    public static object?[] Float_SetString(object z, GoString s)
    {
        if (double.TryParse(s.ToDotNetString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double v))
        { ((GoBigFloat)z).V = v; return new object?[] { z, true }; }
        return new object?[] { null, false };
    }

    public static object NewInt(long x) => new GoBigInt { V = x };
    public static object IntZero() => new GoBigInt { V = 0 };
    private static BigInteger V(object o) => ((GoBigInt)o).V;

    public static object Int_Add(object z, object x, object y) { ((GoBigInt)z).V = V(x) + V(y); return z; }
    public static object Int_Sub(object z, object x, object y) { ((GoBigInt)z).V = V(x) - V(y); return z; }
    public static object Int_Mul(object z, object x, object y) { ((GoBigInt)z).V = V(x) * V(y); return z; }
    public static object Int_Div(object z, object x, object y)
    {
        // Go's big.Int.Div is Euclidean (remainder always non-negative).
        BigInteger q = BigInteger.DivRem(V(x), V(y), out BigInteger r);
        if (r.Sign < 0) q += V(y).Sign > 0 ? -1 : 1;
        ((GoBigInt)z).V = q; return z;
    }
    public static object Int_Quo(object z, object x, object y) { ((GoBigInt)z).V = BigInteger.Divide(V(x), V(y)); return z; }
    public static object Int_Rem(object z, object x, object y) { ((GoBigInt)z).V = BigInteger.Remainder(V(x), V(y)); return z; }
    public static object Int_GCD(object z, object a, object b, object x, object y) { ((GoBigInt)z).V = BigInteger.GreatestCommonDivisor(V(x), V(y)); return z; }
    public static object Int_Mod(object z, object x, object y) { var r = V(x) % V(y); if (r.Sign < 0) r += BigInteger.Abs(V(y)); ((GoBigInt)z).V = r; return z; }
    public static object Int_Neg(object z, object x) { ((GoBigInt)z).V = -V(x); return z; }
    public static object Int_Abs(object z, object x) { ((GoBigInt)z).V = BigInteger.Abs(V(x)); return z; }
    // (z *Int).FillBytes(buf): write |z| big-endian into buf, right-aligned/zero-padded.
    public static GoSlice Int_FillBytes(object z, GoSlice buf)
    {
        for (int i = 0; i < buf.Len; i++) buf.Data![buf.Off + i] = 0;
        var be = BigInteger.Abs(((GoBigInt)z).V).ToByteArray(isUnsigned: true, isBigEndian: true);
        int start = buf.Len - be.Length;
        for (int i = 0; i < be.Length && start + i >= 0; i++) buf.Data![buf.Off + start + i] = (int)be[i];
        return buf;
    }
    public static object Int_Exp(object z, object x, object y, object m)
    {
        BigInteger r = m == null ? BigInteger.Pow(V(x), (int)V(y)) : BigInteger.ModPow(V(x), V(y), V(m));
        ((GoBigInt)z).V = r; return z;
    }
    public static object Int_Set(object z, object x) { ((GoBigInt)z).V = V(x); return z; }
    // QuoRem: truncated division — z = x/y (toward zero), r = x - y*z; returns (z, r).
    public static object?[] Int_QuoRem(object z, object x, object y, object r)
    {
        BigInteger q = BigInteger.DivRem(V(x), V(y), out BigInteger rem);
        ((GoBigInt)z).V = q; ((GoBigInt)r).V = rem;
        return new object?[] { z, r };
    }
    // DivMod sets z to the Euclidean quotient and m to the (non-negative) modulus,
    // returning (z, m).
    public static object?[] Int_DivMod(object z, object x, object y, object m)
    {
        BigInteger q = BigInteger.DivRem(V(x), V(y), out BigInteger r);
        if (r.Sign < 0) { if (V(y).Sign > 0) { q -= 1; r += V(y); } else { q += 1; r -= V(y); } }
        ((GoBigInt)z).V = q; ((GoBigInt)m).V = r;
        return new object?[] { z, m };
    }

    public static object Int_SetInt64(object z, long x) { ((GoBigInt)z).V = x; return z; }
    public static object Int_SetUint64(object z, ulong x) { ((GoBigInt)z).V = x; return z; }
    // Uint64 returns the low 64 bits (Go's behaviour for out-of-range values).
    public static ulong Int_Uint64(object x) => unchecked((ulong)(V(x) & ulong.MaxValue));
    public static object Int_And(object z, object x, object y) { ((GoBigInt)z).V = V(x) & V(y); return z; }
    public static object Int_Or(object z, object x, object y) { ((GoBigInt)z).V = V(x) | V(y); return z; }
    public static object Int_Xor(object z, object x, object y) { ((GoBigInt)z).V = V(x) ^ V(y); return z; }
    public static object Int_Not(object z, object x) { ((GoBigInt)z).V = -(V(x)) - 1; return z; } // two's-complement NOT
    public static long Int_BitLen(object x) { var a = BigInteger.Abs(V(x)); long n = 0; while (a > 0) { a >>= 1; n++; } return n; }
    public static bool Int_IsInt64(object x) => V(x) >= long.MinValue && V(x) <= long.MaxValue;
    public static bool Int_IsUint64(object x) => V(x) >= 0 && V(x) <= ulong.MaxValue;
    public static long Int_CmpAbs(object x, object y) => BigInteger.Abs(V(x)).CompareTo(BigInteger.Abs(V(y)));
    // Sqrt sets z to floor(sqrt(x)) (x must be non-negative), returning z.
    public static object Int_Sqrt(object z, object x)
    {
        BigInteger n = V(x);
        if (n < 0) throw new GoPanicException(GoString.FromDotNetString("square root of negative number"));
        if (n == 0) { ((GoBigInt)z).V = 0; return z; }
        BigInteger lo = 0, hi = n, r = 0;
        while (lo <= hi) { var mid = (lo + hi) >> 1; if (mid * mid <= n) { r = mid; lo = mid + 1; } else hi = mid - 1; }
        ((GoBigInt)z).V = r; return z;
    }
    public static bool Int_ProbablyPrime(object x, long n)
    {
        BigInteger v = V(x);
        if (v < 2) return false;
        if (v < 4) return true;
        if (v % 2 == 0) return false;
        for (BigInteger i = 3; i * i <= v; i += 2) if (v % i == 0) return false;
        return true;
    }
    public static object Int_Lsh(object z, object x, ulong n) { ((GoBigInt)z).V = V(x) << (int)n; return z; }
    public static object Int_Rsh(object z, object x, ulong n) { ((GoBigInt)z).V = V(x) >> (int)n; return z; }

    public static object Int_SetBytes(object z, GoSlice buf)
    {
        int n = buf.Len;
        var le = new byte[n + 1];
        for (int i = 0; i < n; i++) le[i] = (byte)(System.Convert.ToInt64(buf.Data![buf.Off + (n - 1 - i)]) & 0xff);
        le[n] = 0; // force a non-negative magnitude
        ((GoBigInt)z).V = new BigInteger(le);
        return z;
    }
    public static GoSlice Int_Bytes(object x)
    {
        var v = BigInteger.Abs(V(x));
        if (v.IsZero) return new GoSlice { Data = System.Array.Empty<object?>(), Off = 0, Len = 0, Cap = 0 };
        var le = v.ToByteArray(); // little-endian, possibly with a trailing 0 sign byte
        int len = le.Length;
        while (len > 1 && le[len - 1] == 0) len--;
        var d = new object?[len];
        for (int i = 0; i < len; i++) d[i] = (int)le[len - 1 - i]; // big-endian, boxed as byte (int)
        return new GoSlice { Data = d, Off = 0, Len = len, Cap = len };
    }
    public static GoString Int_Text(object x, long bas)
    {
        var v = V(x);
        if (bas == 10) return GoString.FromDotNetString(v.ToString());
        if (v.IsZero) return GoString.FromDotNetString("0");
        bool neg = v.Sign < 0;
        var m = BigInteger.Abs(v);
        const string digits = "0123456789abcdefghijklmnopqrstuvwxyz";
        var sb = new System.Text.StringBuilder();
        while (m > 0) { sb.Insert(0, digits[(int)(m % bas)]); m /= bas; }
        if (neg) sb.Insert(0, '-');
        return GoString.FromDotNetString(sb.ToString());
    }

    public static long Int_Cmp(object z, object y) => V(z).CompareTo(V(y));
    public static long Int_Sign(object z) => V(z).Sign;
    public static long Int_Int64(object z) => (long)V(z);
    public static GoString Int_String(object z) => GoString.FromDotNetString(V(z).ToString());
    public static object?[] Int_SetString(object z, GoString s, long base_)
    {
        try
        {
            string str = s.ToDotNetString();
            BigInteger v = base_ == 16 ? ParseBase(str, 16) : base_ == 2 ? ParseBase(str, 2) : BigInteger.Parse(str);
            ((GoBigInt)z).V = v;
            return new object?[] { z, true };
        }
        catch { return new object?[] { null, false }; }
    }
    private static BigInteger ParseBase(string s, int b)
    {
        bool neg = s.StartsWith("-"); if (neg) s = s.Substring(1);
        BigInteger r = 0;
        foreach (char c in s) { int d = c <= '9' ? c - '0' : char.ToLowerInvariant(c) - 'a' + 10; r = r * b + d; }
        return neg ? -r : r;
    }
}
