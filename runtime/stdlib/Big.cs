namespace GoCLR.Stdlib;

using System.Numerics;
using GoCLR.Runtime;

/// <summary>A *big.Int handle (mutable, like Go's receiver-as-destination).</summary>
public sealed class GoBigInt { public BigInteger V; }

/// <summary>A *big.Float handle. Backed by double (best-effort): exact for the JS
/// Number range goja converts through it (integers up to 2^53), lossy beyond.</summary>
public sealed class GoBigFloat { public double V; }

/// <summary>A *big.Rat handle: an exact rational Num/Den (Den &gt; 0, always reduced).</summary>
public sealed class GoBigRat { public BigInteger Num; public BigInteger Den = 1; }

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
    // big.Float.String() == Text('g', 10). Backed by double, so for the common
    // big.NewFloat(float64) case this is byte-exact with Go (which stores the exact float64).
    public static GoString Float_String(object z) => Float_Text(z, 'g', 10);
    // Text(format, prec) maps directly onto strconv.FormatFloat: 'e/E/f/g/G' honor prec
    // (prec<0 = shortest). The big.Float-specific 'b'/'p'/'x' mantissa forms aren't modeled
    // by the double backing — fall back to the shortest decimal for those.
    public static GoString Float_Text(object z, int fmt, long prec)
    {
        char c = (char)fmt;
        if (c is 'e' or 'E' or 'f' or 'g' or 'G')
            return Strconv.FormatFloat(F(z), fmt, prec, 64);
        return GoString.FromDotNetString(GoCLR.Runtime.GoFtoa.Shortest(F(z)));
    }
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
    // Arithmetic + setters on the double backing (returning the receiver, like Go).
    public static object Float_Add(object z, object x, object y) { ((GoBigFloat)z).V = F(x) + F(y); return z; }
    public static object Float_Mul(object z, object x, object y) { ((GoBigFloat)z).V = F(x) * F(y); return z; }
    public static object Float_Quo(object z, object x, object y) { ((GoBigFloat)z).V = F(x) / F(y); return z; }
    public static object Float_Neg(object z, object x) { ((GoBigFloat)z).V = -F(x); return z; }
    public static object Float_Abs(object z, object x) { ((GoBigFloat)z).V = System.Math.Abs(F(x)); return z; }
    public static object Float_Set(object z, object x) { ((GoBigFloat)z).V = F(x); return z; }
    public static object Float_Copy(object z, object x) { ((GoBigFloat)z).V = F(x); return z; }
    public static object Float_SetFloat64(object z, double x) { ((GoBigFloat)z).V = x; return z; }
    public static object Float_SetInt64(object z, long x) { ((GoBigFloat)z).V = x; return z; }
    public static object Float_SetUint64(object z, ulong x) { ((GoBigFloat)z).V = x; return z; }
    public static object?[] Float_Float64(object z) => new object?[] { F(z), 0L }; // (float64, Accuracy=Exact)
    public static bool Float_IsInf(object z) => double.IsInfinity(F(z));
    // Precision/mode are not modeled beyond the double backing (53-bit mantissa): SetPrec/
    // SetMode are accepted (return the receiver) and Prec/MinPrec report 53. A computation
    // needing more than float64 precision is a documented gap.
    public static object Float_SetPrec(object z, ulong prec) => z;
    public static object Float_SetMode(object z, long mode) => z;
    public static ulong Float_Prec(object z) => 53;
    public static ulong Float_MinPrec(object z) => 53;
    public static long Float_Mode(object z) => 0;  // ToNearestEven
    public static long Float_Acc(object z) => 0;   // Exact

    // --- big.Rat (exact rational, two BigIntegers) ---
    private static GoBigRat Norm(GoBigRat r)
    {
        if (r.Den.Sign < 0) { r.Num = -r.Num; r.Den = -r.Den; }
        if (r.Num.IsZero) { r.Den = 1; return r; }
        var g = BigInteger.GreatestCommonDivisor(BigInteger.Abs(r.Num), r.Den);
        if (g > BigInteger.One) { r.Num /= g; r.Den /= g; }
        return r;
    }
    private static GoBigRat R(object o) => (GoBigRat)o;
    public static object NewRat(long a, long b)
    {
        if (b == 0) throw new GoPanicException(GoString.FromDotNetString("division by zero"));
        return Norm(new GoBigRat { Num = a, Den = b });
    }
    public static object RatZero() => new GoBigRat { Num = 0, Den = 1 };
    public static object Rat_Add(object z, object x, object y) { var a = R(x); var b = R(y); var r = R(z); r.Num = a.Num * b.Den + b.Num * a.Den; r.Den = a.Den * b.Den; return Norm(r); }
    public static object Rat_Sub(object z, object x, object y) { var a = R(x); var b = R(y); var r = R(z); r.Num = a.Num * b.Den - b.Num * a.Den; r.Den = a.Den * b.Den; return Norm(r); }
    public static object Rat_Mul(object z, object x, object y) { var a = R(x); var b = R(y); var r = R(z); r.Num = a.Num * b.Num; r.Den = a.Den * b.Den; return Norm(r); }
    public static object Rat_Quo(object z, object x, object y) { var a = R(x); var b = R(y); var r = R(z); r.Num = a.Num * b.Den; r.Den = a.Den * b.Num; return Norm(r); }
    public static object Rat_Neg(object z, object x) { var a = R(x); var r = R(z); r.Num = -a.Num; r.Den = a.Den; return r; }
    public static object Rat_Inv(object z, object x) { var a = R(x); var r = R(z); var n = a.Num; r.Num = a.Den; r.Den = n; return Norm(r); }
    public static object Rat_Abs(object z, object x) { var a = R(x); var r = R(z); r.Num = BigInteger.Abs(a.Num); r.Den = a.Den; return r; }
    public static object Rat_Set(object z, object x) { var a = R(x); var r = R(z); r.Num = a.Num; r.Den = a.Den; return r; }
    public static GoString Rat_String(object x) { var r = R(x); return GoString.FromDotNetString(r.Num + "/" + r.Den); }
    public static GoString Rat_RatString(object x) { var r = R(x); return GoString.FromDotNetString(r.Den.IsOne ? r.Num.ToString() : r.Num + "/" + r.Den); }
    public static object Rat_Num(object x) => new GoBigInt { V = R(x).Num };
    public static object Rat_Denom(object x) => new GoBigInt { V = R(x).Den };
    public static long Rat_Sign(object x) => R(x).Num.Sign;
    public static bool Rat_IsInt(object x) => R(x).Den.IsOne;
    public static long Rat_Cmp(object x, object y) { var a = R(x); var b = R(y); return (a.Num * b.Den).CompareTo(b.Num * a.Den); }
    public static object Rat_SetFrac64(object z, long a, long b)
    {
        if (b == 0) throw new GoPanicException(GoString.FromDotNetString("division by zero"));
        var r = R(z); r.Num = a; r.Den = b; return Norm(r);
    }
    public static object Rat_SetInt64(object z, long a) { var r = R(z); r.Num = a; r.Den = 1; return r; }
    public static object Rat_SetInt(object z, object i) { var r = R(z); r.Num = V(i); r.Den = 1; return r; }
    public static object Rat_SetFrac(object z, object a, object b)
    {
        var r = R(z); r.Num = V(a); r.Den = V(b); return Norm(r);
    }
    // FloatString(prec): decimal with prec digits, rounded half away from zero.
    public static GoString Rat_FloatString(object x, long prec)
    {
        var r = R(x);
        bool neg = r.Num.Sign < 0;
        BigInteger n = BigInteger.Abs(r.Num), d = r.Den;
        BigInteger p10 = BigInteger.Pow(10, (int)prec);
        BigInteger scaled = n * p10, q = scaled / d, rem = scaled % d;
        if (2 * rem >= d) q++;                                  // round half away from zero
        string digits = q.ToString();
        var sb = new System.Text.StringBuilder();
        if (neg && !q.IsZero) sb.Append('-');
        if (prec == 0) { sb.Append(digits); return GoString.FromDotNetString(sb.ToString()); }
        if (digits.Length <= (int)prec) digits = new string('0', (int)prec + 1 - digits.Length) + digits;
        sb.Append(digits.Substring(0, digits.Length - (int)prec)).Append('.').Append(digits.Substring(digits.Length - (int)prec));
        return GoString.FromDotNetString(sb.ToString());
    }

    public static object?[] Rat_MarshalText(object x) => new object?[] { BytesOfStr(Rat_RatString(x).ToDotNetString()), null };
    public static object?[] Rat_AppendText(object x, GoSlice b)
    {
        var extra = System.Text.Encoding.ASCII.GetBytes(Rat_RatString(x).ToDotNetString());
        var d = new object?[b.Len + extra.Length];
        for (int i = 0; i < b.Len; i++) d[i] = b.Data![b.Off + i];
        for (int i = 0; i < extra.Length; i++) d[b.Len + i] = (int)extra[i];
        return new object?[] { new GoSlice { Data = d, Off = 0, Len = d.Length, Cap = d.Length }, null };
    }
    public static object? Rat_UnmarshalText(object z, GoSlice text)
    {
        var rr = Rat_SetString(z, GoString.FromDotNetString(StrOfBytes(text)));
        return rr[1] is bool ok && !ok ? new GoError(GoString.FromDotNetString("math/big: cannot unmarshal into a *big.Rat")) : null;
    }

    private static byte[] BigEndianMag(BigInteger v)
    {
        v = BigInteger.Abs(v);
        if (v.IsZero) return System.Array.Empty<byte>();
        var le = v.ToByteArray();
        int len = le.Length;
        while (len > 1 && le[len - 1] == 0) len--;
        var be = new byte[len];
        for (int i = 0; i < len; i++) be[i] = le[len - 1 - i];
        return be;
    }
    // Gob: [version<<1|sign] [uint32 BE len(numBytes)] [num big-endian] [den big-endian].
    public static object?[] Rat_GobEncode(object x)
    {
        var r = R(x);
        byte[] nb = BigEndianMag(r.Num), db = BigEndianMag(r.Den);
        int header = (1 << 1) | (r.Num.Sign < 0 ? 1 : 0);
        var d = new System.Collections.Generic.List<object?> { header,
            (nb.Length >> 24) & 0xff, (nb.Length >> 16) & 0xff, (nb.Length >> 8) & 0xff, nb.Length & 0xff };
        foreach (var b in nb) d.Add((int)b);
        foreach (var b in db) d.Add((int)b);
        return new object?[] { new GoSlice { Data = d.ToArray(), Off = 0, Len = d.Count, Cap = d.Count }, null };
    }
    public static object? Rat_GobDecode(object z, GoSlice buf)
    {
        var r = R(z);
        if (buf.Len == 0) { r.Num = 0; r.Den = 1; return null; }
        int header = (int)System.Convert.ToInt64(buf.Data![buf.Off]);
        bool neg = (header & 1) != 0;
        int nl = ((int)System.Convert.ToInt64(buf.Data![buf.Off + 1]) << 24) | ((int)System.Convert.ToInt64(buf.Data![buf.Off + 2]) << 16)
               | ((int)System.Convert.ToInt64(buf.Data![buf.Off + 3]) << 8) | (int)System.Convert.ToInt64(buf.Data![buf.Off + 4]);
        BigInteger num = 0, den = 0;
        for (int i = 0; i < nl; i++) num = (num << 8) | (byte)System.Convert.ToInt64(buf.Data![buf.Off + 5 + i]);
        for (int i = 5 + nl; i < buf.Len; i++) den = (den << 8) | (byte)System.Convert.ToInt64(buf.Data![buf.Off + i]);
        r.Num = neg ? -num : num; r.Den = den.IsZero ? 1 : den;
        return null;
    }

    private static (BigInteger num, BigInteger den) DoubleToRational(double f)
    {
        long bits = System.BitConverter.DoubleToInt64Bits(f);
        bool neg = bits < 0;
        int exp = (int)((bits >> 52) & 0x7FF);
        long mant = bits & 0xFFFFFFFFFFFFF;
        if (exp == 0) exp = 1; else mant |= 0x10000000000000;
        exp -= 1075;
        BigInteger num = mant, den = 1;
        if (exp >= 0) num <<= exp; else den <<= -exp;
        return (neg ? -num : num, den);
    }
    public static object? Rat_SetFloat64(object z, double f)
    {
        if (double.IsNaN(f) || double.IsInfinity(f)) return null;
        var r = R(z);
        if (f == 0) { r.Num = 0; r.Den = 1; return r; }
        var (num, den) = DoubleToRational(f);
        r.Num = num; r.Den = den; return Norm(r);
    }
    public static object Rat_SetUint64(object z, ulong a) { var r = R(z); r.Num = a; r.Den = 1; return r; }
    public static object?[] Rat_Float64(object x)
    {
        var r = R(x);
        double d = (double)r.Num / (double)r.Den;
        var (dn, dd) = DoubleToRational(d);
        bool exact = double.IsFinite(d) && dn * r.Den == r.Num * dd;
        return new object?[] { d, exact };
    }

    public static object?[] Rat_SetString(object z, GoString s)
    {
        string str = s.ToDotNetString().Trim();
        var r = R(z);
        try
        {
            int slash = str.IndexOf('/');
            if (slash >= 0) { r.Num = BigInteger.Parse(str.Substring(0, slash)); r.Den = BigInteger.Parse(str.Substring(slash + 1)); }
            else { r.Num = BigInteger.Parse(str); r.Den = 1; }
            if (r.Den.IsZero) return new object?[] { null, false };
            Norm(r);
            return new object?[] { r, true };
        }
        catch { return new object?[] { null, false }; }
    }

    // Sign + lowercase magnitude digits of a *big.Int in base b (2..36), so fmt's integer
    // verbs (%d/%b/%o/%x/%X) can format an arbitrary-precision value beyond long's range.
    public static (bool neg, string digits) IntFmtParts(object z, int b)
    {
        var v = V(z);
        bool neg = v.Sign < 0;
        var m = BigInteger.Abs(v);
        if (b == 10) return (neg, m.ToString());
        if (m.IsZero) return (neg, "0");
        const string digs = "0123456789abcdefghijklmnopqrstuvwxyz";
        var sb = new System.Text.StringBuilder();
        while (m > 0) { sb.Insert(0, digs[(int)(m % b)]); m /= b; }
        return (neg, sb.ToString());
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

    // (z *Int).ModSqrt(x, p): a square root of x mod the odd prime p, or nil if x is a
    // non-residue. Ported from big/int.go: the 3-mod-4 and 5-mod-8 fast paths and the
    // general Tonelli-Shanks loop — all exact integer arithmetic, so byte-exact.
    public static object? Int_ModSqrt(object z, object xo, object po)
    {
        var Z = (GoBigInt)z;
        BigInteger p = V(po);
        int jac = JacobiBI(V(xo), p);
        if (jac == -1) return null;          // x is not a square mod p
        if (jac == 0) { Z.V = 0; return z; } // sqrt(0) = 0
        BigInteger x = ((V(xo) % p) + p) % p;
        BigInteger r;
        if (p % 4 == 3) r = BigInteger.ModPow(x, (p + 1) / 4, p);
        else if (p % 8 == 5) r = ModSqrt5Mod8(x, p);
        else r = ModSqrtTonelliShanks(x, p);
        Z.V = r;
        return z;
    }
    private static BigInteger PosMod(BigInteger a, BigInteger p) { a %= p; return a.Sign < 0 ? a + p : a; }
    private static BigInteger ModSqrt5Mod8(BigInteger x, BigInteger p)
    {
        BigInteger e = p >> 3;                          // (p-5)/8
        BigInteger tx = x << 1;                         // 2x
        BigInteger alpha = BigInteger.ModPow(tx, e, p);
        BigInteger beta = PosMod(alpha * alpha, p);
        beta = PosMod(beta * tx, p);
        beta = PosMod(beta - 1, p);
        beta = PosMod(beta * x, p);
        return PosMod(beta * alpha, p);
    }
    private static BigInteger ModSqrtTonelliShanks(BigInteger x, BigInteger p)
    {
        BigInteger s = p - 1;
        int e = 0;
        while (s.IsEven) { s >>= 1; e++; }
        BigInteger n = 2;
        while (JacobiBI(n, p) != -1) n += 1;            // least non-residue
        BigInteger y = BigInteger.ModPow(x, (s + 1) / 2, p);
        BigInteger b = BigInteger.ModPow(x, s, p);
        BigInteger g = BigInteger.ModPow(n, s, p);
        int r = e;
        while (true)
        {
            BigInteger t = b;
            int m = 0;
            while (t != 1) { t = PosMod(t * t, p); m++; }
            if (m == 0) return y;
            t = BigInteger.ModPow(g, BigInteger.One << (r - m - 1), p);
            g = PosMod(t * t, p);
            y = PosMod(y * t, p);
            b = PosMod(b * g, p);
            r = m;
        }
    }
    // Jacobi symbol (a/n) for odd n > 0.
    private static int JacobiBI(BigInteger a, BigInteger n)
    {
        a = PosMod(a, n);
        int result = 1;
        while (a != 0)
        {
            while (a.IsEven) { a >>= 1; var r8 = n % 8; if (r8 == 3 || r8 == 5) result = -result; }
            (a, n) = (n, a);
            if (a % 4 == 3 && n % 4 == 3) result = -result;
            a %= n;
        }
        return n == 1 ? result : 0;
    }

    // (z *Int).Rand(rnd, n): a uniform random Int in [0,n), consuming rnd exactly as Go's
    // nat.random does (per 64-bit word: two rand.Uint32 draws low|high<<32; mask the top
    // word; reject+redraw the whole value if >= n). Byte-exact since the PRNG matches.
    public static object Int_Rand(object z, object rnd, object no)
    {
        var Z = (GoBigInt)z;
        BigInteger n = V(no);
        if (n.Sign <= 0) { Z.V = 0; return z; }
        var r = (GoRand)rnd;
        int nbits = (int)n.GetBitLength();
        int words = (nbits + 63) / 64;
        int msw = nbits % 64; if (msw == 0) msw = 64;
        ulong mask = msw == 64 ? ulong.MaxValue : (1UL << msw) - 1;
        while (true)
        {
            BigInteger val = 0;
            for (int i = 0; i < words; i++)
            {
                ulong w = (ulong)r.Uint32() | ((ulong)r.Uint32() << 32);
                if (i == words - 1) w &= mask;
                val |= (BigInteger)w << (64 * i);
            }
            if (val < n) { Z.V = val; return z; }
        }
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

    // Int.Bits() []Word: the absolute value as little-endian 64-bit limbs (Word = uintptr,
    // 64-bit here); normalized so the zero value yields an empty slice.
    public static GoSlice Int_Bits(object x)
    {
        var m = BigInteger.Abs(V(x));
        var limbs = new System.Collections.Generic.List<object?>();
        var mask = (BigInteger)ulong.MaxValue;
        while (m > 0) { limbs.Add((ulong)(m & mask)); m >>= 64; }
        var d = limbs.ToArray();
        return new GoSlice { Data = d, Off = 0, Len = d.Length, Cap = d.Length };
    }
    // Int.SetBits(abs []Word): rebuild the magnitude from little-endian 64-bit limbs; the
    // result is non-negative (Go clears the sign).
    public static object Int_SetBits(object z, GoSlice abs)
    {
        BigInteger v = 0;
        for (int i = abs.Len - 1; i >= 0; i--)
            v = (v << 64) | (BigInteger)System.Convert.ToUInt64(abs.Data![abs.Off + i]);
        ((GoBigInt)z).V = v;
        return z;
    }

    // (big.Accuracy).String() and (big.RoundingMode).String(): stringer for the enum
    // (the receiver is the underlying int8 / uint8, dispatched like time.Duration).
    public static GoString Accuracy_String(int i) => GoString.FromDotNetString(i switch
    {
        -1 => "Below", 0 => "Exact", 1 => "Above", _ => "Accuracy(" + i + ")",
    });
    public static GoString RoundingMode_String(int i) => GoString.FromDotNetString(i switch
    {
        0 => "ToNearestEven", 1 => "ToNearestAway", 2 => "ToZero",
        3 => "AwayFromZero", 4 => "ToNegativeInf", 5 => "ToPositiveInf",
        _ => "RoundingMode(" + i + ")",
    });

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
    // Gob format: [version<<1 | sign] followed by the big-endian magnitude (empty for 0).
    public static object?[] Int_GobEncode(object x)
    {
        var v = V(x);
        int header = (1 << 1) | (v.Sign < 0 ? 1 : 0);
        var mag = Int_Bytes(x);
        var d = new object?[1 + mag.Len];
        d[0] = header;
        for (int i = 0; i < mag.Len; i++) d[1 + i] = mag.Data![mag.Off + i];
        return new object?[] { new GoSlice { Data = d, Off = 0, Len = d.Length, Cap = d.Length }, null };
    }
    public static object? Int_GobDecode(object z, GoSlice buf)
    {
        if (buf.Len == 0) { ((GoBigInt)z).V = 0; return null; }
        int header = (int)System.Convert.ToInt64(buf.Data![buf.Off]);
        bool neg = (header & 1) != 0;
        BigInteger mag = 0;
        for (int i = 1; i < buf.Len; i++) mag = (mag << 8) | (byte)System.Convert.ToInt64(buf.Data![buf.Off + i]);
        ((GoBigInt)z).V = neg ? -mag : mag;
        return null;
    }
    public static object?[] Int_AppendText(object x, GoSlice b) => new object?[] { Int_Append(x, b, 10), null };

    // big.Jacobi(x, y) int — the Jacobi symbol (x/y); y must be odd and > 0.
    public static long Jacobi(object xo, object yo)
    {
        BigInteger a = V(xo), n = V(yo);
        a %= n; if (a.Sign < 0) a += n;
        int result = 1;
        while (!a.IsZero)
        {
            while (a.IsEven)
            {
                a >>= 1;
                int r8 = (int)(n % 8);
                if (r8 == 3 || r8 == 5) result = -result;
            }
            (a, n) = (n, a);
            if ((int)(a % 4) == 3 && (int)(n % 4) == 3) result = -result;
            a %= n;
        }
        return n == BigInteger.One ? result : 0;
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
