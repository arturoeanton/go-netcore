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
}
