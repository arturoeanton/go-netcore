namespace GoCLR.Stdlib;

using System.Globalization;
using GoCLR.Runtime;

/// <summary>A *strconv.NumError (the error type Parse* return); Err is the shared
/// ErrSyntax/ErrRange sentinel so code can compare `e.Err == strconv.ErrRange`.</summary>
public sealed class GoNumError : IGoError
{
    public string Func = "", Num = "";
    public GoError Err = null!;
    public GoString Error() => GoString.FromDotNetString($"strconv.{Func}: parsing \"{Num}\": {Err.Message}");
}

/// <summary>Shim for Go's <c>strconv</c> package. Integer/bool conversions are
/// exact; float formatting is best-effort (Go's shortest-decimal ftoa is not yet
/// ported). Parse* return a boxed (value, error) tuple.</summary>
public static class Strconv
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private const string ErrSyntax = "invalid syntax";
    private const string ErrRange = "value out of range";

    // strconv.ErrSyntax / ErrRange sentinels — shared instances so `err == strconv.ErrRange`
    // (reference comparison) works, including via NumError.Err.
    public static readonly GoError ErrSyntaxErr = new(GoString.FromDotNetString(ErrSyntax));
    public static readonly GoError ErrRangeErr = new(GoString.FromDotNetString(ErrRange));
    public static object ErrSyntaxVar() => ErrSyntaxErr;
    public static object ErrRangeVar() => ErrRangeErr;

    private static object NumError(string fn, string num, string kind)
    {
        var err = kind == ErrRange ? ErrRangeErr : ErrSyntaxErr;
        return new GoNumError { Func = fn, Num = num, Err = err };
    }

    public static object NumError_Err(object o) => ((GoNumError)o).Err;
    public static GoString NumError_Func(object o) => GoString.FromDotNetString(((GoNumError)o).Func);
    public static GoString NumError_Num(object o) => GoString.FromDotNetString(((GoNumError)o).Num);

    public static GoString Itoa(long i) => GoString.FromDotNetString(i.ToString(Inv));
    public static GoString FormatInt(long i, long b) => GoString.FromDotNetString(ToBase(i, (int)b, i < 0));

    // AppendInt appends the base-b text of i to dst ([]byte) and returns the slice.
    public static GoSlice AppendInt(GoSlice dst, long i, long b)
    {
        var s = ToBase(i, (int)b, i < 0);
        int n = dst.Len;
        var d = new object?[n + s.Length];
        for (int k = 0; k < n; k++) d[k] = dst.Data![dst.Off + k];
        for (int k = 0; k < s.Length; k++) d[n + k] = (int)(byte)s[k];
        return new GoSlice { Data = d, Off = 0, Len = n + s.Length, Cap = n + s.Length };
    }
    public static GoString FormatUint(ulong i, long b) => GoString.FromDotNetString(ToBaseU(i, (int)b));
    public static GoString FormatBool(bool v) => GoString.FromDotNetString(v ? "true" : "false");

    // CanBackquote reports whether s can be represented unchanged as a single-line
    // backquoted string (no backquote, no control char except tab, no BOM/DEL,
    // valid UTF-8).
    public static bool CanBackquote(GoString s)
    {
        var str = s.ToDotNetString();
        foreach (var r in str.EnumerateRunes())
        {
            int v = r.Value;
            if (v == 0xFEFF) return false;            // BOM
            if (r == System.Text.Rune.ReplacementChar) return false;
            if (v < ' ' && v != '\t') return false;   // control chars (tab ok)
            if (v == '`' || v == 0x7F) return false;  // backquote, DEL
        }
        return true;
    }

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

    public static GoString QuoteToASCII(GoString s)
    {
        var sb = new System.Text.StringBuilder("\"");
        foreach (var r in s.ToDotNetString().EnumerateRunes())
        {
            int cp = r.Value;
            switch (cp)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\t': sb.Append("\\t"); break;
                case '\r': sb.Append("\\r"); break;
                default:
                    if (cp >= 0x20 && cp < 0x7f) sb.Append((char)cp);
                    else if (cp < 0x10000) sb.Append("\\u").Append(cp.ToString("x4", Inv));
                    else sb.Append("\\U").Append(cp.ToString("x8", Inv));
                    break;
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
            'f' or 'F' => GoFtoa.FormatF(f, p),
            'e' => GoFtoa.FormatE(f, p),
            'E' => GoFtoa.FormatE(f, p, 'E'),
            'g' => p < 0 ? GoFtoa.Shortest(f) : GoFtoa.FormatG(f, p),
            'G' => p < 0 ? GoFtoa.Shortest(f).ToUpperInvariant() : GoFtoa.FormatG(f, p).ToUpperInvariant(),
            _ => GoFtoa.Shortest(f),
        };
        return GoString.FromDotNetString(s);
    }

    public static object?[] Atoi(GoString s)
    {
        var r = ParseSigned(s.ToDotNetString(), 10, 64, out long v, out string err);
        if (err == null) return new object?[] { v, null };
        return new object?[] { v, NumError("Atoi", s.ToDotNetString(), err) };
    }

    public static object?[] ParseInt(GoString s, long b, long bitSize)
    {
        ParseSigned(s.ToDotNetString(), (int)b, (int)(bitSize == 0 ? 64 : bitSize), out long v, out string err);
        return new object?[] { v, err == null ? null : NumError("ParseInt", s.ToDotNetString(), err) };
    }

    public static object?[] ParseUint(GoString s, long b, long bitSize)
    {
        ParseUnsignedTop(s.ToDotNetString(), (int)b, (int)(bitSize == 0 ? 64 : bitSize), out ulong v, out string err);
        return new object?[] { v, err == null ? null : NumError("ParseUint", s.ToDotNetString(), err) };
    }

    // Parse a signed integer with Go semantics: optional sign, base 0 prefix
    // detection, range clamp + ErrRange on overflow / bitSize overflow.
    private static string? ParseSigned(string s, int b, int bitSize, out long val, out string err)
    {
        val = 0; err = null!;
        if (s.Length == 0) { err = ErrSyntax; return err; }
        bool neg = false;
        string body = s;
        if (s[0] == '+' || s[0] == '-') { neg = s[0] == '-'; body = s.Substring(1); }
        if (!ParseUnsigned(body, b, out ulong u, out bool rangeErr)) { err = ErrSyntax; val = 0; return err; }
        if (bitSize <= 0 || bitSize > 64) bitSize = 64;
        ulong cutoff = bitSize == 64 ? ((ulong)1 << 63) : ((ulong)1 << (bitSize - 1));
        long maxv = bitSize == 64 ? long.MaxValue : (long)(cutoff - 1);
        long minv = bitSize == 64 ? long.MinValue : -(long)cutoff;
        if (!neg && (rangeErr || u >= cutoff)) { val = maxv; err = ErrRange; return err; }
        if (neg && (rangeErr || u > cutoff)) { val = minv; err = ErrRange; return err; }
        val = unchecked(neg ? -(long)u : (long)u);
        return null;
    }

    private static string? ParseUnsignedTop(string s, int b, int bitSize, out ulong val, out string err)
    {
        val = 0; err = null!;
        if (s.Length == 0) { err = ErrSyntax; return err; }
        if (s[0] == '+' || s[0] == '-') { err = ErrSyntax; return err; } // ParseUint rejects a sign
        if (!ParseUnsigned(s, b, out ulong u, out bool rangeErr)) { err = ErrSyntax; return err; }
        ulong max = bitSize >= 64 ? ulong.MaxValue : ((ulong)1 << bitSize) - 1;
        if (rangeErr || u > max) { val = max; err = ErrRange; return err; }
        val = u; return null;
    }

    // Core unsigned digit scan. base 0 → detect 0x/0o/0b/0 prefix and allow
    // underscores. Returns false on a syntax error; rangeErr signals overflow.
    private static bool ParseUnsigned(string s, int b, out ulong val, out bool rangeErr)
    {
        val = 0; rangeErr = false;
        bool underscoreOk = false;
        if (b == 0)
        {
            underscoreOk = true;
            if (s.Length >= 2 && s[0] == '0' && (s[1] == 'x' || s[1] == 'X')) { b = 16; s = s.Substring(2); }
            else if (s.Length >= 2 && s[0] == '0' && (s[1] == 'o' || s[1] == 'O')) { b = 8; s = s.Substring(2); }
            else if (s.Length >= 2 && s[0] == '0' && (s[1] == 'b' || s[1] == 'B')) { b = 2; s = s.Substring(2); }
            else if (s.Length >= 1 && s[0] == '0') { b = 8; }
            else b = 10;
        }
        if (b < 2 || b > 36) return false;
        bool any = false;
        bool lastUnderscore = false, prevWasDigit = false;
        foreach (char c in s)
        {
            if (c == '_')
            {
                if (!underscoreOk || !prevWasDigit) return false;
                lastUnderscore = true; prevWasDigit = false; continue;
            }
            int d;
            if (c >= '0' && c <= '9') d = c - '0';
            else if (c >= 'a' && c <= 'z') d = c - 'a' + 10;
            else if (c >= 'A' && c <= 'Z') d = c - 'A' + 10;
            else return false;
            if (d >= b) return false;
            any = true; lastUnderscore = false; prevWasDigit = true;
            ulong next = unchecked(val * (ulong)b + (ulong)d);
            if (next < val) rangeErr = true; else val = next;
        }
        if (!any || lastUnderscore) return false;
        return true;
    }

    public static object?[] ParseFloat(GoString gs, long bitSize)
    {
        string s = gs.ToDotNetString();
        string t = s.Replace("_", "");
        string low = t.ToLowerInvariant();
        string ls = low.StartsWith("+") || low.StartsWith("-") ? low.Substring(1) : low;
        double sign = low.StartsWith("-") ? -1 : 1;
        if (ls == "inf" || ls == "infinity") return new object?[] { sign * double.PositiveInfinity, null };
        if (ls == "nan") return new object?[] { double.NaN, null };
        if (ls.StartsWith("0x"))
        {
            if (TryParseHexFloat(t, out double hv)) return new object?[] { hv, null };
            return new object?[] { 0.0, NumError("ParseFloat", s, ErrSyntax) };
        }
        if (double.TryParse(t, NumberStyles.Float, Inv, out double v))
        {
            // overflow to ±Inf is ErrRange in Go (the input wasn't literally "inf")
            if (double.IsInfinity(v)) return new object?[] { v, NumError("ParseFloat", s, ErrRange) };
            return new object?[] { v, null };
        }
        return new object?[] { 0.0, NumError("ParseFloat", s, ErrSyntax) };
    }

    // Parse a Go hexadecimal float literal: 0x<hex>[.<hex>]p<dec-exp>.
    private static bool TryParseHexFloat(string s, out double result)
    {
        result = 0;
        double sign = 1;
        if (s.StartsWith("+")) s = s.Substring(1);
        else if (s.StartsWith("-")) { sign = -1; s = s.Substring(1); }
        if (!(s.StartsWith("0x") || s.StartsWith("0X"))) return false;
        s = s.Substring(2);
        int pPos = s.IndexOfAny(new[] { 'p', 'P' });
        if (pPos < 0) return false;
        string mant = s.Substring(0, pPos);
        if (!int.TryParse(s.Substring(pPos + 1), NumberStyles.AllowLeadingSign, Inv, out int exp)) return false;
        int dot = mant.IndexOf('.');
        string intPart = dot < 0 ? mant : mant.Substring(0, dot);
        string fracPart = dot < 0 ? "" : mant.Substring(dot + 1);
        double m = 0;
        foreach (char c in intPart) { int d = HexDigit(c); if (d < 0) return false; m = m * 16 + d; }
        double scale = 1.0 / 16;
        foreach (char c in fracPart) { int d = HexDigit(c); if (d < 0) return false; m += d * scale; scale /= 16; }
        result = sign * m * System.Math.Pow(2, exp);
        return true;
    }

    private static int HexDigit(char c) =>
        c >= '0' && c <= '9' ? c - '0' : c >= 'a' && c <= 'f' ? c - 'a' + 10 : c >= 'A' && c <= 'F' ? c - 'A' + 10 : -1;

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
