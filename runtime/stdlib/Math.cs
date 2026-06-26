namespace GoCLR.Stdlib;

using SM = System.Math;

/// <summary>Shim for Go's <c>math</c> package, mapping to System.Math.</summary>
public static partial class Math
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
    // Ported from Go's math/atanh.go (built on the byte-exact Log1p) so the result matches
    // `go run` bit-for-bit — System.Math.Atanh differs by a ULP for |x| >= ~0.25.
    public static double Atanh(double x)
    {
        const double NearZero = 1.0 / (1L << 28); // 2**-28
        if (x < -1 || x > 1 || double.IsNaN(x)) return double.NaN;
        if (x == 1) return double.PositiveInfinity;
        if (x == -1) return double.NegativeInfinity;
        bool sign = false;
        if (x < 0) { x = -x; sign = true; }
        double temp;
        if (x < NearZero) temp = x;
        else if (x < 0.5) { temp = x + x; temp = 0.5 * Log1p(temp + temp * x / (1 - x)); }
        else temp = 0.5 * Log1p((x + x) / (1 - x));
        if (sign) temp = -temp;
        return temp;
    }
    public static double Exp(double x) => SM.Exp(x);
    // Exp2: System.Math.Pow(2, x) differs from Go at the last ULP (e.g. Exp2(0.5)=√2). This is
    // a faithful port of Go's math.exp2 (argument reduction + the shared expmulti tail).
    public static double Exp2(double x) => Exp2Impl(x);

    private static double Exp2Impl(double x)
    {
        const double Ln2Hi = 6.93147180369123816490e-01;
        const double Ln2Lo = 1.90821492927058770002e-10;
        const double Overflow = 1.0239999999999999e+03;
        const double Underflow = -1.0740e+03;

        if (double.IsNaN(x) || double.IsPositiveInfinity(x)) return x;
        if (double.IsNegativeInfinity(x)) return 0;
        if (x > Overflow) return double.PositiveInfinity;
        if (x < Underflow) return 0;

        int k = 0;
        if (x > 0) k = (int)(x + 0.5);
        else if (x < 0) k = (int)(x - 0.5);
        double t = x - k;
        double hi = t * Ln2Hi;
        double lo = -t * Ln2Lo;
        return Expmulti(hi, lo, k);
    }

    // The shared tail of Go's exp/exp2: y ≈ e**(hi-lo) scaled by 2**k.
    private static double Expmulti(double hi, double lo, int k)
    {
        const double P1 = 1.66666666666666657415e-01;
        const double P2 = -2.77777777770155933842e-03;
        const double P3 = 6.61375632143793436117e-05;
        const double P4 = -1.65339022054652515390e-06;
        const double P5 = 4.13813679705723846039e-08;
        double r = hi - lo;
        double t = r * r;
        double c = r - t * (P1 + t * (P2 + t * (P3 + t * (P4 + t * P5))));
        double y = 1 - ((lo - (r * c) / (2 - c)) - hi);
        return SM.ScaleB(y, k); // Ldexp(y, k)
    }
    // Expm1/Log1p: a naive Exp(x)-1 / Log(1+x) loses precision for small |x| (catastrophic
    // cancellation), diverging from Go. These are faithful ports of Go's fdlibm-based
    // math.expm1 / math.log1p so the result is byte-exact across the whole range.
    public static double Expm1(double x) => Expm1Impl(x);
    public static double Log1p(double x) => Log1pImpl(x);

    private static double Bits(ulong b) => System.BitConverter.Int64BitsToDouble((long)b);

    private static double Expm1Impl(double x)
    {
        const double Othreshold = 7.09782712893383973096e+02;
        const double Ln2X56 = 3.88162421113569373274e+01;
        const double Ln2HalfX3 = 1.03972077083991796413e+00;
        const double Ln2Half = 3.46573590279972654709e-01;
        const double Ln2Hi = 6.93147180369123816490e-01;
        const double Ln2Lo = 1.90821492927058770002e-10;
        const double InvLn2 = 1.44269504088896338700e+00;
        const double Tiny = 1.0 / (1L << 54);
        const double Q1 = -3.33333333333331316428e-02;
        const double Q2 = 1.58730158725481460165e-03;
        const double Q3 = -7.93650757867487942473e-05;
        const double Q4 = 4.00821782732936239552e-06;
        const double Q5 = -2.01099218183624371326e-07;

        if (double.IsNaN(x) || double.IsPositiveInfinity(x)) return x;
        if (double.IsNegativeInfinity(x)) return -1;

        double absx = x;
        bool sign = false;
        if (x < 0) { absx = -absx; sign = true; }

        if (absx >= Ln2X56)
        {
            if (sign) return -1;
            if (absx >= Othreshold) return double.PositiveInfinity;
        }

        double c = 0;
        int k;
        if (absx > Ln2Half)
        {
            double hi, lo;
            if (absx < Ln2HalfX3)
            {
                if (!sign) { hi = x - Ln2Hi; lo = Ln2Lo; k = 1; }
                else { hi = x + Ln2Hi; lo = -Ln2Lo; k = -1; }
            }
            else
            {
                k = !sign ? (int)(InvLn2 * x + 0.5) : (int)(InvLn2 * x - 0.5);
                double tt = k;
                hi = x - tt * Ln2Hi;
                lo = tt * Ln2Lo;
            }
            x = hi - lo;
            c = (hi - x) - lo;
        }
        else if (absx < Tiny) return x;
        else k = 0;

        double hfx = 0.5 * x;
        double hxs = x * hfx;
        double r1 = 1 + hxs * (Q1 + hxs * (Q2 + hxs * (Q3 + hxs * (Q4 + hxs * Q5))));
        double t = 3 - r1 * hfx;
        double e = hxs * ((r1 - t) / (6.0 - x * t));
        if (k == 0) return x - (x * e - hxs);
        e = (x * (e - c) - c);
        e -= hxs;
        if (k == -1) return 0.5 * (x - e) - 0.5;
        if (k == 1)
        {
            if (x < -0.25) return -2 * (e - (x + 0.5));
            return 1 + 2 * (x - e);
        }
        if (k <= -2 || k > 56)
        {
            double yy = 1 - (e - x);
            if (k == 1024) yy = yy * 2 * 8.98846567431158e+307; // 0x1p1023
            else yy = yy * Bits((ulong)(0x3ff + k) << 52);
            return yy - 1;
        }
        if (k < 20)
        {
            t = Bits(0x3ff0000000000000UL - (0x0020000000000000UL >> k)); // 1 - 2**-k
            double yy = t - (e - x);
            yy = yy * Bits((ulong)(0x3ff + k) << 52); // 2**k
            return yy;
        }
        t = Bits((ulong)(0x3ff - k) << 52); // 2**-k
        double y = x - (e + t);
        y++;
        y = y * Bits((ulong)(0x3ff + k) << 52); // 2**k
        return y;
    }

    private static double Log1pImpl(double x)
    {
        const double Sqrt2M1 = 4.142135623730950488017e-01;
        const double Sqrt2HalfM1 = -2.928932188134524755992e-01;
        const double Small = 1.0 / (1L << 29);
        const double Tiny = 1.0 / (1L << 54);
        const double Two53 = (double)(1L << 53);
        const double Ln2Hi = 6.93147180369123816490e-01;
        const double Ln2Lo = 1.90821492927058770002e-10;
        const double Lp1 = 6.666666666666735130e-01;
        const double Lp2 = 3.999999999940941908e-01;
        const double Lp3 = 2.857142874366239149e-01;
        const double Lp4 = 2.222219843214978396e-01;
        const double Lp5 = 1.818357216161805012e-01;
        const double Lp6 = 1.531383769920937332e-01;
        const double Lp7 = 1.479819860511658591e-01;

        if (x < -1 || double.IsNaN(x)) return double.NaN;
        if (x == -1) return double.NegativeInfinity;
        if (double.IsPositiveInfinity(x)) return double.PositiveInfinity;

        double absx = System.Math.Abs(x);
        double f = 0;
        ulong iu = 0;
        int k = 1;
        if (absx < Sqrt2M1)
        {
            if (absx < Small)
            {
                if (absx < Tiny) return x;
                return x - x * x * 0.5;
            }
            if (x > Sqrt2HalfM1) { k = 0; f = x; iu = 1; }
        }
        double c = 0;
        if (k != 0)
        {
            double u;
            if (absx < Two53)
            {
                u = 1.0 + x;
                iu = (ulong)System.BitConverter.DoubleToInt64Bits(u);
                k = (int)((iu >> 52) - 1023);
                c = (k > 0) ? 1.0 - (u - x) : x - (u - 1.0);
                c /= u;
            }
            else
            {
                u = x;
                iu = (ulong)System.BitConverter.DoubleToInt64Bits(u);
                k = (int)((iu >> 52) - 1023);
                c = 0;
            }
            iu &= 0x000fffffffffffffUL;
            if (iu < 0x0006a09e667f3bcdUL)
                u = Bits(iu | 0x3ff0000000000000UL); // normalize u
            else
            {
                k++;
                u = Bits(iu | 0x3fe0000000000000UL); // normalize u/2
                iu = (0x0010000000000000UL - iu) >> 2;
            }
            f = u - 1.0;
        }
        double hfsq = 0.5 * f * f;
        if (iu == 0) // |f| < 2**-20
        {
            if (f == 0)
            {
                if (k == 0) return 0;
                c += k * Ln2Lo;
                return k * Ln2Hi + c;
            }
            double R0 = hfsq * (1.0 - 0.66666666666666666 * f);
            if (k == 0) return f - R0;
            return k * Ln2Hi - ((R0 - (k * Ln2Lo + c)) - f);
        }
        double s = f / (2.0 + f);
        double z = s * s;
        double R = z * (Lp1 + z * (Lp2 + z * (Lp3 + z * (Lp4 + z * (Lp5 + z * (Lp6 + z * Lp7))))));
        if (k == 0) return f - (hfsq - s * (hfsq + R));
        return k * Ln2Hi - ((hfsq - (s * (hfsq + R) + (k * Ln2Lo + c))) - f);
    }
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

    // --- math.Lgamma (faithful port of Go's src/math/lgamma.go) ---
    private static readonly double[] _lgamA = {
        7.72156649015328655494e-02, 3.22467033424113591611e-01, 6.73523010531292681824e-02, 2.05808084325167332806e-02,
        7.38555086081402883957e-03, 2.89051383673415629091e-03, 1.19270763183362067845e-03, 5.10069792153511336608e-04,
        2.20862790713908385557e-04, 1.08011567247583939954e-04, 2.52144565451257326939e-05, 4.48640949618915160150e-05,
    };
    private static readonly double[] _lgamR = {
        1.0, 1.39200533467621045958e+00, 7.21935547567138069525e-01, 1.71933865632803078993e-01,
        1.86459191715652901344e-02, 7.77942496381893596434e-04, 7.32668430744625636189e-06,
    };
    private static readonly double[] _lgamS = {
        -7.72156649015328655494e-02, 2.14982415960608852501e-01, 3.25778796408930981787e-01, 1.46350472652464452805e-01,
        2.66422703033638609560e-02, 1.84028451407337715652e-03, 3.19475326584100867617e-05,
    };
    private static readonly double[] _lgamT = {
        4.83836122723810047042e-01, -1.47587722994593911752e-01, 6.46249402391333854778e-02, -3.27885410759859649565e-02,
        1.79706750811820387126e-02, -1.03142241298341437450e-02, 6.10053870246291332635e-03, -3.68452016781138256760e-03,
        2.25964780900612472250e-03, -1.40346469989232843813e-03, 8.81081882437654011382e-04, -5.38595305356740546715e-04,
        3.15632070903625950361e-04, -3.12754168375120860518e-04, 3.35529192635519073543e-04,
    };
    private static readonly double[] _lgamU = {
        -7.72156649015328655494e-02, 6.32827064025093366517e-01, 1.45492250137234768737e+00, 9.77717527963372745603e-01,
        2.28963728064692451092e-01, 1.33810918536787660377e-02,
    };
    private static readonly double[] _lgamV = {
        1.0, 2.45597793713041134822e+00, 2.12848976379893395361e+00, 7.69285150456672783825e-01,
        1.04222645593369134254e-01, 3.21709242282423911810e-03,
    };
    private static readonly double[] _lgamW = {
        4.18938533204672725052e-01, 8.33333333333329678849e-02, -2.77777777728775536470e-03, 7.93650558643019558500e-04,
        -5.95187557450339963135e-04, 8.36339918996282139126e-04, -1.63092934096575273989e-03,
    };

    private static double SinPi(double x)
    {
        const double Two52s = 1L << 52, Two53s = 1L << 53;
        if (x < 0.25) return -SM.Sin(System.Math.PI * x);
        double z = SM.Floor(x);
        int n;
        if (z != x)
        {
            x = x % 2;
            n = (int)(x * 4);
        }
        else
        {
            if (x >= Two53s) { x = 0; n = 0; }
            else
            {
                if (x < Two52s) z = x + Two52s;
                n = (int)(1 & Float64bits(z));
                x = n;
                n <<= 2;
            }
        }
        switch (n)
        {
            case 0: x = SM.Sin(System.Math.PI * x); break;
            case 1: case 2: x = SM.Cos(System.Math.PI * (0.5 - x)); break;
            case 3: case 4: x = SM.Sin(System.Math.PI * (1 - x)); break;
            case 5: case 6: x = -SM.Cos(System.Math.PI * (x - 1.5)); break;
            default: x = SM.Sin(System.Math.PI * (x - 2)); break;
        }
        return -x;
    }

    public static object?[] Lgamma(double x)
    {
        const double Ymin = 1.461632144968362245;
        const double Two52 = 1L << 52, Two58 = 1L << 58;
        // 2^-70. NOTE: 1L<<70 would wrap (C# masks the shift count to 6), giving 1/64 — that
        // made every |x|<0.015625 take the -Log(x) shortcut and skip the polynomial correction.
        const double Tiny = 1.0 / 1180591620717411303424.0;
        const double Tc = 1.46163214496836224576e+00, Tf = -1.21486290535849611461e-01, Tt = -3.63867699703950536541e-18;
        long sign = 1;
        if (double.IsNaN(x)) return new object?[] { x, sign };
        if (double.IsInfinity(x)) return new object?[] { x, sign };
        if (x == 0) return new object?[] { double.PositiveInfinity, sign };

        bool neg = false;
        if (x < 0) { x = -x; neg = true; }
        if (x < Tiny)
        {
            if (neg) sign = -1;
            return new object?[] { -SM.Log(x), sign };
        }
        double nadj = 0;
        if (neg)
        {
            if (x >= Two52) return new object?[] { double.PositiveInfinity, sign };
            double t0 = SinPi(x);
            if (t0 == 0) return new object?[] { double.PositiveInfinity, sign };
            nadj = SM.Log(System.Math.PI / SM.Abs(t0 * x));
            if (t0 < 0) sign = -1;
        }

        double lgamma;
        if (x == 1 || x == 2) lgamma = 0;
        else if (x < 2)
        {
            double y; int i;
            if (x <= 0.9)
            {
                lgamma = -SM.Log(x);
                if (x >= (Ymin - 1 + 0.27)) { y = 1 - x; i = 0; }
                else if (x >= (Ymin - 1 - 0.27)) { y = x - (Tc - 1); i = 1; }
                else { y = x; i = 2; }
            }
            else
            {
                lgamma = 0;
                if (x >= (Ymin + 0.27)) { y = 2 - x; i = 0; }
                else if (x >= (Ymin - 0.27)) { y = x - Tc; i = 1; }
                else { y = x - 1; i = 2; }
            }
            switch (i)
            {
                case 0:
                    {
                        double z = y * y;
                        double p1 = Madd(z, Madd(z, Madd(z, Madd(z, Madd(z, _lgamA[10], _lgamA[8]), _lgamA[6]), _lgamA[4]), _lgamA[2]), _lgamA[0]);
                        double p2 = z * Madd(z, Madd(z, Madd(z, Madd(z, Madd(z, _lgamA[11], _lgamA[9]), _lgamA[7]), _lgamA[5]), _lgamA[3]), _lgamA[1]);
                        double p = Madd(y, p1, p2);
                        lgamma += Madd(-0.5, y, p);
                        break;
                    }
                case 1:
                    {
                        double z = y * y;
                        double w = z * y;
                        double p1 = Madd(w, Madd(w, Madd(w, Madd(w, _lgamT[12], _lgamT[9]), _lgamT[6]), _lgamT[3]), _lgamT[0]);
                        double p2 = Madd(w, Madd(w, Madd(w, Madd(w, _lgamT[13], _lgamT[10]), _lgamT[7]), _lgamT[4]), _lgamT[1]);
                        double p3 = Madd(w, Madd(w, Madd(w, Madd(w, _lgamT[14], _lgamT[11]), _lgamT[8]), _lgamT[5]), _lgamT[2]);
                        double p = Madd(z, p1, -Madd(-w, Madd(y, p3, p2), Tt));
                        lgamma += Tf + p;
                        break;
                    }
                default:
                    {
                        // Go's lgamma case 2 uses plain multiply-adds (no FMA), so mirror that
                        // exactly to stay byte-exact for tiny x (e.g. 0.01, 0.18).
                        double p1 = _lgamU[0] + y * (_lgamU[1] + y * (_lgamU[2] + y * (_lgamU[3] + y * (_lgamU[4] + y * _lgamU[5]))));
                        double p2 = 1 + y * (_lgamV[1] + y * (_lgamV[2] + y * (_lgamV[3] + y * (_lgamV[4] + y * _lgamV[5]))));
                        lgamma += -0.5 * y + y * p1 / p2;
                        break;
                    }
            }
        }
        else if (x < 8)
        {
            int i = (int)x;
            double y = x - i;
            double p = y * Madd(y, Madd(y, Madd(y, Madd(y, Madd(y, Madd(y, _lgamS[6], _lgamS[5]), _lgamS[4]), _lgamS[3]), _lgamS[2]), _lgamS[1]), _lgamS[0]);
            double q = Madd(y, Madd(y, Madd(y, Madd(y, Madd(y, _lgamR[6], _lgamR[5]), _lgamR[4]), _lgamR[3]), _lgamR[2]), _lgamR[1]);
            q = Madd(y, q, 1);
            lgamma = Madd(0.5, y, p / q);
            double z = 1.0;
            switch (i)
            {
                case 7: z *= (y + 6); goto case 6;
                case 6: z *= (y + 5); goto case 5;
                case 5: z *= (y + 4); goto case 4;
                case 4: z *= (y + 3); goto case 3;
                case 3: z *= (y + 2); lgamma += SM.Log(z); break;
            }
        }
        else if (x < Two58)
        {
            double t = SM.Log(x);
            double z = 1 / x;
            double y = z * z;
            double w = Madd(z, Madd(y, Madd(y, Madd(y, Madd(y, Madd(y, _lgamW[6], _lgamW[5]), _lgamW[4]), _lgamW[3]), _lgamW[2]), _lgamW[1]), _lgamW[0]);
            lgamma = Madd(x - 0.5, t - 1, w);
        }
        else lgamma = x * (SM.Log(x) - 1);

        if (neg) lgamma = nadj - lgamma;
        return new object?[] { lgamma, sign };
    }

    // --- math.Erfinv / math.Erfcinv (faithful port of Go's src/math/erfinv.go) ---
    private const double ei_a0 = 1.1975323115670912564578e0, ei_a1 = 4.7072688112383978012285e1,
        ei_a2 = 6.9706266534389598238465e2, ei_a3 = 4.8548868893843886794648e3, ei_a4 = 1.6235862515167575384252e4,
        ei_a5 = 2.3782041382114385731252e4, ei_a6 = 1.1819493347062294404278e4, ei_a7 = 8.8709406962545514830200e2;
    private const double ei_b0 = 1.0000000000000000000e0, ei_b1 = 4.2313330701600911252e1,
        ei_b2 = 6.8718700749205790830e2, ei_b3 = 5.3941960214247511077e3, ei_b4 = 2.1213794301586595867e4,
        ei_b5 = 3.9307895800092710610e4, ei_b6 = 2.8729085735721942674e4, ei_b7 = 5.2264952788528545610e3;
    private const double ei_c0 = 1.42343711074968357734e0, ei_c1 = 4.63033784615654529590e0,
        ei_c2 = 5.76949722146069140550e0, ei_c3 = 3.64784832476320460504e0, ei_c4 = 1.27045825245236838258e0,
        ei_c5 = 2.41780725177450611770e-1, ei_c6 = 2.27238449892691845833e-2, ei_c7 = 7.74545014278341407640e-4;
    private const double ei_d0 = 1.4142135623730950488016887e0, ei_d1 = 2.9036514445419946173133295e0,
        ei_d2 = 2.3707661626024532365971225e0, ei_d3 = 9.7547832001787427186894837e-1, ei_d4 = 2.0945065210512749128288442e-1,
        ei_d5 = 2.1494160384252876777097297e-2, ei_d6 = 7.7441459065157709165577218e-4, ei_d7 = 1.4859850019840355905497876e-9;
    private const double ei_e0 = 6.65790464350110377720e0, ei_e1 = 5.46378491116411436990e0,
        ei_e2 = 1.78482653991729133580e0, ei_e3 = 2.96560571828504891230e-1, ei_e4 = 2.65321895265761230930e-2,
        ei_e5 = 1.24266094738807843860e-3, ei_e6 = 2.71155556874348757815e-5, ei_e7 = 2.01033439929228813265e-7;
    private const double ei_f0 = 1.414213562373095048801689e0, ei_f1 = 8.482908416595164588112026e-1,
        ei_f2 = 1.936480946950659106176712e-1, ei_f3 = 2.103693768272068968719679e-2, ei_f4 = 1.112800997078859844711555e-3,
        ei_f5 = 2.611088405080593625138020e-5, ei_f6 = 2.010321207683943062279931e-7, ei_f7 = 2.891024605872965461538222e-15;
    private const double Ln2 = 0.693147180559945309417232121458176568075500134360255254120680;

    public static double Erfinv(double x)
    {
        if (double.IsNaN(x) || x <= -1 || x >= 1)
        {
            if (x == -1 || x == 1) return Inf((long)x);
            return double.NaN;
        }
        bool sign = false;
        if (x < 0) { x = -x; sign = true; }
        double ans;
        if (x <= 0.85)
        {
            double r = Madd(-(0.25 * x), x, 0.180625); // Go contracts 0.180625 - 0.25*x*x to FMA
            double z1 = Madd(Madd(Madd(Madd(Madd(Madd(Madd(ei_a7, r, ei_a6), r, ei_a5), r, ei_a4), r, ei_a3), r, ei_a2), r, ei_a1), r, ei_a0);
            double z2 = Madd(Madd(Madd(Madd(Madd(Madd(Madd(ei_b7, r, ei_b6), r, ei_b5), r, ei_b4), r, ei_b3), r, ei_b2), r, ei_b1), r, ei_b0);
            ans = (x * z1) / z2;
        }
        else
        {
            double z1, z2;
            double r = SM.Sqrt(Ln2 - SM.Log(1.0 - x));
            if (r <= 5.0)
            {
                r -= 1.6;
                z1 = Madd(Madd(Madd(Madd(Madd(Madd(Madd(ei_c7, r, ei_c6), r, ei_c5), r, ei_c4), r, ei_c3), r, ei_c2), r, ei_c1), r, ei_c0);
                z2 = Madd(Madd(Madd(Madd(Madd(Madd(Madd(ei_d7, r, ei_d6), r, ei_d5), r, ei_d4), r, ei_d3), r, ei_d2), r, ei_d1), r, ei_d0);
            }
            else
            {
                r -= 5.0;
                z1 = Madd(Madd(Madd(Madd(Madd(Madd(Madd(ei_e7, r, ei_e6), r, ei_e5), r, ei_e4), r, ei_e3), r, ei_e2), r, ei_e1), r, ei_e0);
                z2 = Madd(Madd(Madd(Madd(Madd(Madd(Madd(ei_f7, r, ei_f6), r, ei_f5), r, ei_f4), r, ei_f3), r, ei_f2), r, ei_f1), r, ei_f0);
            }
            ans = z1 / z2;
        }
        return sign ? -ans : ans;
    }

    public static double Erfcinv(double x) => Erfinv(1 - x);

    // --- math.Erf / math.Erfc (faithful port of Go's src/math/erf.go; Sun fdlibm constants) ---
    private const double erf_erx = 8.45062911510467529297e-01;
    private const double erf_efx = 1.28379167095512586316e-01, erf_efx8 = 1.02703333676410069053e+00;
    private const double erf_pp0 = 1.28379167095512558561e-01, erf_pp1 = -3.25042107247001499370e-01,
        erf_pp2 = -2.84817495755985104766e-02, erf_pp3 = -5.77027029648944159157e-03, erf_pp4 = -2.37630166566501626084e-05;
    private const double erf_qq1 = 3.97917223959155352819e-01, erf_qq2 = 6.50222499887672944485e-02,
        erf_qq3 = 5.08130628187576562776e-03, erf_qq4 = 1.32494738004321644526e-04, erf_qq5 = -3.96022827877536812320e-06;
    private const double erf_pa0 = -2.36211856075265944077e-03, erf_pa1 = 4.14856118683748331666e-01,
        erf_pa2 = -3.72207876035701323847e-01, erf_pa3 = 3.18346619901161753674e-01, erf_pa4 = -1.10894694282396677476e-01,
        erf_pa5 = 3.54783043256182359371e-02, erf_pa6 = -2.16637559486879084300e-03;
    private const double erf_qa1 = 1.06420880400844228286e-01, erf_qa2 = 5.40397917702171048937e-01,
        erf_qa3 = 7.18286544141962662868e-02, erf_qa4 = 1.26171219808761642112e-01, erf_qa5 = 1.36370839120290507362e-02,
        erf_qa6 = 1.19844998467991074170e-02;
    private const double erf_ra0 = -9.86494403484714822705e-03, erf_ra1 = -6.93858572707181764372e-01,
        erf_ra2 = -1.05586262253232909814e+01, erf_ra3 = -6.23753324503260060396e+01, erf_ra4 = -1.62396669462573470355e+02,
        erf_ra5 = -1.84605092906711035994e+02, erf_ra6 = -8.12874355063065934246e+01, erf_ra7 = -9.81432934416914548592e+00;
    private const double erf_sa1 = 1.96512716674392571292e+01, erf_sa2 = 1.37657754143519042600e+02,
        erf_sa3 = 4.34565877475229228821e+02, erf_sa4 = 6.45387271733267880336e+02, erf_sa5 = 4.29008140027567833386e+02,
        erf_sa6 = 1.08635005541779435134e+02, erf_sa7 = 6.57024977031928170135e+00, erf_sa8 = -6.04244152148580987438e-02;
    private const double erf_rb0 = -9.86494292470009928597e-03, erf_rb1 = -7.99283237680523006574e-01,
        erf_rb2 = -1.77579549177547519889e+01, erf_rb3 = -1.60636384855821916062e+02, erf_rb4 = -6.37566443368389627722e+02,
        erf_rb5 = -1.02509513161107724954e+03, erf_rb6 = -4.83519191608651397019e+02;
    private const double erf_sb1 = 3.03380607434824582924e+01, erf_sb2 = 3.25792512996573918826e+02,
        erf_sb3 = 1.53672958608443695994e+03, erf_sb4 = 3.19985821950859553908e+03, erf_sb5 = 2.55305040643316442583e+03,
        erf_sb6 = 4.74528541206955367215e+02, erf_sb7 = -2.24409524465858183362e+01;

    // Fused multiply-add a*b+c (single rounding). The erf/lgamma/erfinv ports use this to
    // reproduce Go's results bit-for-bit across the tested range. (A couple of extreme inputs
    // — Erfc(10), large-argument trig — still differ by the last ULP; documented.)
    private static double Madd(double a, double b, double c) => SM.FusedMultiplyAdd(a, b, c);
    public static double Erf(double x)
    {
        const double VeryTiny = 2.848094538889218e-306;
        const double Small = 1.0 / (1 << 28);
        if (double.IsNaN(x)) return double.NaN;
        if (double.IsPositiveInfinity(x)) return 1;
        if (double.IsNegativeInfinity(x)) return -1;
        bool sign = false;
        if (x < 0) { x = -x; sign = true; }
        if (x < 0.84375)
        {
            double temp;
            if (x < Small)
            {
                if (x < VeryTiny) temp = 0.125 * (8.0 * x + erf_efx8 * x);
                else temp = x + erf_efx * x;
            }
            else
            {
                double z = x * x;
                double r = Madd(z, Madd(z, Madd(z, Madd(z, erf_pp4, erf_pp3), erf_pp2), erf_pp1), erf_pp0);
                double s = Madd(z, Madd(z, Madd(z, Madd(z, Madd(z, erf_qq5, erf_qq4), erf_qq3), erf_qq2), erf_qq1), 1);
                temp = Madd(x, r / s, x);
            }
            return sign ? -temp : temp;
        }
        if (x < 1.25)
        {
            double s = x - 1;
            double P = Madd(s, Madd(s, Madd(s, Madd(s, Madd(s, Madd(s, erf_pa6, erf_pa5), erf_pa4), erf_pa3), erf_pa2), erf_pa1), erf_pa0);
            double Q = Madd(s, Madd(s, Madd(s, Madd(s, Madd(s, Madd(s, erf_qa6, erf_qa5), erf_qa4), erf_qa3), erf_qa2), erf_qa1), 1);
            return sign ? -erf_erx - P / Q : erf_erx + P / Q;
        }
        if (x >= 6) return sign ? -1 : 1;
        double s2 = 1 / (x * x);
        double R, S;
        if (x < 1 / 0.35)
        {
            R = Madd(s2, Madd(s2, Madd(s2, Madd(s2, Madd(s2, Madd(s2, Madd(s2, erf_ra7, erf_ra6), erf_ra5), erf_ra4), erf_ra3), erf_ra2), erf_ra1), erf_ra0);
            S = Madd(s2, Madd(s2, Madd(s2, Madd(s2, Madd(s2, Madd(s2, Madd(s2, Madd(s2, erf_sa8, erf_sa7), erf_sa6), erf_sa5), erf_sa4), erf_sa3), erf_sa2), erf_sa1), 1);
        }
        else
        {
            R = Madd(s2, Madd(s2, Madd(s2, Madd(s2, Madd(s2, Madd(s2, erf_rb6, erf_rb5), erf_rb4), erf_rb3), erf_rb2), erf_rb1), erf_rb0);
            S = Madd(s2, Madd(s2, Madd(s2, Madd(s2, Madd(s2, Madd(s2, Madd(s2, erf_sb7, erf_sb6), erf_sb5), erf_sb4), erf_sb3), erf_sb2), erf_sb1), 1);
        }
        double zz = Float64frombits(Float64bits(x) & 0xffffffff00000000UL);
        double rr = SM.Exp(-zz * zz - 0.5625) * SM.Exp(Madd(zz - x, zz + x, R / S));
        return sign ? rr / x - 1 : 1 - rr / x;
    }

    public static double Erfc(double x)
    {
        const double Tiny = 1.0 / (1L << 56);
        if (double.IsNaN(x)) return double.NaN;
        if (double.IsPositiveInfinity(x)) return 0;
        if (double.IsNegativeInfinity(x)) return 2;
        bool sign = false;
        if (x < 0) { x = -x; sign = true; }
        if (x < 0.84375)
        {
            double temp;
            if (x < Tiny) temp = x;
            else
            {
                double z = x * x;
                double r = Madd(z, Madd(z, Madd(z, Madd(z, erf_pp4, erf_pp3), erf_pp2), erf_pp1), erf_pp0);
                double s = Madd(z, Madd(z, Madd(z, Madd(z, Madd(z, erf_qq5, erf_qq4), erf_qq3), erf_qq2), erf_qq1), 1);
                double y = r / s;
                if (x < 0.25) temp = Madd(x, y, x);
                else temp = 0.5 + Madd(x, y, x - 0.5);
            }
            return sign ? 1 + temp : 1 - temp;
        }
        if (x < 1.25)
        {
            double s = x - 1;
            double P = Madd(s, Madd(s, Madd(s, Madd(s, Madd(s, Madd(s, erf_pa6, erf_pa5), erf_pa4), erf_pa3), erf_pa2), erf_pa1), erf_pa0);
            double Q = Madd(s, Madd(s, Madd(s, Madd(s, Madd(s, Madd(s, erf_qa6, erf_qa5), erf_qa4), erf_qa3), erf_qa2), erf_qa1), 1);
            return sign ? 1 + erf_erx + P / Q : 1 - erf_erx - P / Q;
        }
        if (x < 28)
        {
            double s = 1 / (x * x);
            double R, S;
            if (x < 1 / 0.35)
            {
                R = Madd(s, Madd(s, Madd(s, Madd(s, Madd(s, Madd(s, Madd(s, erf_ra7, erf_ra6), erf_ra5), erf_ra4), erf_ra3), erf_ra2), erf_ra1), erf_ra0);
                S = Madd(s, Madd(s, Madd(s, Madd(s, Madd(s, Madd(s, Madd(s, Madd(s, erf_sa8, erf_sa7), erf_sa6), erf_sa5), erf_sa4), erf_sa3), erf_sa2), erf_sa1), 1);
            }
            else
            {
                if (sign && x > 6) return 2;
                R = Madd(s, Madd(s, Madd(s, Madd(s, Madd(s, Madd(s, erf_rb6, erf_rb5), erf_rb4), erf_rb3), erf_rb2), erf_rb1), erf_rb0);
                S = Madd(s, Madd(s, Madd(s, Madd(s, Madd(s, Madd(s, Madd(s, erf_sb7, erf_sb6), erf_sb5), erf_sb4), erf_sb3), erf_sb2), erf_sb1), 1);
            }
            double z = Float64frombits(Float64bits(x) & 0xffffffff00000000UL);
            double r = SM.Exp(-z * z - 0.5625) * SM.Exp(Madd(z - x, z + x, R / S));
            return sign ? 2 - r / x : r / x;
        }
        return sign ? 2 : 0;
    }

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
        w = Madd(w, Madd(Madd(Madd(Madd(_gamS[0], w, _gamS[1]), w, _gamS[2]), w, _gamS[3]), w, _gamS[4]), 1);
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
        p = Madd(Madd(Madd(Madd(Madd(Madd(x, _gamP[0], _gamP[1]), x, _gamP[2]), x, _gamP[3]), x, _gamP[4]), x, _gamP[5]), x, _gamP[6]);
        q = Madd(Madd(Madd(Madd(Madd(Madd(Madd(x, _gamQ[0], _gamQ[1]), x, _gamQ[2]), x, _gamQ[3]), x, _gamQ[4]), x, _gamQ[5]), x, _gamQ[6]), x, _gamQ[7]);
        return z * p / q;
    small:
        if (x == 0) return double.PositiveInfinity;
        return z / (Madd(Euler, x, 1) * x);
    }
}
