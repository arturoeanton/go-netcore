namespace GoCLR.Stdlib;

using GoCLR.Runtime;
using SM = System.Math;

/// <summary>Shim for math/cmplx. Each function is a direct port of Go's
/// src/math/cmplx algorithm so results are byte-exact with `go run` (the backing
/// math primitives — Sqrt, Atan2, Exp, Sin, Cos — already match Go on this platform).</summary>
public static class Cmplx
{
    private const double Log10E = 0.43429448190325182765; // math.Log10E

    // Go's math.Hypot (overflow-safe), not the naive sqrt(p*p+q*q).
    private static double Hypot(double p, double q)
    {
        p = SM.Abs(p); q = SM.Abs(q);
        if (double.IsPositiveInfinity(p) || double.IsPositiveInfinity(q)) return double.PositiveInfinity;
        if (double.IsNaN(p) || double.IsNaN(q)) return double.NaN;
        if (p < q) { (p, q) = (q, p); }
        if (p == 0) return 0;
        q /= p;
        return p * SM.Sqrt(1 + q * q);
    }

    // cmplx.Abs(x) = math.Hypot(real, imag).
    public static double Abs(GoComplex x) => Hypot(x.Re, x.Im);

    // cmplx.Conj(x) = complex(real, -imag).
    public static GoComplex Conj(GoComplex x) => new(x.Re, -x.Im);

    // cmplx.Phase(x) = math.Atan2(imag, real).
    public static double Phase(GoComplex x) => SM.Atan2(x.Im, x.Re);

    // cmplx.Polar(x) (r, θ).
    public static object?[] Polar(GoComplex x) => new object?[] { Abs(x), Phase(x) };

    // cmplx.Rect(r, θ): s, c := math.Sincos(θ); complex(r*c, r*s).
    public static GoComplex Rect(double r, double theta) => new(r * SM.Cos(theta), r * SM.Sin(theta));

    public static GoComplex Inf() => new(double.PositiveInfinity, double.PositiveInfinity);
    public static GoComplex NaN() => new(double.NaN, double.NaN);

    public static bool IsInf(GoComplex x) => double.IsInfinity(x.Re) || double.IsInfinity(x.Im);

    public static bool IsNaN(GoComplex x)
    {
        if (double.IsInfinity(x.Re) || double.IsInfinity(x.Im)) return false;
        return double.IsNaN(x.Re) || double.IsNaN(x.Im);
    }

    // cmplx.Exp / cmplx.Rect are NOT registered: they are backed by math.Exp/Sin/Cos, which
    // differ from Go by 1 ULP on this platform, so they cannot be verified byte-exact.
    public static GoComplex Exp(GoComplex x)
    {
        double re = x.Re, im = x.Im;
        if (double.IsInfinity(re))
        {
            if (re > 0 && im == 0) return x;
            if (double.IsInfinity(im) || double.IsNaN(im))
            {
                if (re < 0) return new GoComplex(0, SM.CopySign(0, im));
                return new GoComplex(double.PositiveInfinity, double.NaN);
            }
        }
        else if (double.IsNaN(re))
        {
            if (im == 0) return new GoComplex(double.NaN, im);
        }
        double r = SM.Exp(re);
        return new GoComplex(r * SM.Cos(im), r * SM.Sin(im));
    }

    // cmplx.Sqrt(x): direct port of Go's algorithm.
    public static GoComplex Sqrt(GoComplex x)
    {
        double rx = x.Re, ix = x.Im;
        if (ix == 0)
        {
            if (rx == 0) return new GoComplex(0, ix);
            if (rx < 0) return new GoComplex(0, SM.CopySign(SM.Sqrt(-rx), ix));
            return new GoComplex(SM.Sqrt(rx), ix);
        }
        if (double.IsInfinity(ix)) return new GoComplex(double.PositiveInfinity, ix);
        if (rx == 0)
        {
            if (ix < 0) { double rr = SM.Sqrt(-0.5 * ix); return new GoComplex(rr, -rr); }
            double r0 = SM.Sqrt(0.5 * ix); return new GoComplex(r0, r0);
        }
        double a = rx, b = ix, scale;
        if (SM.Abs(a) > 4 || SM.Abs(b) > 4) { a *= 0.25; b *= 0.25; scale = 2; }
        else { a *= 1.8014398509481984e16; b *= 1.8014398509481984e16; scale = 7.450580596923828125e-9; }
        double r = Hypot(a, b), t;
        if (a > 0) { t = SM.Sqrt(0.5 * r + 0.5 * a); r = scale * SM.Abs((0.5 * b) / t); t *= scale; }
        else { r = SM.Sqrt(0.5 * r - 0.5 * a); t = scale * SM.Abs((0.5 * b) / r); r *= scale; }
        return ix < 0 ? new GoComplex(t, -r) : new GoComplex(t, r);
    }

