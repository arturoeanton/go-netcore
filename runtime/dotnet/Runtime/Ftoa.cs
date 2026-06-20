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

        bool neg = double.IsNegative(d);
        if (neg) d = -d;

        // Shortest round-trippable digits from .NET, parsed into a digit string
        // plus dp = count of digits left of the decimal point in the significand.
        string r = d.ToString("R", Inv);
        int ePos = r.IndexOfAny(new[] { 'E', 'e' });
        int exp10 = 0;
        string mant = r;
        if (ePos >= 0) { exp10 = int.Parse(r.Substring(ePos + 1), Inv); mant = r.Substring(0, ePos); }

        int dot = mant.IndexOf('.');
        string digits;
        int dp;
        if (dot < 0) { digits = mant; dp = mant.Length + exp10; }
        else
        {
            string ip = mant.Substring(0, dot), fp = mant.Substring(dot + 1);
            digits = ip + fp;
            dp = ip.Length + exp10;
        }

        int lead = 0;
        while (lead < digits.Length - 1 && digits[lead] == '0') { lead++; dp--; }
        digits = digits.Substring(lead);
        int end = digits.Length;
        while (end > 1 && digits[end - 1] == '0') end--;
        digits = digits.Substring(0, end);

        if (digits == "0") return neg ? "-0" : "0";

        int exp = dp - 1;
        string body = (exp < -4 || exp >= 6) ? FmtE(digits, exp) : FmtF(digits, dp);
        return neg ? "-" + body : body;
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

    /// <summary>strconv.FormatFloat(d, 'e', prec, 64) — Go-style %e: lowercase 'e',
    /// signed exponent with at least two digits (.NET emits three).</summary>
    public static string FormatE(double d, int prec, char e = 'e')
    {
        if (double.IsNaN(d)) return "NaN";
        if (double.IsInfinity(d)) return d < 0 ? "-Inf" : "+Inf";
        string s = d.ToString((e == 'E' ? "E" : "e") + (prec < 0 ? 6 : prec), Inv);
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
