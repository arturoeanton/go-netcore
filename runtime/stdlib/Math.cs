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

    // --- math.Gamma (faithful port of Go's src/math/gamma.go; Sun/FreeBSD Cephes constants) ---
    private static readonly double[] _gamP =
    {
        1.60119522476751861407e-04, 1.19135147006586384913e-03, 1.04213797561761569935e-02,
        4.76367800457137231464e-02, 2.07448227648435975150e-01, 4.94214826801497100753e-01,
        9.99999999999999996796e-01,
    };
    private static readonly double[] _gamQ =
    {
        -2.31581873324120129819e-05, 5.39605580493303397842e-04, -4.45641913851797240494e-03,
        1.18139785222060435552e-02, 3.58236398605498653373e-02, -2.34591795718243348568e-01,
        7.14304917030273074085e-02, 1.00000000000000000320e+00,
    };
    private static readonly double[] _gamS =
    {
        7.87311395793093628397e-04, -2.29549961613378126380e-04, -2.68132617805781232825e-03,
        3.47222221605458667310e-03, 8.33333333333482257126e-02,
    };

    private static bool IsNegInt(double x)
    {
        if (x < 0) { double xf = x - SM.Truncate(x); return xf == 0; }
        return false;
    }
    private static (double, double) Stirling(double x)
    {
        if (x > 200) return (double.PositiveInfinity, 1);
        const double SqrtTwoPi = 2.506628274631000502417;
        const double MaxStirling = 143.01608;
        double w = 1 / x;
        w = 1 + w * ((((_gamS[0] * w + _gamS[1]) * w + _gamS[2]) * w + _gamS[3]) * w + _gamS[4]);
        double y1 = SM.Exp(x);
        double y2 = 1.0;
        if (x > MaxStirling) { double v = SM.Pow(x, 0.5 * x - 0.25); y2 = v / y1; y1 = v; } // Go: y1, y2 = v, v/y1 (simultaneous)
        else { y1 = SM.Pow(x, x - 0.5) / y1; }
        return (y1, SqrtTwoPi * w * y2);
    }

    public static double Gamma(double x)
    {
        const double Euler = 0.57721566490153286060651209008240243104215933593992;
        if (IsNegInt(x) || double.IsNegativeInfinity(x) || double.IsNaN(x)) return double.NaN;
        if (double.IsPositiveInfinity(x)) return double.PositiveInfinity;
        if (x == 0) return double.IsNegative(x) ? double.NegativeInfinity : double.PositiveInfinity;
        double q = SM.Abs(x);
        double p = SM.Floor(q);
        if (q > 33)
        {
            if (x >= 0) { var (a, b) = Stirling(x); return a * b; }
            int signgam = 1;
            long ip = (long)p;
            if ((ip & 1) == 0) signgam = -1;
            double zb = q - p;
            if (zb > 0.5) { p = p + 1; zb = q - p; }
            zb = q * SM.Sin(System.Math.PI * zb);
            if (zb == 0) return Inf(signgam);
            var (sq1, sq2) = Stirling(q);
            double absz = SM.Abs(zb);
            double d = absz * sq1 * sq2;
            zb = double.IsInfinity(d) ? System.Math.PI / absz / sq1 / sq2 : System.Math.PI / d;
            return signgam * zb;
        }
        double z = 1.0;
        while (x >= 3) { x = x - 1; z = z * x; }
        while (x < 0) { if (x > -1e-09) goto small; z = z / x; x = x + 1; }
        while (x < 2) { if (x < 1e-09) goto small; z = z / x; x = x + 1; }
        if (x == 2) return z;
        x = x - 2;
        p = (((((x * _gamP[0] + _gamP[1]) * x + _gamP[2]) * x + _gamP[3]) * x + _gamP[4]) * x + _gamP[5]) * x + _gamP[6];
        q = ((((((x * _gamQ[0] + _gamQ[1]) * x + _gamQ[2]) * x + _gamQ[3]) * x + _gamQ[4]) * x + _gamQ[5]) * x + _gamQ[6]) * x + _gamQ[7];
        return z * p / q;
    small:
        if (x == 0) return double.PositiveInfinity;
        return z / ((1 + Euler * x) * x);
    }
}