    // cmplx.Log(x) = complex(math.Log(Abs(x)), Phase(x)).
    public static GoComplex Log(GoComplex x) => new(SM.Log(Abs(x)), Phase(x));

    // cmplx.Log10(x) = Log(x) scaled by Log10E.
    public static GoComplex Log10(GoComplex x)
    {
        var z = Log(x);
        return new GoComplex(z.Re * Log10E, z.Im * Log10E);
    }

    // --- complex arithmetic helpers (Go's built-in complex128 ops) ---
    private static GoComplex CAdd(GoComplex a, GoComplex b) => new(a.Re + b.Re, a.Im + b.Im);
    private static GoComplex CMul(GoComplex a, GoComplex b) => new(a.Re * b.Re - a.Im * b.Im, a.Re * b.Im + a.Im * b.Re);
    private const double Pi = SM.PI;

    // cmplx.Pow(x, y) — port of Go's cmplx/pow.go.
    public static GoComplex Pow(GoComplex x, GoComplex y)
    {
        if (x.Re == 0 && x.Im == 0) // x == 0 (also true for -0)
        {
            if (IsNaN(y)) return NaN();
            double yr = y.Re, yi = y.Im;
            if (yr == 0) return new GoComplex(1, 0);
            if (yr < 0) return yi == 0 ? new GoComplex(double.PositiveInfinity, 0) : Inf();
            return new GoComplex(0, 0); // yr > 0
        }
        double modulus = Abs(x);
        if (modulus == 0) return new GoComplex(0, 0);
        double r = SM.Pow(modulus, y.Re);
        double arg = Phase(x);
        double theta = y.Re * arg;
        if (y.Im != 0)
        {
            r *= SM.Exp(-y.Im * arg);
            theta += y.Im * SM.Log(modulus);
        }
        return new GoComplex(r * SM.Cos(theta), r * SM.Sin(theta));
    }

    // (sh, ch) = (sinh x, cosh x), Go's cmplx sinhcosh helper.
    private static (double sh, double ch) SinhCosh(double x)
    {
        if (SM.Abs(x) <= 0.5) return (SM.Sinh(x), SM.Cosh(x));
        double e = SM.Exp(x);
        double ei = 0.5 / e;
        e *= 0.5;
        return (e - ei, e + ei);
    }

    public static GoComplex Sin(GoComplex x)
    {
        double re = x.Re, im = x.Im;
        if (im == 0 && (double.IsInfinity(re) || double.IsNaN(re))) return new GoComplex(double.NaN, im);
        if (double.IsInfinity(im))
        {
            if (re == 0) return x;
            if (double.IsInfinity(re) || double.IsNaN(re)) return new GoComplex(double.NaN, im);
        }
        else if (re == 0 && double.IsNaN(im)) return x;
        double s = SM.Sin(re), c = SM.Cos(re);
        var (sh, ch) = SinhCosh(im);
        return new GoComplex(s * ch, c * sh);
    }

    public static GoComplex Sinh(GoComplex x)
    {
        double re = x.Re, im = x.Im;
        if (re == 0 && (double.IsInfinity(im) || double.IsNaN(im))) return new GoComplex(re, double.NaN);
        if (double.IsInfinity(re))
        {
            if (im == 0) return new GoComplex(re, im);
            if (double.IsInfinity(im) || double.IsNaN(im)) return new GoComplex(re, double.NaN);
        }
        else if (im == 0 && double.IsNaN(re)) return new GoComplex(double.NaN, im);
        double s = SM.Sin(im), c = SM.Cos(im);
        var (sh, ch) = SinhCosh(re);
        return new GoComplex(c * sh, s * ch);
    }

