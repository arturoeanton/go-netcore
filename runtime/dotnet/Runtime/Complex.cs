namespace GoCLR.Runtime;

/// <summary>
/// GoComplex is a Go complex number (complex128/complex64) as a reference type
/// holding the real and imaginary parts as doubles. complex64 is stored at double
/// precision; the narrower type is rare and only loses precision at the edges.
/// </summary>
public sealed class GoComplex
{
    public double Re;
    public double Im;

    public GoComplex(double re, double im) { Re = re; Im = im; }

    public override string ToString()
    {
        // Go prints complex values as "(re+imi)" / "(re-imi)". This is best-effort
        // (the exact float formatting differs); programs comparing output convert
        // real()/imag() to int, as with plain floats.
        var c = System.Globalization.CultureInfo.InvariantCulture;
        string sign = Im < 0 ? "-" : "+";
        return "(" + Re.ToString("g", c) + sign + System.Math.Abs(Im).ToString("g", c) + "i)";
    }
}

/// <summary>Complex operations the compiler calls into.</summary>
public static class GoComplexs
{
    public static GoComplex Make(double re, double im) => new(re, im);

    public static GoComplex Add(GoComplex a, GoComplex b) => new(a.Re + b.Re, a.Im + b.Im);
    public static GoComplex Sub(GoComplex a, GoComplex b) => new(a.Re - b.Re, a.Im - b.Im);

    public static GoComplex Mul(GoComplex a, GoComplex b) =>
        new(a.Re * b.Re - a.Im * b.Im, a.Re * b.Im + a.Im * b.Re);

    public static GoComplex Div(GoComplex a, GoComplex b)
    {
        double d = b.Re * b.Re + b.Im * b.Im;
        return new((a.Re * b.Re + a.Im * b.Im) / d, (a.Im * b.Re - a.Re * b.Im) / d);
    }

    public static GoComplex Neg(GoComplex a) => new(-a.Re, -a.Im);

    public static bool Eq(GoComplex a, GoComplex b) => a.Re == b.Re && a.Im == b.Im;

    public static double Real(GoComplex a) => a.Re;
    public static double Imag(GoComplex a) => a.Im;
}
