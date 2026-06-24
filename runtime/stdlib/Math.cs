namespace GoCLR.Stdlib;

using SM = System.Math;

/// <summary>Shim for Go's <c>math</c> package, mapping to System.Math.</summary>
public static class Math
{
    public static double Abs(double x) => SM.Abs(x);
    public static double Acos(double x) => SM.Acos(x);
    public static double Asin(double x) => SM.Asin(x);
    public static double Atan(double x) => SM.Atan(x);
    public static double Atan2(double y, double x) => SM.Atan2(y, x);
    public static double Cbrt(double x) => SM.Cbrt(x);
    public static double Ceil(double x) => SM.Ceiling(x);
    public static double Copysign(double x, double y) => SM.CopySign(x, y);
    public static double Cos(double x) => SM.Cos(x);
    public static double Cosh(double x) => SM.Cosh(x);
    public static double Acosh(double x) => SM.Acosh(x);
    public static double Asinh(double x) => SM.Asinh(x);
    public static double Atanh(double x) => SM.Atanh(x);
    public static double Exp(double x) => SM.Exp(x);
    public static double Exp2(double x) => SM.Pow(2, x);
    public static double Expm1(double x) => SM.Exp(x) - 1.0;
    public static double Log1p(double x) => SM.Log(1.0 + x);
    public static double Floor(double x) => SM.Floor(x);
    // math.Modf(f) (int, frac): integer and fractional parts, both carrying f's sign.
    public static object?[] Modf(double f) { double i = SM.Truncate(f); return new object?[] { i, f - i }; }
    public static double Hypot(double p, double q) => SM.Sqrt(p * p + q * q);
    public static double Log(double x) => SM.Log(x);
    public static double Log10(double x) => SM.Log10(x);
    public static double Log2(double x) => SM.Log2(x);
    public static double Max(double x, double y) => SM.Max(x, y);
    public static double Min(double x, double y) => SM.Min(x, y);
    public static double Mod(double x, double y) => x % y; // C# % on doubles is fmod, like Go's Mod
    public static double Pow(double x, double y) => SM.Pow(x, y);
    public static double Pow10(long n) => SM.Pow(10, n);
    public static double Remainder(double x, double y) => SM.IEEERemainder(x, y);
    public static double Round(double x) => SM.Round(x, System.MidpointRounding.AwayFromZero);
    public static bool Signbit(double x) => double.IsNegative(x);
    public static double Sin(double x) => SM.Sin(x);
    public static double Sinh(double x) => SM.Sinh(x);
    public static double Sqrt(double x) => SM.Sqrt(x);
    public static double Tan(double x) => SM.Tan(x);
    public static double Tanh(double x) => SM.Tanh(x);
    public static double Trunc(double x) => SM.Truncate(x);
    public static bool IsNaN(double x) => double.IsNaN(x);
    public static bool IsInf(double x, long sign) =>
        (sign >= 0 && double.IsPositiveInfinity(x)) || (sign <= 0 && double.IsNegativeInfinity(x));
    public static double NaN() => double.NaN;
    public static ulong Float64bits(double f) => unchecked((ulong)System.BitConverter.DoubleToInt64Bits(f));
    public static double Float64frombits(ulong b) => System.BitConverter.Int64BitsToDouble(unchecked((long)b));
    public static uint Float32bits(float f) => unchecked((uint)System.BitConverter.SingleToInt32Bits(f));
    public static float Float32frombits(uint b) => System.BitConverter.Int32BitsToSingle(unchecked((int)b));

    public static double Inf(long sign) => sign >= 0 ? double.PositiveInfinity : double.NegativeInfinity;

    // --- IEEE-754 bit-level functions (faithful ports of Go's math source) ---
    private const int    fShift = 52;
    private const ulong  fMask  = 0x7FF;
    private const int    fBias  = 1023;
    private const ulong  fFrac  = (1UL << fShift) - 1;   // fraction mask
    private const ulong  fSign  = 1UL << 63;             // sign mask
    private const ulong  fUvone = 0x3FF0000000000000UL;  // +1.0
    private const double SmallestNormal = 2.2250738585072014e-308; // 2**-1022

