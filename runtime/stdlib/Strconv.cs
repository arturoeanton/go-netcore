namespace GoCLR.Stdlib;

using System.Globalization;
using GoCLR.Runtime;

/// <summary>A *strconv.NumError (the error type Parse* return); Err is the shared
/// ErrSyntax/ErrRange sentinel so code can compare `e.Err == strconv.ErrRange`.
/// [GoShim]-tagged and IGoWrapped so errors.As(*strconv.NumError) and errors.Is
/// (against ErrSyntax/ErrRange) work.</summary>
[GoShim("strconv.NumError")]
public sealed class GoNumError : IGoError, IGoWrapped
{
    public object? GoUnwrapped() => Err;
    public string Func = "", Num = "";
    public GoError Err = null!;
    public GoString Error() => GoString.FromDotNetString($"strconv.{Func}: parsing \"{Num}\": {Err.Message}");
}

/// <summary>Shim for Go's <c>strconv</c> package. Integer/bool conversions are
/// exact; float formatting is best-effort (Go's shortest-decimal ftoa is not yet
/// ported). Parse* return a boxed (value, error) tuple.</summary>
public static partial class Strconv
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

    private static GoSlice AppendStr(GoSlice dst, string s)
    {
        // UTF-8 encode: a non-ASCII rune (e.g. 世 in AppendQuoteRune) spans multiple bytes;
        // truncating each char to a byte would corrupt it. ASCII output is byte-identical.
        var by = System.Text.Encoding.UTF8.GetBytes(s);
        int n = dst.Len;
        var d = new object?[n + by.Length];
        for (int k = 0; k < n; k++) d[k] = dst.Data![dst.Off + k];
        for (int k = 0; k < by.Length; k++) d[n + k] = (int)by[k];
        return new GoSlice { Data = d, Off = 0, Len = n + by.Length, Cap = n + by.Length };
    }
    public static GoSlice AppendUint(GoSlice dst, ulong i, long b) => AppendStr(dst, ToBaseU(i, (int)b));
    public static GoSlice AppendBool(GoSlice dst, bool v) => AppendStr(dst, v ? "true" : "false");
    public static GoSlice AppendFloat(GoSlice dst, double f, int fmt, long prec, long bitSize) => AppendStr(dst, FormatFloat(f, fmt, prec, bitSize).ToDotNetString());
    public static GoSlice AppendQuote(GoSlice dst, GoString s) => AppendStr(dst, Quote(s).ToDotNetString());

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

    public static GoString Quote(GoString s) => QuoteWith(s, '"', false, false);
    public static GoString QuoteToASCII(GoString s) => QuoteWith(s, '"', true, false);
    public static GoString QuoteToGraphicG(GoString s) => QuoteWith(s, '"', false, true);

    // Faithful port of strconv.quoteWith / appendEscapedRune over the string's runes,
    // decoding raw bytes so an invalid UTF-8 byte becomes \xNN exactly as Go does.
    private static GoString QuoteWith(GoString s, char quote, bool asciiOnly, bool graphicOnly)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(quote);
        byte[] b = s.Bytes;
        for (int i = 0; i < b.Length;)
        {
            int r = b[i], width = 1;
            if (r >= 0x80) (r, width) = DecodeRune(b, i);
            if (width == 1 && r == 0xFFFD) // invalid byte
            {
                sb.Append("\\x").Append(LowerHex[b[i] >> 4]).Append(LowerHex[b[i] & 0xF]);
                i++;
                continue;
            }
            AppendEscapedRune(sb, r, quote, asciiOnly, graphicOnly);
            i += width;
        }
        sb.Append(quote);
        return GoString.FromDotNetString(sb.ToString());
    }
    private const string LowerHex = "0123456789abcdef";

    // utf8.DecodeRune over a byte slice at i: returns (rune, width); (0xFFFD,1) on invalid.
    private static (int, int) DecodeRune(byte[] b, int i)
    {
        byte b0 = b[i];
        int n = b0 < 0xC0 ? 1 : b0 < 0xE0 ? 2 : b0 < 0xF0 ? 3 : b0 < 0xF8 ? 4 : 1;
        if (n == 1 || i + n > b.Length) return (0xFFFD, 1);
        int r = b0 & (0x7F >> n);
        for (int k = 1; k < n; k++)
        {
            if ((b[i + k] & 0xC0) != 0x80) return (0xFFFD, 1);
            r = (r << 6) | (b[i + k] & 0x3F);
        }
        // Reject overlong/surrogate/out-of-range per utf8.DecodeRune.
        int[] mins = { 0, 0, 0x80, 0x800, 0x10000 };
        if (r < mins[n] || (r >= 0xD800 && r <= 0xDFFF) || r > 0x10FFFF) return (0xFFFD, 1);
        return (r, n);
    }

    public static GoString NumError_Error(object o) => ((GoNumError)o).Error();
    public static object? NumError_Unwrap(object o) => ((GoNumError)o).Err;

    // strconv.appendEscapedRune, ported verbatim: backslash/quote first, then IsPrint,
    // then the \a..\v shorthands, then \x / \u / \U.
    private static void AppendEscapedRune(System.Text.StringBuilder sb, int r, char quote, bool asciiOnly, bool graphicOnly)
    {
        if (r == quote || r == '\\') { sb.Append('\\').Append((char)r); return; }
        if (asciiOnly)
        {
            if (r < 0x80 && IsPrint(r)) { sb.Append((char)r); return; }
        }
        else if (IsPrint(r) || (graphicOnly && r <= 0xFFFF && Bsearch16(isGraphic, (ushort)r).found))
        {
            sb.Append(char.ConvertFromUtf32(r));
            return;
        }
        switch (r)
        {
            case 7: sb.Append("\\a"); return;
            case 8: sb.Append("\\b"); return;
            case 12: sb.Append("\\f"); return;
            case '\n': sb.Append("\\n"); return;
            case '\r': sb.Append("\\r"); return;
            case '\t': sb.Append("\\t"); return;
            case 11: sb.Append("\\v"); return;
        }
        if (r < ' ' || r == 0x7f)
            sb.Append("\\x").Append(LowerHex[(r >> 4) & 0xF]).Append(LowerHex[r & 0xF]);
        else if (r < 0x10000)
            sb.Append("\\u").Append(((uint)r).ToString("x4", Inv));
        else
        {
            if (r > 0x10FFFF) r = 0xFFFD;
            sb.Append("\\U").Append(((uint)r).ToString("x8", Inv));
        }
    }
    public static GoString QuoteRune(int r) { var sb = new System.Text.StringBuilder("'"); AppendEscapedRune(sb, r, '\'', false, false); return GoString.FromDotNetString(sb.Append('\'').ToString()); }
    public static GoString QuoteRuneToASCII(int r) { var sb = new System.Text.StringBuilder("'"); AppendEscapedRune(sb, r, '\'', true, false); return GoString.FromDotNetString(sb.Append('\'').ToString()); }
    public static GoString QuoteRuneToGraphic(int r) { var sb = new System.Text.StringBuilder("'"); AppendEscapedRune(sb, r, '\'', false, true); return GoString.FromDotNetString(sb.Append('\'').ToString()); }
    public static GoString QuoteToGraphic(GoString s) => QuoteToGraphicG(s);
    public static GoSlice AppendQuoteToASCII(GoSlice dst, GoString s) => AppendStr(dst, QuoteToASCII(s).ToDotNetString());
    public static GoSlice AppendQuoteToGraphic(GoSlice dst, GoString s) => AppendStr(dst, QuoteToGraphic(s).ToDotNetString());
    public static GoSlice AppendQuoteRune(GoSlice dst, int r) => AppendStr(dst, QuoteRune(r).ToDotNetString());
    public static GoSlice AppendQuoteRuneToASCII(GoSlice dst, int r) => AppendStr(dst, QuoteRuneToASCII(r).ToDotNetString());
    public static GoSlice AppendQuoteRuneToGraphic(GoSlice dst, int r) => AppendStr(dst, QuoteRuneToGraphic(r).ToDotNetString());

    // UnquoteChar decodes the first char in the escaped string literal s (quote is the
    // surrounding delimiter, '"' or '\''). Returns (value, multibyte, tail, err).
    public static object?[] UnquoteChar(GoString gs, int quote)
    {
        string s = gs.ToDotNetString();
        var (value, multibyte, tail, ok) = UnquoteCharImpl(s, (char)quote);
        return new object?[] { value, multibyte, GoString.FromDotNetString(tail), ok ? null : ErrSyntaxErr };
    }
    private static (int value, bool multibyte, string tail, bool ok) UnquoteCharImpl(string s, char quote)
    {
        if (s.Length == 0) return (0, false, s, false);
        char c = s[0];
        if ((c == quote && (quote == '\'' || quote == '"')) || c == '\n') return (0, false, s, false);
        if (c >= 0x80) { int cp = char.ConvertToUtf32(s, 0); return (cp, true, s.Substring(char.ConvertFromUtf32(cp).Length), true); }
        if (c != '\\') return (c, false, s.Substring(1), true);
        if (s.Length < 2) return (0, false, s, false);
        char e = s[1];
        string rest = s.Substring(2);
        switch (e)
        {
            case 'a': return (7, false, rest, true);
            case 'b': return (8, false, rest, true);
            case 'f': return (12, false, rest, true);
            case 'n': return (10, false, rest, true);
            case 'r': return (13, false, rest, true);
            case 't': return (9, false, rest, true);
            case 'v': return (11, false, rest, true);
            case '\\': return ('\\', false, rest, true);
            case '\'': return ('\'', false, rest, true);
            case '"': return ('"', false, rest, true);
            case 'x':
            case 'u':
            case 'U':
            {
                int n = e == 'x' ? 2 : e == 'u' ? 4 : 8;
                if (rest.Length < n) return (0, false, s, false);
                int v = 0;
                for (int i = 0; i < n; i++) { int d = HexVal(rest[i]); if (d < 0) return (0, false, s, false); v = v * 16 + d; }
                if (e != 'x' && v > 0x10FFFF) return (0, false, s, false);
                return (v, e != 'x', rest.Substring(n), true);
            }
            case '0': case '1': case '2': case '3': case '4': case '5': case '6': case '7':
            {
                if (s.Length < 4) return (0, false, s, false);
                int v = 0;
                for (int i = 1; i < 4; i++) { int d = s[i] - '0'; if (d < 0 || d > 7) return (0, false, s, false); v = v * 8 + d; }
                if (v > 255) return (0, false, s, false);
                return (v, false, s.Substring(4), true);
            }
        }
        return (0, false, s, false);
    }
    private static int HexVal(char c) => c >= '0' && c <= '9' ? c - '0' : c >= 'a' && c <= 'f' ? c - 'a' + 10 : c >= 'A' && c <= 'F' ? c - 'A' + 10 : -1;

    public static object?[] Unquote(GoString gs)
    {
        string s = gs.ToDotNetString();
        if (s.Length < 2) return new object?[] { GoString.FromDotNetString(""), ErrSyntaxErr };
        char q = s[0];
        if (s[s.Length - 1] != q) return new object?[] { GoString.FromDotNetString(""), ErrSyntaxErr };
        string inner = s.Substring(1, s.Length - 2);
        if (q == '`')
        {
            if (inner.Contains('`') || inner.Contains('\r')) return new object?[] { GoString.FromDotNetString(""), ErrSyntaxErr };
            return new object?[] { GoString.FromDotNetString(inner), null };
        }
        if (q != '"' && q != '\'') return new object?[] { GoString.FromDotNetString(""), ErrSyntaxErr };
        var bytes = new System.Collections.Generic.List<byte>();
        string cur = inner;
        int runes = 0;
        while (cur.Length > 0)
        {
            var (value, multibyte, tail, ok) = UnquoteCharImpl(cur, q);
            if (!ok) return new object?[] { GoString.FromDotNetString(""), ErrSyntaxErr };
            // Go appends a single byte for ASCII and \x escapes (multibyte == false), and the
            // rune's UTF-8 otherwise — so `\xc3\xa9` yields the two bytes of "é", not U+00C3 U+00A9.
            if (value < 0x80 || !multibyte) bytes.Add((byte)value);
            else bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(char.ConvertFromUtf32(value)));
            cur = tail;
            runes++;
        }
        // A single-quoted literal holds at most one rune; Go accepts the empty form '' (-> "")
        // but rejects two or more characters.
        if (q == '\'' && runes > 1) return new object?[] { GoString.FromDotNetString(""), ErrSyntaxErr };
        return new object?[] { GoString.FromBytesOwned(bytes.ToArray()), null };
    }
    // QuotedPrefix returns the quoted string (including delimiters) at the start of s.
    public static object?[] QuotedPrefix(GoString gs)
    {
        string s = gs.ToDotNetString();
        if (s.Length == 0) return new object?[] { GoString.FromDotNetString(""), ErrSyntaxErr };
        char q = s[0];
        if (q == '`')
        {
            int end = s.IndexOf('`', 1);
            if (end < 0) return new object?[] { GoString.FromDotNetString(""), ErrSyntaxErr };
            return new object?[] { GoString.FromDotNetString(s.Substring(0, end + 1)), null };
        }
        if (q != '"' && q != '\'') return new object?[] { GoString.FromDotNetString(""), ErrSyntaxErr };
        string cur = s.Substring(1);
        while (cur.Length > 0 && cur[0] != q)
        {
            var (_, _, tail, ok) = UnquoteCharImpl(cur, q);
            if (!ok) return new object?[] { GoString.FromDotNetString(""), ErrSyntaxErr };
            cur = tail;
        }
        if (cur.Length == 0) return new object?[] { GoString.FromDotNetString(""), ErrSyntaxErr };
        return new object?[] { GoString.FromDotNetString(s.Substring(0, s.Length - cur.Length + 1)), null };
    }

    // FormatComplex renders (re±imi), each part via FormatFloat at half the bit size.
    public static GoString FormatComplex(GoComplex c, int fmt, long prec, long bitSize)
    {
        long fb = bitSize == 0 ? 128 : bitSize;
        string re = FormatFloat(c.Re, fmt, prec, fb / 2).ToDotNetString();
        string im = FormatFloat(c.Im, fmt, prec, fb / 2).ToDotNetString();
        string sign = im.Length > 0 && (im[0] == '+' || im[0] == '-') ? "" : "+";
        return GoString.FromDotNetString("(" + re + sign + im + "i)");
    }
    public static object?[] ParseComplex(GoString gs, long bitSize)
    {
        string s = gs.ToDotNetString();
        string orig = s;
        if (s.Length >= 2 && s[0] == '(' && s[s.Length - 1] == ')') s = s.Substring(1, s.Length - 2);
        long fb = bitSize == 64 ? 32 : 64;
        // Pure imaginary "Ni" or a real, or "re±imi".
        if (s.EndsWith("i"))
        {
            string body = s.Substring(0, s.Length - 1);
            int split = -1;
            for (int i = 1; i < body.Length; i++)
                if ((body[i] == '+' || body[i] == '-') && body[i - 1] != 'e' && body[i - 1] != 'E') split = i;
            if (split < 0)
            {
                string imOnly = body.Length == 0 || body == "+" ? "1" : body == "-" ? "-1" : body;
                var pi = ParseFloatRaw(imOnly, fb);
                if (pi == null) return new object?[] { new GoComplex(0, 0), NumError("ParseComplex", orig, ErrSyntax) };
                return new object?[] { new GoComplex(0, pi.Value), null };
            }
            string reP = body.Substring(0, split);
            string imP = body.Substring(split);
            if (imP == "+") imP = "1"; else if (imP == "-") imP = "-1";
            var re = ParseFloatRaw(reP, fb);
            var im = ParseFloatRaw(imP, fb);
            if (re == null || im == null) return new object?[] { new GoComplex(0, 0), NumError("ParseComplex", orig, ErrSyntax) };
            return new object?[] { new GoComplex(re.Value, im.Value), null };
        }
        var r = ParseFloatRaw(s, fb);
        if (r == null) return new object?[] { new GoComplex(0, 0), NumError("ParseComplex", orig, ErrSyntax) };
        return new object?[] { new GoComplex(r.Value, 0), null };
    }
    private static double? ParseFloatRaw(string s, long bitSize)
    {
        var res = ParseFloat(GoString.FromDotNetString(s), bitSize);
        return res[1] == null ? (double)res[0]! : (double?)null;
    }

    public static GoString FormatFloat(double f, int fmt, long prec, long bitSize)
    {
        char c = (char)fmt;
        int p = (int)prec;
        // For the SHORTEST forms (prec < 0) a 32-bit size must use the float32 round-trip
        // (fewer digits), else the spurious float64 tail prints (e.g. 0.10000000149011612 for
        // a float32 0.1). Fixed-precision forms format the value's bits regardless of bitSize.
        bool f32 = bitSize == 32 && p < 0;
        string s = c switch
        {
            'f' or 'F' => p < 0 ? (f32 ? GoFtoa.ShortestF((float)f) : GoFtoa.ShortestF(f)) : GoFtoa.FormatF(f, p),
            'e' => f32 ? GoFtoa.ShortestE((float)f) : GoFtoa.FormatE(f, p),
            'E' => f32 ? GoFtoa.ShortestE((float)f, 'E') : GoFtoa.FormatE(f, p, 'E'),
            'g' => p < 0 ? (f32 ? GoFtoa.Shortest((float)f) : GoFtoa.Shortest(f)) : GoFtoa.FormatG(f, p),
            'G' => p < 0 ? (f32 ? GoFtoa.Shortest((float)f) : GoFtoa.Shortest(f)).ToUpperInvariant() : GoFtoa.FormatG(f, p).ToUpperInvariant(),
            'b' => FormatB(f, (int)bitSize),
            'x' => FormatX(f, p, false, (int)bitSize),
            'X' => FormatX(f, p, true, (int)bitSize),
            _ => f32 ? GoFtoa.Shortest((float)f) : GoFtoa.Shortest(f),
        };
        return GoString.FromDotNetString(s);
    }

    // strconv 'b' format: the exact value as mantissa × 2^exp, printed "dddp±ddd" (no
    // rounding). The mantissa is the full significand as an integer (implicit bit added for
    // normals) and exp = unbiasedExp - mantbits. NaN/±Inf print like every other format.
    private static string FormatB(double f, int bitSize)
    {
        if (double.IsNaN(f)) return "NaN";
        if (double.IsPositiveInfinity(f)) return "+Inf";
        if (double.IsNegativeInfinity(f)) return "-Inf";
        bool neg;
        ulong mant;
        int exp;
        if (bitSize == 32)
        {
            uint bits = (uint)BitConverter.SingleToInt32Bits((float)f);
            neg = (bits >> 31) != 0;
            uint m = bits & ((1u << 23) - 1);
            int be = (int)((bits >> 23) & 0xFF);
            if (be == 0) { exp = 1 - 127; mant = m; }
            else { mant = m | (1u << 23); exp = be - 127; }
            exp -= 23;
        }
        else
        {
            ulong bits = (ulong)BitConverter.DoubleToInt64Bits(f);
            neg = (bits >> 63) != 0;
            ulong m = bits & (((ulong)1 << 52) - 1);
            int be = (int)((bits >> 52) & 0x7FF);
            if (be == 0) { exp = 1 - 1023; mant = m; }
            else { mant = m | ((ulong)1 << 52); exp = be - 1023; }
            exp -= 52;
        }
        var sb = new System.Text.StringBuilder();
        if (neg) sb.Append('-');
        sb.Append(mant.ToString(Inv)).Append('p');
        if (exp >= 0) sb.Append('+');
        sb.Append(exp.ToString(Inv));
        return sb.ToString();
    }

    // strconv 'x'/'X' format: hexadecimal floating-point "0x1.<hexfrac>p±dd". prec<0 prints
    // the shortest exact fraction; prec>=0 prints exactly that many hex digits (round to
    // even). A direct port of Go's strconv fmtX (leading 1 normalized to bit 1<<60).
    private static string FormatX(double f, int prec, bool upper, int bitSize)
    {
        int mantbits, expbits, bias;
        ulong bits;
        if (bitSize == 32) { mantbits = 23; expbits = 8; bias = -127; bits = (uint)BitConverter.SingleToInt32Bits((float)f); }
        else { mantbits = 52; expbits = 11; bias = -1023; bits = (ulong)BitConverter.DoubleToInt64Bits(f); }

        bool neg = (bits >> (expbits + mantbits)) != 0;
        int exp = (int)((bits >> mantbits) & (((ulong)1 << expbits) - 1));
        ulong mant = bits & (((ulong)1 << mantbits) - 1);

        if (exp == (1 << expbits) - 1) return mant != 0 ? "NaN" : (neg ? "-Inf" : "+Inf");
        if (exp == 0) exp++;                       // denormalized
        else mant |= (ulong)1 << mantbits;         // add implicit top bit
        exp += bias;

        if (mant == 0) exp = 0;
        // Shift digits so leading 1 (if any) is at bit 1<<60.
        mant <<= 60 - mantbits;
        while (mant != 0 && (mant & ((ulong)1 << 60)) == 0) { mant <<= 1; exp--; }

        // Round to 1+prec hex digits if requested.
        if (prec >= 0 && prec < 15)
        {
            int shift = prec * 4;
            ulong extra = (mant << shift) & (((ulong)1 << 60) - 1);
            mant >>= 60 - shift;
            if ((extra | (mant & 1)) > ((ulong)1 << 59)) mant++;
            mant <<= 60 - shift;
            if ((mant & ((ulong)1 << 61)) != 0) { mant >>= 1; exp++; } // wrapped into the integer digit
        }

        string hex = upper ? "0123456789ABCDEF" : "0123456789abcdef";
        var sb = new System.Text.StringBuilder();
        if (neg) sb.Append('-');
        sb.Append('0').Append(upper ? 'X' : 'x').Append((char)('0' + (int)((mant >> 60) & 1)));

        mant <<= 4; // remove the leading 0 or 1
        if (prec < 0 && mant != 0)
        {
            sb.Append('.');
            while (mant != 0) { sb.Append(hex[(int)((mant >> 60) & 15)]); mant <<= 4; }
        }
        else if (prec > 0)
        {
            sb.Append('.');
            for (int i = 0; i < prec; i++) { sb.Append(hex[(int)((mant >> 60) & 15)]); mant <<= 4; }
        }

        sb.Append(upper ? 'P' : 'p');
        if (exp < 0) { sb.Append('-'); exp = -exp; } else sb.Append('+');
        if (exp < 100) sb.Append((char)(exp / 10 + '0')).Append((char)(exp % 10 + '0'));
        else if (exp < 1000) sb.Append((char)(exp / 100 + '0')).Append((char)(exp / 10 % 10 + '0')).Append((char)(exp % 10 + '0'));
        else sb.Append((char)(exp / 1000 + '0')).Append((char)(exp / 100 % 10 + '0')).Append((char)(exp / 10 % 10 + '0')).Append((char)(exp % 10 + '0'));
        return sb.ToString();
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
        bool prefixStripped = false; // a stripped 0x/0o/0b base prefix counts as a digit for a following '_'
        if (b == 0)
        {
            underscoreOk = true;
            if (s.Length >= 2 && s[0] == '0' && (s[1] == 'x' || s[1] == 'X')) { b = 16; s = s.Substring(2); prefixStripped = true; }
            else if (s.Length >= 2 && s[0] == '0' && (s[1] == 'o' || s[1] == 'O')) { b = 8; s = s.Substring(2); prefixStripped = true; }
            else if (s.Length >= 2 && s[0] == '0' && (s[1] == 'b' || s[1] == 'B')) { b = 2; s = s.Substring(2); prefixStripped = true; }
            else if (s.Length >= 1 && s[0] == '0') { b = 8; }
            else b = 10;
        }
        if (b < 2 || b > 36) return false;
        bool any = false;
        // Go allows one underscore between the base prefix and the first digit (0x_FF), so the
        // prefix is treated as the preceding digit for the very next character.
        bool lastUnderscore = false, prevWasDigit = prefixStripped;
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
            if (TryParseHexFloat(t, out double hv)) return FloatResult(hv, bitSize, s);
            return new object?[] { 0.0, NumError("ParseFloat", s, ErrSyntax) };
        }
        // Go's ParseFloat does NOT allow surrounding whitespace or thousands separators
        // (NumberStyles.Float would accept leading/trailing white) — only sign/point/exponent.
        const NumberStyles floatStyle = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent;
        if (double.TryParse(t, floatStyle, Inv, out double v))
            return FloatResult(v, bitSize, s);
        return new object?[] { 0.0, NumError("ParseFloat", s, ErrSyntax) };
    }

    // Apply bitSize: with bitSize==32 the result is rounded to float32 precision (so the
    // returned float64 reflects that rounding), and a value overflowing the float32/float64
    // range — when the input wasn't literally "inf" — is ±Inf with ErrRange (as in Go).
    private static object?[] FloatResult(double v, long bitSize, string s)
    {
        if (bitSize == 32)
        {
            float f = (float)v;
            if (float.IsInfinity(f) && !double.IsInfinity(v))
                return new object?[] { (double)f, NumError("ParseFloat", s, ErrRange) };
            return new object?[] { (double)f, null };
        }
        if (double.IsInfinity(v)) return new object?[] { v, NumError("ParseFloat", s, ErrRange) };
        return new object?[] { v, null };
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
