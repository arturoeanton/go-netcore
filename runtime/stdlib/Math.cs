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
}
