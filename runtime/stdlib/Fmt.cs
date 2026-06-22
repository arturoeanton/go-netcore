namespace GoCLR.Stdlib;

using System.Reflection;
using System.Text;
using System.Globalization;
using GoCLR.Runtime;

/// <summary>Shim for a subset of Go's <c>fmt</c> package. %v formatting uses .NET
/// reflection over boxed values, mirroring Go's default formatting.</summary>
public static class Fmt
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // Stringer/Error dispatch tables, populated at startup by the compiler. A value
    // receiver's method is keyed by the struct's CLR type name; a pointer receiver's
    // (and a value receiver reached through *T) by the struct's runtime type id.
    private static readonly System.Collections.Generic.Dictionary<string, GoClosure> _valStringers = new();
    private static readonly System.Collections.Generic.Dictionary<long, GoClosure> _ptrStringers = new();
    // Named non-struct types (the typed box): keyed by GoNamed type id.
    private static readonly System.Collections.Generic.Dictionary<long, GoClosure> _namedStringers = new();

    public static void RegisterValStringer(GoString name, GoClosure fn) => _valStringers[name.ToDotNetString()] = fn;
    public static void RegisterPtrStringer(long id, GoClosure fn) => _ptrStringers[id] = fn;
    public static void RegisterNamedStringer(long id, GoClosure fn) => _namedStringers[id] = fn;

    // TryStringer invokes a registered String()/Error() method for v's concrete type,
    // returning its text. Used for the %v and %s verbs (not %#v / %T / %d / %p).
    private static bool TryStringer(object? v, out string s)
    {
        s = "";
        // reflect's well-known types format via their String() shim (like error).
        if (v is GoReflectType rt) { s = Reflect.Type_String(rt).ToDotNetString(); return true; }
        if (v is GoSignal sg) { s = sg.Name; return true; }
        if (v is GoNamed kn && Rt.NamedTypeName(kn.TypeId) == "reflect.Kind")
        { s = GoKind.Name((int)System.Convert.ToInt64(kn.Value ?? 0L)); return true; }
        GoClosure? fn = null;
        if (v is GoNamed nm)
            _namedStringers.TryGetValue(nm.TypeId, out fn);
        else if (v is GoPtr p && p.TypeId != 0)
            _ptrStringers.TryGetValue(p.TypeId, out fn);
        else if (v != null && v.GetType().IsValueType && IsStructVal(v))
            _valStringers.TryGetValue(v.GetType().Name, out fn);
        if (fn == null) return false;
        s = GoRuntime.InvokeArgs(fn, v) is GoString gs ? gs.ToDotNetString() : "";
        return true;
    }

    private static object?[] Args(GoSlice a)
    {
        var r = new object?[a.Len];
        for (int i = 0; i < a.Len; i++) r[i] = a.Data[a.Off + i];
        return r;
    }
    private static bool IsString(object? v) => v is GoString;

    public static GoString Sprint(GoSlice args)
    {
        var a = Args(args);
        var sb = new StringBuilder();
        for (int i = 0; i < a.Length; i++)
        {
            if (i > 0 && !IsString(a[i - 1]) && !IsString(a[i])) sb.Append(' ');
            sb.Append(Format(a[i], 'v', false, false));
        }
        return GoString.FromDotNetString(sb.ToString());
    }

    public static GoString Sprintln(GoSlice args)
    {
        var a = Args(args);
        var sb = new StringBuilder();
        for (int i = 0; i < a.Length; i++) { if (i > 0) sb.Append(' '); sb.Append(Format(a[i], 'v', false, false)); }
        sb.Append('\n');
        return GoString.FromDotNetString(sb.ToString());
    }

    public static GoString Sprintf(GoString format, GoSlice args) =>
        GoString.FromDotNetString(DoSprintf(format.ToDotNetString(), Args(args)));

    private static void Out(string s) { System.Console.Out.Write(s); System.Console.Out.Flush(); }
    public static object?[] Print(GoSlice args) { var s = Sprint(args); Out(s.ToDotNetString()); return new object?[] { (long)s.Len, null }; }
    public static object?[] Println(GoSlice args) { var s = Sprintln(args); Out(s.ToDotNetString()); return new object?[] { (long)s.Len, null }; }
    public static object?[] Printf(GoString format, GoSlice args) { var s = DoSprintf(format.ToDotNetString(), Args(args)); Out(s); return new object?[] { (long)Encoding.UTF8.GetByteCount(s), null }; }

    public static object Errorf(GoString format, GoSlice args)
    {
        var a = Args(args);
        string f = format.ToDotNetString();
        string msg = DoSprintf(f, a);
        object? wrapped = FindWrapArg(f, a);
        return new GoError(GoString.FromDotNetString(msg), wrapped);
    }

    // Locate the argument consumed by a %w verb (the wrapped error), mirroring the
    // argument-advance rules of DoSprintf.
    private static object? FindWrapArg(string f, object?[] args)
    {
        int ai = 0;
        for (int i = 0; i < f.Length; i++)
        {
            if (f[i] != '%') continue;
            i++;
            if (i >= f.Length) break;
            while (i < f.Length && "+-# 0".IndexOf(f[i]) >= 0) i++;
            while (i < f.Length && (char.IsDigit(f[i]) || f[i] == '*')) { if (f[i] == '*') ai++; i++; }
            if (i < f.Length && f[i] == '.') { i++; while (i < f.Length && (char.IsDigit(f[i]) || f[i] == '*')) { if (f[i] == '*') ai++; i++; } }
            if (i >= f.Length) break;
            char verb = f[i];
            if (verb == '%') continue;
            if (verb == 'w') return ai < args.Length ? args[ai] : null;
            ai++;
        }
        return null;
    }

    /// <summary>Write a string to an io.Writer the runtime understands (a buffer,
    /// builder, or stdout/stderr); returns the byte count.</summary>
    internal static long WriteTo(object? w, string s)
    {
        long n = Encoding.UTF8.GetByteCount(s);
        // A known sink passed directly: write to it.
        switch (w)
        {
            case null: Out(s); return n;
            case GoStringBuilder sb: sb.SB.Append(s); return n;
            case GoBuffer buf: foreach (byte b in Encoding.UTF8.GetBytes(s)) buf.B.Add(b); return n;
            case GoFile f when f.Wr != null: { var b = Encoding.UTF8.GetBytes(s); f.Wr.Write(b, 0, b.Length); return n; }
            case GoFile f when f.IsStderr: System.Console.Error.Write(s); System.Console.Error.Flush(); return n;
            case GoFile: Out(s); return n;
            case GoRespWriter rw: { var b = Encoding.UTF8.GetBytes(s); rw.Body.Write(b, 0, b.Length); return n; }
        }
        // A user io.Writer wrapper (echo.Response, gin's responseWriter, a gzip/bufio
        // writer): drive its OWN Write through the callback bridge so its semantics run —
        // echo.Response.Write fires WriteHeader-on-first-write (committing a non-200 status
        // correctly), a compressing writer compresses — instead of punching straight through
        // to the underlying sink and losing all of it.
        if (Bridge.HasMethod(w, "Write"))
        {
            Bridge.CallMethod(w, "Write", GoStrings.ToByteSlice(GoString.FromDotNetString(s)));
            return n;
        }
        // Fallback: a wrapper with no generated Write adapter (e.g. a named non-struct
        // writer) — navigate its fields to an underlying known sink.
        switch (ResolveSink(w, 0))
        {
            case GoStringBuilder sb: sb.SB.Append(s); break;
            case GoBuffer buf: foreach (byte b in Encoding.UTF8.GetBytes(s)) buf.B.Add(b); break;
            case GoFile f when f.Wr != null: { var b = Encoding.UTF8.GetBytes(s); f.Wr.Write(b, 0, b.Length); break; }
            case GoFile f when f.IsStderr: System.Console.Error.Write(s); System.Console.Error.Flush(); break;
            case GoRespWriter rw: { var b = Encoding.UTF8.GetBytes(s); rw.Body.Write(b, 0, b.Length); break; }
            default: Out(s); break;
        }
        return n;
    }

    // Resolve an io.Writer to a sink the runtime can write to, navigating user wrapper
    // types (a struct/pointer embedding another writer) to the underlying sink. Only a
    // fallback now: an io.Writer with a generated Write adapter is driven through the
    // callback bridge instead (so its own Write semantics run).
    private static object? ResolveSink(object? w, int depth)
    {
        switch (w)
        {
            case null: return null;
            case GoStringBuilder: case GoBuffer: case GoRespWriter: return w;
            case GoFile: return w;
        }
        if (depth >= 8) return w;
        if (w is GoPtr p) return ResolveSink(GoPtrs.Get(p), depth + 1);
        foreach (var f in w.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            var fv = f.GetValue(w);
            switch (fv)
            {
                case GoRespWriter: case GoBuffer: case GoStringBuilder: case GoFile: return fv;
                case GoPtr:
                    var inner = ResolveSink(fv, depth + 1);
                    if (inner is GoRespWriter or GoBuffer or GoStringBuilder or GoFile) return inner;
                    break;
            }
        }
        return w;
    }

    public static object?[] Fprint(object? w, GoSlice args) { long n = WriteTo(w, Sprint(args).ToDotNetString()); return new object?[] { n, null }; }
    public static object?[] Fprintln(object? w, GoSlice args) { long n = WriteTo(w, Sprintln(args).ToDotNetString()); return new object?[] { n, null }; }
    public static object?[] Fprintf(object? w, GoString format, GoSlice args) { long n = WriteTo(w, DoSprintf(format.ToDotNetString(), Args(args))); return new object?[] { n, null }; }

    // A parsed %-verb specifier: flags, optional width/precision, and the verb.
    private struct Spec { public bool Minus, Plus, Space, Hash, Zero; public int Width, Prec; public char Verb; }

    private static string DoSprintf(string f, object?[] args)
    {
        var sb = new StringBuilder();
        int ai = 0;
        for (int i = 0; i < f.Length; i++)
        {
            if (f[i] != '%') { sb.Append(f[i]); continue; }
            i++;
            if (i >= f.Length) { sb.Append('%'); break; }
            var sp = new Spec { Width = -1, Prec = -1 };
            // flags
            for (; i < f.Length; i++)
            {
                if (f[i] == '+') sp.Plus = true;
                else if (f[i] == '-') sp.Minus = true;
                else if (f[i] == ' ') sp.Space = true;
                else if (f[i] == '#') sp.Hash = true;
                else if (f[i] == '0') sp.Zero = true;
                else break;
            }
            // width (number or *)
            if (i < f.Length && f[i] == '*') { sp.Width = ai < args.Length ? (int)ToLong(args[ai++]) : 0; if (sp.Width < 0) { sp.Minus = true; sp.Width = -sp.Width; } i++; }
            else { int ws = i; while (i < f.Length && char.IsDigit(f[i])) i++; if (i > ws) sp.Width = int.Parse(f.Substring(ws, i - ws), Inv); }
            // precision
            if (i < f.Length && f[i] == '.')
            {
                i++;
                if (i < f.Length && f[i] == '*') { sp.Prec = ai < args.Length ? (int)ToLong(args[ai++]) : 0; i++; }
                else { int ps = i; while (i < f.Length && char.IsDigit(f[i])) i++; sp.Prec = (i > ps) ? int.Parse(f.Substring(ps, i - ps), Inv) : 0; }
            }
            if (i >= f.Length) { sb.Append('%'); break; }
            sp.Verb = f[i];
            if (sp.Verb == '%') { sb.Append('%'); continue; }
            object? arg = ai < args.Length ? args[ai++] : MissingArg;
            sb.Append(Pad(FormatVerb(arg, sp), sp));
        }
        return sb.ToString();
    }

    private static readonly object MissingArg = new();

    // Pad a formatted core string to the spec width (space- or zero-justified).
    private static string Pad(string core, Spec sp)
    {
        if (sp.Width < 0 || core.Length >= sp.Width) return core;
        int n = sp.Width - core.Length;
        if (sp.Minus) return core + new string(' ', n);
        if (sp.Zero && !IsBadVerb(core))
        {
            // zero-pad after a leading sign/0x prefix
            int p = 0;
            if (core.Length > 0 && (core[0] == '-' || core[0] == '+' || core[0] == ' ')) p = 1;
            if (core.Length >= p + 2 && core[p] == '0' && (core[p + 1] == 'x' || core[p + 1] == 'X')) p += 2;
            return core.Substring(0, p) + new string('0', n) + core.Substring(p);
        }
        return new string(' ', n) + core;
    }

    private static bool IsBadVerb(string s) => s.Length > 2 && s[0] == '%' && s[1] == '!';

    private static string FormatVerb(object? v, Spec sp)
    {
        if (ReferenceEquals(v, MissingArg)) return "%!" + sp.Verb + "(MISSING)";
        char verb = sp.Verb;
        // A typed-box value dispatches its Stringer for %v/%s and names itself for
        // %T (handled downstream); every other verb formats the underlying value —
        // unwrap here so the numeric/char/quote paths never see the wrapper (and so
        // a Stringer that itself uses %d can't recurse infinitely).
        if (v is GoNamed gn && verb != 'v' && verb != 's' && verb != 'T') v = gn.Value;
        switch (verb)
        {
            case 'd': return IntVerb(v, sp, 10, false);
            case 'b': return IntVerb(v, sp, 2, false);
            case 'o': return IntVerb(v, sp, 8, sp.Hash);
            case 'x': return v is GoString gx ? HexStr(gx, false) : v is GoSlice sx ? HexSlice(sx, false) : IntVerb(v, sp, 16, sp.Hash);
            case 'X': return v is GoString gX ? HexStr(gX, true) : v is GoSlice sX ? HexSlice(sX, true) : IntVerb(v, sp, -16, sp.Hash);
            case 't': return v is bool bb ? (bb ? "true" : "false") : BadVerb(verb, v);
            case 'c': return IsIntegral(v) ? char.ConvertFromUtf32((int)ToLong(v)) : BadVerb(verb, v);
            case 'U': return IsIntegral(v) ? UnicodeVerb(ToLong(v), sp.Hash) : BadVerb(verb, v);
            case 's': return StrVerb(v, sp);
            case 'q': return QuoteVerb(v);
            case 'f':
            case 'F': return FloatVerb(v, sp, () => GoFtoa.FormatF(ToDouble(v), sp.Prec < 0 ? 6 : sp.Prec), verb);
            case 'e': return FloatVerb(v, sp, () => GoFtoa.FormatE(ToDouble(v), sp.Prec < 0 ? 6 : sp.Prec), verb);
            case 'E': return FloatVerb(v, sp, () => GoFtoa.FormatE(ToDouble(v), sp.Prec < 0 ? 6 : sp.Prec, 'E'), verb);
            case 'g': return FloatVerb(v, sp, () => sp.Prec < 0 ? GoFtoa.Shortest(ToDouble(v)) : GoFtoa.FormatG(ToDouble(v), sp.Prec), verb);
            case 'G': return FloatVerb(v, sp, () => sp.Prec < 0 ? GoFtoa.Shortest(ToDouble(v)) : GoFtoa.FormatG(ToDouble(v), sp.Prec), verb);
            case 'p': return v == null ? "<nil>" : "0x" + (System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(v) & 0xffffff).ToString("x", Inv);
            case 'T': return GoTypeName(v);
            case 'w': // %w (Errorf) formats the wrapped error like %v
            case 'v': return sp.Hash ? FormatGoSyntax(v) : Format(v, 'v', sp.Plus, sp.Hash);
            default: return BadVerb(verb, v);
        }
    }

    // %U formats a code point as "U+XXXX" (>= 4 uppercase hex digits). With the '#'
    // flag (%#U) Go appends the quoted character when it is printable, matching
    // unicode.IsPrint (letters/marks/numbers/punct/symbols + ASCII space U+0020).
    private static string UnicodeVerb(long r, bool hash)
    {
        string s = "U+" + r.ToString("X4", Inv);
        if (hash && r >= 0 && r <= 0x10FFFF && !(r >= 0xD800 && r <= 0xDFFF))
        {
            var str = char.ConvertFromUtf32((int)r);
            var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(str, 0);
            bool printable = cat != System.Globalization.UnicodeCategory.Control
                && cat != System.Globalization.UnicodeCategory.Format
                && cat != System.Globalization.UnicodeCategory.Surrogate
                && cat != System.Globalization.UnicodeCategory.OtherNotAssigned
                && !(cat == System.Globalization.UnicodeCategory.SpaceSeparator && r != 0x20)
                && !(cat == System.Globalization.UnicodeCategory.LineSeparator)
                && !(cat == System.Globalization.UnicodeCategory.ParagraphSeparator);
            if (printable) s += " '" + str + "'";
        }
        return s;
    }

    private static string IntVerb(object? v, Spec sp, int baseN, bool hash)
    {
        if (!IsIntegral(v)) return BadVerb(sp.Verb, v);
        bool upper = baseN < 0; int b = System.Math.Abs(baseN);
        string digits; bool neg = false;
        if (v is ulong u) digits = ToBase(u, b);
        else { long l = ToLong(v); neg = l < 0; digits = ToBase(neg ? (ulong)(-l) : (ulong)l, b); }
        if (upper) digits = digits.ToUpperInvariant();
        string prefix = "";
        if (hash && b == 16) prefix = upper ? "0X" : "0x";
        if (hash && b == 8 && digits != "0") prefix = "0";
        string sign = neg ? "-" : sp.Plus ? "+" : sp.Space ? " " : "";
        return sign + prefix + digits;
    }

    private static string ToBase(ulong v, int b)
    {
        if (v == 0) return "0";
        const string D = "0123456789abcdef";
        var sb = new StringBuilder();
        while (v > 0) { sb.Insert(0, D[(int)(v % (ulong)b)]); v /= (ulong)b; }
        return sb.ToString();
    }

    private static string StrVerb(object? v, Spec sp)
    {
        // %s applies to strings, errors, and composites; a bare number/bool/char
        // is a bad verb in Go (it has no string form).
        if (IsIntegral(v) || IsFloaty(v) || v is bool) return BadVerb('s', v);
        string s = v switch
        {
            GoString g => g.ToDotNetString(),
            IGoError e => e.Error().ToDotNetString(),
            GoSlice sl => Format(sl, 'v', sp.Plus, sp.Hash),
            _ => Format(v, 'v', sp.Plus, sp.Hash),
        };
        if (sp.Prec >= 0 && s.Length > sp.Prec) s = s.Substring(0, sp.Prec);
        return s;
    }

    private static string QuoteVerb(object? v)
    {
        if (v is GoString gq) return GoQuote(gq);
        if (IsIntegral(v)) return "'" + char.ConvertFromUtf32((int)ToLong(v)) + "'";
        // %q over a slice quotes each element.
        if (v is GoSlice sl)
        {
            var sb = new StringBuilder("[");
            for (int i = 0; i < sl.Len; i++) { if (i > 0) sb.Append(' '); sb.Append(QuoteVerb(sl.Data![sl.Off + i])); }
            return sb.Append(']').ToString();
        }
        return BadVerb('q', v);
    }

    private static string FloatVerb(object? v, Spec sp, System.Func<string> fmt, char verb)
    {
        if (!IsFloaty(v) && !IsIntegral(v)) return BadVerb(verb, v);
        double d = ToDouble(v);
        if (double.IsNaN(d)) return "NaN";
        if (double.IsInfinity(d)) return d < 0 ? "-Inf" : (sp.Plus ? "+Inf" : "+Inf");
        string s = fmt();
        if (s.Length > 0 && s[0] != '-')
        {
            if (sp.Plus) s = "+" + s;
            else if (sp.Space) s = " " + s;
        }
        return s;
    }

    private static string BadVerb(char verb, object? v) => "%!" + verb + "(" + GoTypeName(v) + "=" + Format(v, 'v', false, false) + ")";

    // ---- value helpers -----------------------------------------------------

    private static bool IsIntegral(object? v) =>
        v is long || v is int || v is ulong || v is uint || v is short || v is ushort || v is byte || v is sbyte;
    private static bool IsFloaty(object? v) => v is double || v is float;
    private static long ToLong(object? v) => v == null ? 0 : v is ulong u ? unchecked((long)u) : System.Convert.ToInt64(v, Inv);
    private static double ToDouble(object? v) => v == null ? 0 : System.Convert.ToDouble(v, Inv);

    private static string HexStr(GoString s, bool upper)
    {
        var sb = new StringBuilder();
        foreach (byte b in s.Bytes) sb.Append(b.ToString(upper ? "X2" : "x2", Inv));
        return sb.ToString();
    }

    private static string HexSlice(GoSlice s, bool upper)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < s.Len; i++) sb.Append(((byte)ToLong(s.Data[s.Off + i])).ToString(upper ? "X2" : "x2", Inv));
        return sb.ToString();
    }

    // Go-style quoted string: prints valid runes, escapes specials, and emits
    // \xNN for bytes that are not part of a valid UTF-8 sequence.
    private static string GoQuote(GoString gs)
    {
        var sb = new StringBuilder("\"");
        byte[] b = gs.Bytes;
        int i = 0;
        while (i < b.Length)
        {
            int n = Utf8DecodeLen(b, i);
            if (n == 1 && b[i] >= 0x80) { sb.Append("\\x").Append(b[i].ToString("x2", Inv)); i++; continue; }
            if (n == 1)
            {
                char c = (char)b[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\r': sb.Append("\\r"); break;
                    default: if (c < 0x20) sb.Append("\\x").Append(((int)c).ToString("x2", Inv)); else sb.Append(c); break;
                }
                i++;
            }
            else { sb.Append(Encoding.UTF8.GetString(b, i, n)); i += n; }
        }
        return sb.Append('"').ToString();
    }

    // Length (1-4) of the UTF-8 sequence starting at b[i]; 1 if invalid.
    private static int Utf8DecodeLen(byte[] b, int i)
    {
        byte c = b[i];
        if (c < 0x80) return 1;
        int n = c >= 0xF0 ? 4 : c >= 0xE0 ? 3 : c >= 0xC0 ? 2 : 0;
        if (n == 0 || i + n > b.Length) return 1;
        for (int k = 1; k < n; k++) if ((b[i + k] & 0xC0) != 0x80) return 1;
        return n;
    }

    // Go type name for a boxed value (for %T and bad-verb messages). Slice/map
    // element types are erased at runtime, so those are approximate.
    private static string GoTypeName(object? v) => v switch
    {
        null => "<nil>",
        bool => "bool",
        long => "int",
        int => "int32",
        ulong => "uint64",
        uint => "uint32",
        double => "float64",
        float => "float32",
        GoString => "string",
        IGoError => "*errors.errorString",
        GoComplex => "complex128",
        GoSlice => "[]interface {}",
        GoMap => "map[string]interface {}",
        GoPtr p => "*" + GoTypeName(p.Value),
        GoNamed nm => NamedTypeNameOr(nm),
        _ => "main." + v.GetType().Name,
    };

    // %T of a typed-box value: its registered Go display name (e.g. "main.Money",
    // "sort.StringSlice"), falling back to the underlying value's name.
    private static string NamedTypeNameOr(GoNamed nm)
    {
        var name = Rt.NamedTypeName(nm.TypeId);
        return name.Length > 0 ? name : GoTypeName(nm.Value);
    }

    private static string Format(object? v, char verb, bool plus, bool hash)
    {
        // A type's String()/Error() method governs %v and %s output (but not the
        // Go-syntax %#v, nor numeric/bool/pointer-address verbs).
        if (!hash && (verb == 'v' || verb == 's') && TryStringer(v, out var sv)) return sv;
        // For every other verb a named value formats by its underlying value
        // (e.g. %d on a `type Money int64`).
        if (v is GoNamed nm) v = nm.Value;
        switch (v)
        {
            case null: return "<nil>";
            case bool b: return b ? "true" : "false";
            case long l: return l.ToString(Inv);
            case int i: return i.ToString(Inv);
            case ulong u: return u.ToString(Inv);
            case uint ui: return ui.ToString(Inv);
            case short sh: return sh.ToString(Inv);
            case ushort ush: return ush.ToString(Inv);
            case byte by: return by.ToString(Inv);
            case sbyte sb: return sb.ToString(Inv);
            case float fl: return FormatFloatV(fl);
            case double d: return FormatFloatV(d);
            case GoString gs: return gs.ToDotNetString();
            case IGoError e: return e.Error().ToDotNetString();
            case GoComplex c: return "(" + FormatFloatV(c.Re) + (c.Im < 0 ? "-" : "+") + FormatFloatV(System.Math.Abs(c.Im)) + "i)";
            // Go prints &{...}/&[...]/&map[...] for pointers to a composite, but a
            // hex address for a pointer to a scalar.
            case GoPtr p when p.Value is GoSlice || p.Value is GoMap || IsStructVal(p.Value):
                return "&" + Format(p.Value, verb, plus, hash);
            case GoPtr p: return "0x" + (System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(p) & 0xffffff).ToString("x", Inv);
            case GoSlice sl when sl.Data == null: return "[]";
            case GoMap m when m.Data == null: return "map[]";
            case GoSlice sl: return FormatSlice(sl, plus, hash);
            case GoMap m: return FormatMap(m, plus, hash);
            // Shim types that carry their own Go String() representation.
            case GoBigInt bi: return Big.Int_String(bi).ToDotNetString();
            case GoTime tm: return Time.Time_String(tm).ToDotNetString();
            case GoStringBuilder gsb: return gsb.SB.ToString();
            default: return FormatStruct(v, plus, hash);
        }
    }

    private static bool IsStructVal(object? v) =>
        v != null && v.GetType().IsValueType && v is not bool && v is not long && v is not int
        && v is not ulong && v is not uint && v is not double && v is not float && v is not GoString
        && v is not GoSlice && v is not GoComplex;

    // %#v — Go-syntax representation.
    private static string FormatGoSyntax(object? v)
    {
        switch (v)
        {
            case null: return "<nil>";
            case bool b: return b ? "true" : "false";
            case GoString gs: return GoQuote(gs);
            case long or int or ulong or uint: return Format(v, 'v', false, false);
            case double d: return FormatFloatV(d);
            case GoPtr p: return "&" + FormatGoSyntax(p.Value);
            case GoSlice sl:
            {
                var sb = new StringBuilder("[]interface {}{");
                for (int i = 0; i < sl.Len; i++) { if (i > 0) sb.Append(", "); sb.Append(FormatGoSyntax(sl.Data[sl.Off + i])); }
                return sb.Append('}').ToString();
            }
            case GoMap m:
            {
                var sb = new StringBuilder("map[string]interface {}{");
                var keys = new System.Collections.Generic.List<(string s, object? k)>();
                if (m.Data != null) foreach (var k in m.Data.Keys) keys.Add((k is GoString g ? g.ToDotNetString() : k?.ToString() ?? "", k));
                keys.Sort((a, b) => string.CompareOrdinal(a.s, b.s));
                for (int i = 0; i < keys.Count; i++) { if (i > 0) sb.Append(", "); sb.Append('"').Append(keys[i].s).Append("\":").Append(FormatGoSyntax(m.Data![keys[i].k!])); }
                return sb.Append('}').ToString();
            }
            default:
            {
                var t = v.GetType();
                var fields = t.GetFields(BindingFlags.Public | BindingFlags.Instance);
                var sb = new StringBuilder("main." + t.Name + "{");
                for (int i = 0; i < fields.Length; i++) { if (i > 0) sb.Append(", "); sb.Append(fields[i].Name).Append(':').Append(FormatGoSyntax(fields[i].GetValue(v))); }
                return sb.Append('}').ToString();
            }
        }
    }

    private static string FormatFloatV(double d) => GoFtoa.Shortest(d);

    private static string FormatSlice(GoSlice s, bool plus, bool hash)
    {
        var sb = new StringBuilder("[");
        for (int i = 0; i < s.Len; i++) { if (i > 0) sb.Append(' '); sb.Append(Format(s.Data[s.Off + i], 'v', plus, hash)); }
        return sb.Append(']').ToString();
    }

    private static string FormatMap(GoMap m, bool plus, bool hash)
    {
        var keys = new System.Collections.Generic.List<(string s, object? k)>();
        foreach (var k in m.Data.Keys) keys.Add((k is GoString g ? g.ToDotNetString() : k?.ToString() ?? "", k));
        keys.Sort((a, b) => string.CompareOrdinal(a.s, b.s));
        var sb = new StringBuilder("map[");
        for (int i = 0; i < keys.Count; i++)
        { if (i > 0) sb.Append(' '); sb.Append(Format(keys[i].k, 'v', plus, hash)).Append(':').Append(Format(m.Data[keys[i].k!], 'v', plus, hash)); }
        return sb.Append(']').ToString();
    }

    private static string FormatStruct(object v, bool plus, bool hash)
    {
        var t = v.GetType();
        var fields = t.GetFields(BindingFlags.Public | BindingFlags.Instance);
        var sb = new StringBuilder("{");
        for (int i = 0; i < fields.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            if (plus) sb.Append(fields[i].Name).Append(':');
            sb.Append(Format(fields[i].GetValue(v), 'v', plus, hash));
        }
        return sb.Append('}').ToString();
    }

}
