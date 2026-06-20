namespace GoCLR.Stdlib;

using System.Globalization;
using GoCLR.Runtime;

/// <summary>Shim for Go's <c>strconv</c> package. Integer/bool conversions are
/// exact; float formatting is best-effort (Go's shortest-decimal ftoa is not yet
/// ported). Parse* return a boxed (value, error) tuple.</summary>
public static class Strconv
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static object NumError(string fn, string num, string kind) =>
        new GoError(GoString.FromDotNetString($"strconv.{fn}: parsing \"{num}\": {kind}"));

    public static GoString Itoa(long i) => GoString.FromDotNetString(i.ToString(Inv));
    public static GoString FormatInt(long i, long b) => GoString.FromDotNetString(ToBase(i, (int)b, i < 0));
    public static GoString FormatUint(ulong i, long b) => GoString.FromDotNetString(ToBaseU(i, (int)b));
    public static GoString FormatBool(bool v) => GoString.FromDotNetString(v ? "true" : "false");

    public static GoString Quote(GoString s)
    {
        var sb = new System.Text.StringBuilder("\"");
        foreach (char c in s.ToDotNetString())
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\t': sb.Append("\\t"); break;
                case '\r': sb.Append("\\r"); break;
                default: sb.Append(c); break;
            }
        }
        sb.Append('"');
        return GoString.FromDotNetString(sb.ToString());
    }

    public static GoString FormatFloat(double f, int fmt, long prec, long bitSize)
    {
        char c = (char)fmt;
        int p = (int)prec;
        string s = c switch
        {
            'f' => p < 0 ? f.ToString("0.0###############", Inv) : f.ToString("F" + p, Inv),
            'e' => GoFtoa.FormatE(f, p),
            'g' => p < 0 ? GoFtoa.Shortest(f) : f.ToString("G" + p, Inv),
            _ => f.ToString(Inv),
        };
        return GoString.FromDotNetString(s);
    }

    public static object?[] Atoi(GoString s)
    {
        if (long.TryParse(s.ToDotNetString(), NumberStyles.AllowLeadingSign, Inv, out long v))
            return new object?[] { v, null };
        return new object?[] { 0L, NumError("Atoi", s.ToDotNetString(), "invalid syntax") };
    }

    public static object?[] ParseInt(GoString s, long b, long bitSize)
    {
        try { return new object?[] { System.Convert.ToInt64(s.ToDotNetString(), b == 0 ? 10 : (int)b), null }; }
        catch { return new object?[] { 0L, NumError("ParseInt", s.ToDotNetString(), "invalid syntax") }; }
    }

    public static object?[] ParseUint(GoString s, long b, long bitSize)
    {
        try { return new object?[] { (ulong)System.Convert.ToUInt64(s.ToDotNetString(), b == 0 ? 10 : (int)b), null }; }
        catch { return new object?[] { (ulong)0, NumError("ParseUint", s.ToDotNetString(), "invalid syntax") }; }
    }

    public static object?[] ParseFloat(GoString s, long bitSize)
    {
        if (double.TryParse(s.ToDotNetString(), NumberStyles.Float, Inv, out double v))
            return new object?[] { v, null };
        return new object?[] { 0.0, NumError("ParseFloat", s.ToDotNetString(), "invalid syntax") };
    }

    public static object?[] ParseBool(GoString s)
    {
        switch (s.ToDotNetString())
        {
            case "1": case "t": case "T": case "TRUE": case "true": case "True":
                return new object?[] { true, null };
            case "0": case "f": case "F": case "FALSE": case "false": case "False":
                return new object?[] { false, null };
            default:
                return new object?[] { false, NumError("ParseBool", s.ToDotNetString(), "invalid syntax") };
        }
    }

    private static string ToBase(long v, int b, bool neg)
    {
        if (b == 10) return v.ToString(Inv);
        ulong u = neg ? (ulong)(-v) : (ulong)v;
        return (neg ? "-" : "") + ToBaseU(u, b);
    }
    private static string ToBaseU(ulong v, int b)
    {
        if (v == 0) return "0";
        const string digits = "0123456789abcdefghijklmnopqrstuvwxyz";
        var sb = new System.Text.StringBuilder();
        while (v > 0) { sb.Insert(0, digits[(int)(v % (ulong)b)]); v /= (ulong)b; }
        return sb.ToString();
    }
}
