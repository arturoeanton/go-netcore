namespace GoCLR.Runtime;

using System.Globalization;

/// <summary>Go-compatible float formatting. Go's %v/%g shortest form uses the
/// minimal round-trippable digits (which .NET's "R" also produces) but its own
/// layout rule: exponent form when the decimal exponent is &lt; -4 or &gt;= 6,
/// fixed form otherwise (lowercase 'e', signed exponent, &gt;= 2 exponent digits).</summary>
public static class GoFtoa
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>strconv.FormatFloat(d, 'g', -1, 64) — the format fmt's %v/%g use.</summary>
    public static string Shortest(double d)
    {
        if (double.IsNaN(d)) return "NaN";
        if (double.IsPositiveInfinity(d)) return "+Inf";
        if (double.IsNegativeInfinity(d)) return "-Inf";
        if (!ShortestDigits(d, out bool neg, out string digits, out int dp)) return neg ? "-0" : "0";
        int exp = dp - 1;
        string body = (exp < -4 || exp >= 6) ? FmtE(digits, exp) : FmtF(digits, dp);
        return neg ? "-" + body : body;
    }

    /// <summary>strconv.FormatFloat(f, 'g', -1, 32) — the shortest form fmt's %v/%g use
    /// for a float32. A float32 has fewer significant digits than a float64, so it must
    /// be formatted from its own 32-bit shortest round-trip (.NET float "R"), not by
    /// widening to double (which would print the spurious tail, e.g. 0.10000000149011612).</summary>
    public static string Shortest(float f)
    {
        if (float.IsNaN(f)) return "NaN";
        if (float.IsPositiveInfinity(f)) return "+Inf";
        if (float.IsNegativeInfinity(f)) return "-Inf";
        bool neg = float.IsNegative(f);
        if (neg) f = -f;
        if (!ShortestDigitsFromR(f.ToString("R", Inv), out string digits, out int dp)) return neg ? "-0" : "0";
        int exp = dp - 1;
        string body = (exp < -4 || exp >= 6) ? FmtE(digits, exp) : FmtF(digits, dp);
        return neg ? "-" + body : body;
    }

    /// <summary>strconv.FormatFloat(f, 'e', -1, 32) — shortest float32 exponent form.</summary>
    public static string ShortestE(float f, char e = 'e')
    {
        if (float.IsNaN(f)) return "NaN";
        if (float.IsInfinity(f)) return f < 0 ? "-Inf" : "+Inf";
        bool neg = float.IsNegative(f); if (neg) f = -f;
        if (!ShortestDigitsFromR(f.ToString("R", Inv), out string digits, out int dp)) return (neg ? "-0" : "0") + (e == 'E' ? "E+00" : "e+00");
        string body = FmtE(digits, dp - 1);
        if (e == 'E') body = body.Replace('e', 'E');
        return neg ? "-" + body : body;
    }

    /// <summary>strconv.FormatFloat(f, 'f', -1, 32) — shortest float32 fixed form.</summary>
    public static string ShortestF(float f)
    {
        if (float.IsNaN(f)) return "NaN";
        if (float.IsInfinity(f)) return f < 0 ? "-Inf" : "+Inf";
        bool neg = float.IsNegative(f); if (neg) f = -f;
        if (!ShortestDigitsFromR(f.ToString("R", Inv), out string digits, out int dp)) return neg ? "-0" : "0";
        string body = FmtF(digits, dp);
        return neg ? "-" + body : body;
    }

    /// <summary>strconv.FormatFloat(d, 'e', -1, 64) — shortest digits in exponent form.</summary>
    public static string ShortestE(double d, char e = 'e')
    {
        if (double.IsNaN(d)) return "NaN";
        if (double.IsInfinity(d)) return d < 0 ? "-Inf" : "+Inf";
        if (!ShortestDigits(d, out bool neg, out string digits, out int dp)) return (neg ? "-0" : "0") + (e == 'E' ? "E+00" : "e+00");
        string body = FmtE(digits, dp - 1);
        if (e == 'E') body = body.Replace('e', 'E');
        return neg ? "-" + body : body;
    }

    // Extract the shortest round-trippable decimal digits of |d| (.NET "R"),
    // returning the bare digit string and dp = count of digits left of the point.
    // Returns false when the value is zero.
    private static bool ShortestDigits(double d, out bool neg, out string digits, out int dp)
    {
        neg = double.IsNegative(d);
        if (neg) d = -d;
        return ShortestDigitsFromR(d.ToString("R", Inv), out digits, out dp);
    }

    // Parses a .NET round-trip ("R") numeric string of a non-negative magnitude into the
    // bare significant-digit string and dp = digit count left of the decimal point. Shared
    // by the double and float32 shortest formatters. Returns false when the value is zero.
    private static bool ShortestDigitsFromR(string r, out string digits, out int dp)
    {
        int ePos = r.IndexOfAny(new[] { 'E', 'e' });
        int exp10 = 0;
        string mant = r;
        if (ePos >= 0) { exp10 = int.Parse(r.Substring(ePos + 1), Inv); mant = r.Substring(0, ePos); }
        int dot = mant.IndexOf('.');
        if (dot < 0) { digits = mant; dp = mant.Length + exp10; }
        else { string ip = mant.Substring(0, dot), fp = mant.Substring(dot + 1); digits = ip + fp; dp = ip.Length + exp10; }
        int lead = 0;
        while (lead < digits.Length - 1 && digits[lead] == '0') { lead++; dp--; }
        digits = digits.Substring(lead);
        int end = digits.Length;
        while (end > 1 && digits[end - 1] == '0') end--;
        digits = digits.Substring(0, end);
        return digits != "0";
    }

    private static string FmtE(string digits, int exp)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(digits[0]);
        if (digits.Length > 1) sb.Append('.').Append(digits, 1, digits.Length - 1);
        sb.Append('e').Append(exp < 0 ? '-' : '+');
        string es = System.Math.Abs(exp).ToString(Inv);
        if (es.Length < 2) es = "0" + es;
        return sb.Append(es).ToString();
    }

    private static string FmtF(string digits, int dp)
    {
        int nd = digits.Length;
        if (dp <= 0) return "0." + new string('0', -dp) + digits;
        if (dp >= nd) return digits + new string('0', dp - nd);
        return digits.Substring(0, dp) + "." + digits.Substring(dp);
    }

    /// <summary>strconv.FormatFloat(d, 'f', prec, 64) — fixed notation. prec&lt;0
    /// means shortest. Go renders ±Inf/NaN as +Inf/-Inf/NaN.</summary>
    public static string FormatF(double d, int prec)
    {
        if (double.IsNaN(d)) return "NaN";
        if (double.IsInfinity(d)) return d < 0 ? "-Inf" : "+Inf";
        if (prec < 0)
        {
            // shortest fixed form: take the shortest digits and lay them out as %f.
            string g = Shortest(d);
            if (g.IndexOfAny(new[] { 'e', 'E' }) < 0) return g; // already fixed
            return d.ToString("0.#####################", Inv);
        }
        return d.ToString("F" + prec, Inv);
    }

    /// <summary>strconv.FormatFloat(d, 'g', prec, 64) with an explicit precision
    /// (significant digits). Go uses a lowercase exponent and the exp&lt;-4||exp&gt;=prec
    /// switch; .NET "G" uppercases and pads the exponent, so normalize.</summary>
    public static string FormatG(double d, int prec)
    {
        if (double.IsNaN(d)) return "NaN";
        if (double.IsInfinity(d)) return d < 0 ? "-Inf" : "+Inf";
        if (prec == 0) prec = 1;
        string s = d.ToString("G" + prec, Inv);
        int ei = s.IndexOfAny(new[] { 'e', 'E' });
        if (ei < 0) return s;
        string mant = s.Substring(0, ei);
        // .NET emits the exponent as <sign><digits> after 'E'; the digits parse to a magnitude
        // (no sign), so the sign must come from the sign char — not recomputed from the
        // magnitude (which is never negative). Mirrors FormatE.
        char sign = s[ei + 1];
        int ev = int.Parse(s.Substring(ei + 2), Inv);
        string es = ev.ToString(Inv);
        if (es.Length < 2) es = "0" + es;
        return mant + "e" + sign + es;
    }

    /// <summary>strconv.FormatFloat(d, 'e', prec, 64) — Go-style %e: lowercase 'e',
    /// signed exponent with at least two digits (.NET emits three).</summary>
    public static string FormatE(double d, int prec, char e = 'e')
    {
        if (double.IsNaN(d)) return "NaN";
        if (double.IsInfinity(d)) return d < 0 ? "-Inf" : "+Inf";
        if (prec < 0) return ShortestE(d, e);
        string s = d.ToString((e == 'E' ? "E" : "e") + prec, Inv);
        int ei = s.IndexOfAny(new[] { 'e', 'E' });
        if (ei < 0) return s;
        string mant = s.Substring(0, ei);
        char sign = s[ei + 1];
        int ev = int.Parse(s.Substring(ei + 2), Inv);
        string es = ev.ToString(Inv);
        if (es.Length < 2) es = "0" + es;
        return mant + e + sign + es;
    }
}
