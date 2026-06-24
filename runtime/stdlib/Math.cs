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
            double r = Fma(-(0.25 * x), x, 0.180625); // Go contracts 0.180625 - 0.25*x*x to FMA
            double z1 = Fma(Fma(Fma(Fma(Fma(Fma(Fma(ei_a7, r, ei_a6), r, ei_a5), r, ei_a4), r, ei_a3), r, ei_a2), r, ei_a1), r, ei_a0);
            double z2 = Fma(Fma(Fma(Fma(Fma(Fma(Fma(ei_b7, r, ei_b6), r, ei_b5), r, ei_b4), r, ei_b3), r, ei_b2), r, ei_b1), r, ei_b0);
            ans = (x * z1) / z2;
        }
        else
        {
            double z1, z2;
            double r = SM.Sqrt(Ln2 - SM.Log(1.0 - x));
            if (r <= 5.0)
            {
                r -= 1.6;
                z1 = Fma(Fma(Fma(Fma(Fma(Fma(Fma(ei_c7, r, ei_c6), r, ei_c5), r, ei_c4), r, ei_c3), r, ei_c2), r, ei_c1), r, ei_c0);
                z2 = Fma(Fma(Fma(Fma(Fma(Fma(Fma(ei_d7, r, ei_d6), r, ei_d5), r, ei_d4), r, ei_d3), r, ei_d2), r, ei_d1), r, ei_d0);
            }
            else
            {
                r -= 5.0;
                z1 = Fma(Fma(Fma(Fma(Fma(Fma(Fma(ei_e7, r, ei_e6), r, ei_e5), r, ei_e4), r, ei_e3), r, ei_e2), r, ei_e1), r, ei_e0);
                z2 = Fma(Fma(Fma(Fma(Fma(Fma(Fma(ei_f7, r, ei_f6), r, ei_f5), r, ei_f4), r, ei_f3), r, ei_f2), r, ei_f1), r, ei_f0);
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

    private static double Fma(double a, double b, double c) => SM.FusedMultiplyAdd(a, b, c);
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
                double r = Fma(z, Fma(z, Fma(z, Fma(z, erf_pp4, erf_pp3), erf_pp2), erf_pp1), erf_pp0);
                double s = Fma(z, Fma(z, Fma(z, Fma(z, Fma(z, erf_qq5, erf_qq4), erf_qq3), erf_qq2), erf_qq1), 1);
                temp = Fma(x, r / s, x);
            }
            return sign ? -temp : temp;
        }
        if (x < 1.25)
        {
            double s = x - 1;
            double P = Fma(s, Fma(s, Fma(s, Fma(s, Fma(s, Fma(s, erf_pa6, erf_pa5), erf_pa4), erf_pa3), erf_pa2), erf_pa1), erf_pa0);
            double Q = Fma(s, Fma(s, Fma(s, Fma(s, Fma(s, Fma(s, erf_qa6, erf_qa5), erf_qa4), erf_qa3), erf_qa2), erf_qa1), 1);
            return sign ? -erf_erx - P / Q : erf_erx + P / Q;
        }
        if (x >= 6) return sign ? -1 : 1;
        double s2 = 1 / (x * x);
        double R, S;
        if (x < 1 / 0.35)
        {
            R = Fma(s2, Fma(s2, Fma(s2, Fma(s2, Fma(s2, Fma(s2, Fma(s2, erf_ra7, erf_ra6), erf_ra5), erf_ra4), erf_ra3), erf_ra2), erf_ra1), erf_ra0);
            S = Fma(s2, Fma(s2, Fma(s2, Fma(s2, Fma(s2, Fma(s2, Fma(s2, Fma(s2, erf_sa8, erf_sa7), erf_sa6), erf_sa5), erf_sa4), erf_sa3), erf_sa2), erf_sa1), 1);
        }
        else
        {
            R = Fma(s2, Fma(s2, Fma(s2, Fma(s2, Fma(s2, Fma(s2, erf_rb6, erf_rb5), erf_rb4), erf_rb3), erf_rb2), erf_rb1), erf_rb0);
            S = Fma(s2, Fma(s2, Fma(s2, Fma(s2, Fma(s2, Fma(s2, Fma(s2, erf_sb7, erf_sb6), erf_sb5), erf_sb4), erf_sb3), erf_sb2), erf_sb1), 1);
        }
        double zz = Float64frombits(Float64bits(x) & 0xffffffff00000000UL);
        double rr = SM.Exp(-zz * zz - 0.5625) * SM.Exp(Fma(zz - x, zz + x, R / S));
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
                double r = Fma(z, Fma(z, Fma(z, Fma(z, erf_pp4, erf_pp3), erf_pp2), erf_pp1), erf_pp0);
                double s = Fma(z, Fma(z, Fma(z, Fma(z, Fma(z, erf_qq5, erf_qq4), erf_qq3), erf_qq2), erf_qq1), 1);
                double y = r / s;
                if (x < 0.25) temp = Fma(x, y, x);
                else temp = 0.5 + Fma(x, y, x - 0.5);
            }
            return sign ? 1 + temp : 1 - temp;
        }
        if (x < 1.25)
        {
            double s = x - 1;
            double P = Fma(s, Fma(s, Fma(s, Fma(s, Fma(s, Fma(s, erf_pa6, erf_pa5), erf_pa4), erf_pa3), erf_pa2), erf_pa1), erf_pa0);
            double Q = Fma(s, Fma(s, Fma(s, Fma(s, Fma(s, Fma(s, erf_qa6, erf_qa5), erf_qa4), erf_qa3), erf_qa2), erf_qa1), 1);
            return sign ? 1 + erf_erx + P / Q : 1 - erf_erx - P / Q;
        }
        if (x < 28)
        {
            double s = 1 / (x * x);
            double R, S;
            if (x < 1 / 0.35)
            {
                R = Fma(s, Fma(s, Fma(s, Fma(s, Fma(s, Fma(s, Fma(s, erf_ra7, erf_ra6), erf_ra5), erf_ra4), erf_ra3), erf_ra2), erf_ra1), erf_ra0);
                S = Fma(s, Fma(s, Fma(s, Fma(s, Fma(s, Fma(s, Fma(s, Fma(s, erf_sa8, erf_sa7), erf_sa6), erf_sa5), erf_sa4), erf_sa3), erf_sa2), erf_sa1), 1);
            }
            else
            {
                if (sign && x > 6) return 2;
                R = Fma(s, Fma(s, Fma(s, Fma(s, Fma(s, Fma(s, erf_rb6, erf_rb5), erf_rb4), erf_rb3), erf_rb2), erf_rb1), erf_rb0);
                S = Fma(s, Fma(s, Fma(s, Fma(s, Fma(s, Fma(s, Fma(s, erf_sb7, erf_sb6), erf_sb5), erf_sb4), erf_sb3), erf_sb2), erf_sb1), 1);
            }
            double z = Float64frombits(Float64bits(x) & 0xffffffff00000000UL);
            double r = SM.Exp(-z * z - 0.5625) * SM.Exp(Fma(z - x, z + x, R / S));
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
        w = Fma(w, Fma(Fma(Fma(Fma(_gamS[0], w, _gamS[1]), w, _gamS[2]), w, _gamS[3]), w, _gamS[4]), 1);
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
        p = Fma(Fma(Fma(Fma(Fma(Fma(x, _gamP[0], _gamP[1]), x, _gamP[2]), x, _gamP[3]), x, _gamP[4]), x, _gamP[5]), x, _gamP[6]);
        q = Fma(Fma(Fma(Fma(Fma(Fma(Fma(x, _gamQ[0], _gamQ[1]), x, _gamQ[2]), x, _gamQ[3]), x, _gamQ[4]), x, _gamQ[5]), x, _gamQ[6]), x, _gamQ[7]);
        return z * p / q;
    small:
        if (x == 0) return double.PositiveInfinity;
        return z / (Fma(Euler, x, 1) * x);
    }
}