    // normalize returns a normal number y and exponent exp satisfying x == y * 2**exp.
    private static (double y, int exp) Normalize(double x)
    {
        if (SM.Abs(x) < SmallestNormal) return (x * (1L << 52), -52);
        return (x, 0);
    }

    public static double Dim(double x, double y)
    {
        double v = x - y;
        if (v <= 0) return 0; // negative, zero, or NaN-subtraction collapses below
        return v;
    }

    public static double FMA(double x, double y, double z) => SM.FusedMultiplyAdd(x, y, z);

    public static object?[] Frexp(double f)
    {
        if (f == 0) return new object?[] { f, 0L };                 // ±0
        if (double.IsInfinity(f) || double.IsNaN(f)) return new object?[] { f, 0L };
        var (nf, e) = Normalize(f);
        ulong x = Float64bits(nf);
        long exp = e + (long)((x >> fShift) & fMask) - fBias + 1;
        x &= ~(fMask << fShift);
        x |= (ulong)(-1 + fBias) << fShift;
        return new object?[] { Float64frombits(x), exp };
    }

    public static double Ldexp(double frac, long exp)
    {
        if (frac == 0) return frac;                                  // ±0
        if (double.IsInfinity(frac) || double.IsNaN(frac)) return frac;
        var (nf, e) = Normalize(frac);
        exp += e;
        ulong x = Float64bits(nf);
        exp += (long)((x >> fShift) & fMask) - fBias;
        if (exp < -1075) return SM.CopySign(0, frac);               // underflow
        if (exp > 1023) return frac < 0 ? double.NegativeInfinity : double.PositiveInfinity; // overflow
        double m = 1;
        if (exp < -1022) { exp += 53; m = 1.0 / (1L << 53); }        // denormal
        x &= ~(fMask << fShift);
        x |= (ulong)(exp + fBias) << fShift;
        return m * Float64frombits(x);
    }

    private static int IlogbRaw(double x)
    {
        var (nx, exp) = Normalize(x);
        return (int)((Float64bits(nx) >> fShift) & fMask) - fBias + exp;
    }

    public static long Ilogb(double x)
    {
        if (double.IsNaN(x)) return int.MaxValue;
        if (x == 0) return int.MinValue;
        if (double.IsInfinity(x)) return int.MaxValue;
        return IlogbRaw(x);
    }

    public static double Logb(double x)
    {
        if (x == 0) return double.NegativeInfinity;
        if (double.IsInfinity(x)) return double.PositiveInfinity;
        if (double.IsNaN(x)) return x;
        return IlogbRaw(x);
    }

    public static double Nextafter(double x, double y)
    {
        if (double.IsNaN(x) || double.IsNaN(y)) return double.NaN;
        if (x == y) return x;
        if (x == 0) return SM.CopySign(Float64frombits(1), y);
        if ((y > x) == (x > 0)) return Float64frombits(Float64bits(x) + 1);
        return Float64frombits(Float64bits(x) - 1);
    }

    public static float Nextafter32(float x, float y)
    {
        if (float.IsNaN(x) || float.IsNaN(y)) return (float)double.NaN;
        if (x == y) return x;
        if (x == 0) return (float)SM.CopySign(Float32frombits(1), y);
        if ((y > x) == (x > 0)) return Float32frombits(Float32bits(x) + 1);
        return Float32frombits(Float32bits(x) - 1);
    }

    public static double RoundToEven(double x)
    {
        ulong bits = Float64bits(x);
        ulong e = (bits >> fShift) & fMask;
        if (e >= fBias)
        {
            const ulong halfMinusULP = (1UL << (fShift - 1)) - 1;
            e -= fBias;
            bits += (halfMinusULP + ((bits >> (int)(fShift - e)) & 1)) >> (int)e;
            bits &= ~(fFrac >> (int)e);
        }
        else if (e == fBias - 1 && (bits & fFrac) != 0)
        {
            bits = (bits & fSign) | fUvone; // ±1
        }
        else
        {
            bits &= fSign; // ±0
        }
        return Float64frombits(bits);
    }

    public static object?[] Sincos(double x) => new object?[] { SM.Sin(x), SM.Cos(x) };
}