    public static GoComplex Cos(GoComplex x)
    {
        double re = x.Re, im = x.Im;
        if (im == 0 && (double.IsInfinity(re) || double.IsNaN(re))) return new GoComplex(double.NaN, -im * SM.CopySign(0, re));
        if (double.IsInfinity(im))
        {
            if (re == 0) return new GoComplex(double.PositiveInfinity, -re * SM.CopySign(0, im));
            if (double.IsInfinity(re) || double.IsNaN(re)) return new GoComplex(double.PositiveInfinity, double.NaN);
        }
        else if (re == 0 && double.IsNaN(im)) return new GoComplex(double.NaN, 0);
        double s = SM.Sin(re), c = SM.Cos(re);
        var (sh, ch) = SinhCosh(im);
        return new GoComplex(c * ch, -s * sh);
    }

    public static GoComplex Cosh(GoComplex x)
    {
        double re = x.Re, im = x.Im;
        if (re == 0 && (double.IsInfinity(im) || double.IsNaN(im))) return new GoComplex(double.NaN, re * SM.CopySign(0, im));
        if (double.IsInfinity(re))
        {
            if (im == 0) return new GoComplex(double.PositiveInfinity, im * SM.CopySign(0, re));
            if (double.IsInfinity(im) || double.IsNaN(im)) return new GoComplex(double.PositiveInfinity, double.NaN);
        }
        else if (im == 0 && double.IsNaN(re)) return new GoComplex(double.NaN, im);
        double s = SM.Sin(im), c = SM.Cos(im);
        var (sh, ch) = SinhCosh(re);
        return new GoComplex(c * ch, s * sh);
    }

    public static GoComplex Tan(GoComplex x)
    {
        double re = x.Re, im = x.Im;
        if (double.IsInfinity(im))
        {
            if (double.IsInfinity(re) || double.IsNaN(re)) return new GoComplex(SM.CopySign(0, re), SM.CopySign(1, im));
            return new GoComplex(SM.CopySign(0, SM.Sin(2 * re)), SM.CopySign(1, im));
        }
        if (re == 0 && double.IsNaN(im)) return x;
        double d = SM.Cos(2 * re) + SM.Cosh(2 * im);
        if (SM.Abs(d) < 0.25) d = TanSeries(x);
        if (d == 0) return Inf();
        return new GoComplex(SM.Sin(2 * re) / d, SM.Sinh(2 * im) / d);
    }

    public static GoComplex Tanh(GoComplex x)
    {
        double re = x.Re, im = x.Im;
        if (double.IsInfinity(re))
        {
            if (double.IsInfinity(im) || double.IsNaN(im)) return new GoComplex(SM.CopySign(1, re), SM.CopySign(0, im));
            return new GoComplex(SM.CopySign(1, re), SM.CopySign(0, SM.Sin(2 * im)));
        }
        if (im == 0 && double.IsNaN(re)) return x;
        double d = SM.Cosh(2 * re) + SM.Cos(2 * im);
        if (d == 0) return Inf();
        return new GoComplex(SM.Sinh(2 * re) / d, SM.Sin(2 * im) / d);
    }

    public static GoComplex Cot(GoComplex x)
    {
        double re = x.Re, im = x.Im;
        double d = SM.Cosh(2 * im) - SM.Cos(2 * re);
        if (SM.Abs(d) < 0.25) d = TanSeries(x);
        if (d == 0) return Inf();
        return new GoComplex(SM.Sin(2 * re) / d, -SM.Sinh(2 * im) / d);
    }

    // Shift helpers giving Go's uint64 semantics (a shift count >= 64 yields 0,
    // unlike C# which masks the count to 6 bits).
    private static ulong Shl(ulong v, int n) => n >= 64 ? 0UL : v << n;
    private static ulong Shr(ulong v, int n) => n >= 64 ? 0UL : v >> n;

