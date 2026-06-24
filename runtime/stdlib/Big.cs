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

    // ---- additional pure Int methods (byte-exact via BigInteger) ----
    public static ulong Int_Bit(object x, long i) => (ulong)((V(x) >> (int)i) & 1);
    public static ulong Int_TrailingZeroBits(object x)
    {
        var v = BigInteger.Abs(V(x));
        if (v.IsZero) return 0;
        ulong n = 0;
        while ((v & 1) == 0) { v >>= 1; n++; }
        return n;
    }
    public static object Int_AndNot(object z, object x, object y) { ((GoBigInt)z).V = V(x) & ~V(y); return z; }
    public static object Int_SetBit(object z, object x, long i, ulong b)
    {
        BigInteger mask = BigInteger.One << (int)i;
        ((GoBigInt)z).V = b != 0 ? (V(x) | mask) : (V(x) & ~mask);
        return z;
    }
    private static BigInteger MulRangeBI(long a, long b)
    {
        if (a > b) return BigInteger.One;
        if (a <= 0 && b >= 0) return BigInteger.Zero;
        BigInteger r = BigInteger.One;
        for (long i = a; i <= b; i++) r *= i;
        return r;
    }
    public static object Int_MulRange(object z, long a, long b) { ((GoBigInt)z).V = MulRangeBI(a, b); return z; }
    public static object Int_Binomial(object z, long n, long k)
    {
        if (k > n) { ((GoBigInt)z).V = 0; return z; }
        if (k > n - k) k = n - k;
        ((GoBigInt)z).V = MulRangeBI(n - k + 1, n) / MulRangeBI(1, k);
        return z;
    }
    public static object?[] Int_Float64(object x)
    {
        var v = V(x);
        double d = (double)v;
        int acc;                                              // Accuracy is int8 -> int
        if (double.IsInfinity(d)) acc = d > 0 ? 1 : -1;       // Above / Below on overflow
        else { int c = new BigInteger(d).CompareTo(v); acc = c > 0 ? 1 : (c < 0 ? -1 : 0); }
        return new object?[] { d, acc };
    }
    public static object Int_ModInverse(object z, object g, object n)
    {
        BigInteger m = BigInteger.Abs(V(n)), a = ((V(g) % m) + m) % m;
        BigInteger old_r = a, r = m, old_s = 1, s = 0;
        while (r != 0) { var q = old_r / r; (old_r, r) = (r, old_r - q * r); (old_s, s) = (s, old_s - q * s); }
        BigInteger res = old_r == 1 ? ((old_s % m) + m) % m : BigInteger.Zero;
        ((GoBigInt)z).V = res;
        return z;
    }

    private static GoSlice BytesOfStr(string s)
    {
        var b = System.Text.Encoding.ASCII.GetBytes(s);
        var d = new object?[b.Length];
        for (int i = 0; i < b.Length; i++) d[i] = (int)b[i];
        return new GoSlice { Data = d, Off = 0, Len = b.Length, Cap = b.Length };
    }
    private static string StrOfBytes(GoSlice b)
    {
        var by = new byte[b.Len];
        for (int i = 0; i < b.Len; i++) by[i] = (byte)System.Convert.ToInt64(b.Data![b.Off + i]);
        return System.Text.Encoding.UTF8.GetString(by);
    }
    public static object?[] Int_MarshalText(object x) => new object?[] { BytesOfStr(V(x).ToString()), null };
    public static object?[] Int_MarshalJSON(object x) => new object?[] { BytesOfStr(V(x).ToString()), null };
    public static object? Int_UnmarshalText(object z, GoSlice text)
    {
        string s = StrOfBytes(text).Trim();
        try { ((GoBigInt)z).V = BigInteger.Parse(s); return null; }
        catch { return new GoError(GoString.FromDotNetString("math/big: cannot unmarshal \"" + s + "\" into a *big.Int")); }
    }
    public static object? Int_UnmarshalJSON(object z, GoSlice text)
    {
        string s = StrOfBytes(text).Trim().Trim('"');
        if (s == "null") return null;
        try { ((GoBigInt)z).V = BigInteger.Parse(s); return null; }
        catch { return new GoError(GoString.FromDotNetString("math/big: cannot unmarshal \"" + s + "\" into a *big.Int")); }
    }
    public static GoSlice Int_Append(object x, GoSlice buf, long base_)
    {
        var extra = System.Text.Encoding.ASCII.GetBytes(Int_Text(x, base_).ToDotNetString());
        var d = new object?[buf.Len + extra.Length];
        for (int i = 0; i < buf.Len; i++) d[i] = buf.Data![buf.Off + i];
        for (int i = 0; i < extra.Length; i++) d[buf.Len + i] = (int)extra[i];
        return new GoSlice { Data = d, Off = 0, Len = d.Length, Cap = d.Length };
    }
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