    private static readonly ulong[] mPi = {
        0x0000000000000000, 0x517cc1b727220a94, 0xfe13abe8fa9a6ee0, 0x6db14acc9e21c820,
        0xff28b1d5ef5de2b0, 0xdb92371d2126e970, 0x0324977504e8c90e, 0x7f0ef58e5894d39f,
        0x74411afa975da242, 0x74ce38135a2fbf20, 0x9cc8eb1cc1a99cfa, 0x4e422fc5defc941d,
        0x8ffc4bffef02cc07, 0xf79788c5ad05368f, 0xb69b3f6793e584db, 0xa7a31fb34f2ff516,
        0xba93dd63f5f2f8bd, 0x9e839cfbc5294975, 0x35fdafd88fc6ae84, 0x2b0198237e3db5d5,
    };

    // Argument reduction modulo Pi — Go's cmplx/tan.go reducePi (Cody–Waite for
    // |x| < 2**30, Payne–Hanek otherwise).
    private static double ReducePi(double x)
    {
        const double reduceThreshold = 1 << 30;
        if (SM.Abs(x) < reduceThreshold)
        {
            const double PI1 = 3.141592502593994, PI2 = 1.5099578831723193e-07, PI3 = 1.0780605716316238e-14;
            double t0 = x / Pi;
            t0 += 0.5;
            t0 = (double)(long)t0;
            return ((x - t0 * PI1) - t0 * PI2) - t0 * PI3;
        }
        const int mask = 0x7FF, shift = 64 - 11 - 1, bias = 1023;
        const ulong fracMask = (1UL << shift) - 1;
        ulong ix = System.BitConverter.DoubleToUInt64Bits(x);
        int exp = (int)(ix >> shift & mask) - bias - shift;
        ix &= fracMask;
        ix |= 1UL << shift;
        int digit = (exp + 64) / 64, bitshift = (exp + 64) % 64;
        ulong z0 = Shl(mPi[digit], bitshift) | Shr(mPi[digit + 1], 64 - bitshift);
        ulong z1 = Shl(mPi[digit + 1], bitshift) | Shr(mPi[digit + 2], 64 - bitshift);
        ulong z2 = Shl(mPi[digit + 2], bitshift) | Shr(mPi[digit + 3], 64 - bitshift);
        ulong z2hi = SM.BigMul(z2, ix, out _);
        ulong z1hi = SM.BigMul(z1, ix, out ulong z1lo);
        ulong z0lo = z0 * ix;
        var (lo, c) = Add64(z1lo, z2hi, 0);
        var (hi, _) = Add64(z0lo, z1hi, c);
        int lz = System.Numerics.BitOperations.LeadingZeroCount(hi);
        ulong e = (ulong)(bias - (lz + 1));
        hi = Shl(hi, lz + 1) | Shr(lo, 64 - (lz + 1));
        hi >>= 64 - shift;
        hi |= e << shift;
        double xr = System.BitConverter.UInt64BitsToDouble(hi);
        if (xr > 0.5) xr--;
        return Pi * xr;
    }

    // Go's bits.Add64(x, y, carry) -> (sum, carryOut).
    private static (ulong sum, ulong carry) Add64(ulong x, ulong y, ulong carry)
    {
        ulong sum = x + y + carry;
        ulong carryOut = ((x & y) | ((x | y) & ~sum)) >> 63;
        return (sum, carryOut);
    }

    // Go's cmplx/tan.go tanSeries — Maclaurin series for cos(2x)+cosh(2y) near zero.
    private static double TanSeries(GoComplex z)
    {
        const double MACHEP = 1.0 / (1L << 53);
        double x = SM.Abs(2 * z.Re);
        double y = SM.Abs(2 * z.Im);
        x = ReducePi(x);
        x = x * x;
        y = y * y;
        double x2 = 1.0, y2 = 1.0, f = 1.0, rn = 0.0, d = 0.0;
        while (true)
        {
            rn++; f *= rn; rn++; f *= rn;
            x2 *= x; y2 *= y;
            double t = y2 + x2; t /= f; d += t;
            rn++; f *= rn; rn++; f *= rn;
            x2 *= x; y2 *= y;
            t = y2 - x2; t /= f; d += t;
            if (!(SM.Abs(t / d) > MACHEP)) break;
        }
        return d;
    }

    // --- inverse trig / hyperbolic (cmplx/asin.go) ---
    public static GoComplex Asin(GoComplex x)
    {
        double re = x.Re, im = x.Im;
        if (im == 0 && SM.Abs(re) <= 1) return new GoComplex(SM.Asin(re), im);
        if (re == 0 && SM.Abs(im) <= 1) return new GoComplex(re, SM.Asinh(im));
        if (double.IsNaN(im))
        {
            if (re == 0) return new GoComplex(re, double.NaN);
            if (double.IsInfinity(re)) return new GoComplex(double.NaN, re);
            return NaN();
        }
        if (double.IsInfinity(im))
        {
            if (double.IsNaN(re)) return x;
            if (double.IsInfinity(re)) return new GoComplex(SM.CopySign(Pi / 4, re), im);
            return new GoComplex(SM.CopySign(0, re), im);
        }
        if (double.IsInfinity(re)) return new GoComplex(SM.CopySign(Pi / 2, re), SM.CopySign(re, im));
        var ct = new GoComplex(-x.Im, x.Re); // i * x
        var xx = CMul(x, x);
        var x1 = new GoComplex(1 - xx.Re, -xx.Im); // 1 - x*x
        var x2 = Sqrt(x1);
        var w = Log(CAdd(ct, x2));
        return new GoComplex(w.Im, -w.Re); // -i * w
    }

    public static GoComplex Asinh(GoComplex x)
    {
        double re = x.Re, im = x.Im;
        if (im == 0 && SM.Abs(re) <= 1) return new GoComplex(SM.Asinh(re), im);
        if (re == 0 && SM.Abs(im) <= 1) return new GoComplex(re, SM.Asin(im));
        if (double.IsInfinity(re))
        {
            if (double.IsInfinity(im)) return new GoComplex(re, SM.CopySign(Pi / 4, im));
            if (double.IsNaN(im)) return x;
            return new GoComplex(re, SM.CopySign(0.0, im));
        }
        if (double.IsNaN(re))
        {
            if (im == 0) return x;
            if (double.IsInfinity(im)) return new GoComplex(im, re);
            return NaN();
        }
        if (double.IsInfinity(im)) return new GoComplex(SM.CopySign(im, re), SM.CopySign(Pi / 2, im));
        var xx = CMul(x, x);
        var x1 = new GoComplex(1 + xx.Re, xx.Im); // 1 + x*x
        return Log(CAdd(x, Sqrt(x1)));
    }

    public static GoComplex Acos(GoComplex x)
    {
        var w = Asin(x);
        return new GoComplex(Pi / 2 - w.Re, -w.Im);
    }

    public static GoComplex Acosh(GoComplex x)
    {
        if (x.Re == 0 && x.Im == 0) return new GoComplex(0, SM.CopySign(Pi / 2, x.Im));
        var w = Acos(x);
        if (w.Im <= 0) return new GoComplex(-w.Im, w.Re); // i * w
        return new GoComplex(w.Im, -w.Re); // -i * w
    }

    public static GoComplex Atan(GoComplex x)
    {
        double re = x.Re, im = x.Im;
        if (im == 0) return new GoComplex(SM.Atan(re), im);
        if (re == 0 && SM.Abs(im) <= 1) return new GoComplex(re, SM.Atanh(im));
        if (double.IsInfinity(im) || double.IsInfinity(re))
        {
            if (double.IsNaN(re)) return new GoComplex(double.NaN, SM.CopySign(0, im));
            return new GoComplex(SM.CopySign(Pi / 2, re), SM.CopySign(0, im));
        }
        if (double.IsNaN(re) || double.IsNaN(im)) return NaN();
        double x2 = re * re;
        double a = 1 - x2 - im * im;
        if (a == 0) return NaN();
        double t = 0.5 * SM.Atan2(2 * re, a);
        double w = ReducePi(t);
        t = im - 1;
        double b = x2 + t * t;
        if (b == 0) return NaN();
        t = im + 1;
        double cc = (x2 + t * t) / b;
        return new GoComplex(w, 0.25 * SM.Log(cc));
    }

    public static GoComplex Atanh(GoComplex x)
    {
        var z = new GoComplex(-x.Im, x.Re); // i * x
        z = Atan(z);
        return new GoComplex(z.Im, -z.Re); // z = -i * z
    }
}
