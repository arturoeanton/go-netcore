namespace GoCLR.Stdlib;

using System.Reflection;
using System.Text;
using System.Globalization;
using GoCLR.Runtime;

/// <summary>The fmt.State handed to a user type's Format(f fmt.State, verb rune): it captures
/// the formatted output (IGoWriter, so fmt.Fprintf(f, …) routes its bytes here through
/// Fmt.WriteTo) and reports the active verb's flags/width/precision. One per Formatter call.</summary>
[GoShim("fmt.State")]
public sealed class GoFmtState : IGoWriter
{
    public readonly StringBuilder SB = new();
    public int Width = -1, Prec = -1; // -1 == unset
    public bool Minus, Plus, Space, Hash, Zero;
    public void GoWrite(byte[] data) => SB.Append(Encoding.UTF8.GetString(data));
}

/// <summary>Shim for a subset of Go's <c>fmt</c> package. %v formatting uses .NET
/// reflection over boxed values, mirroring Go's default formatting.</summary>
public static class Fmt
{
    // fmt.State method shims (receiver-first), dispatched when a user Format body calls
    // f.Write / f.Width / f.Precision / f.Flag on the State it was handed.
    public static object?[] State_Write(object st, GoSlice b)
    {
        var s = (GoFmtState)st;
        s.SB.Append(Encoding.UTF8.GetString(SliceToBytes(b)));
        return new object?[] { (long)b.Len, null };
    }
    public static object?[] State_Width(object st) { var s = (GoFmtState)st; return new object?[] { (long)(s.Width < 0 ? 0 : s.Width), s.Width >= 0 }; }
    public static object?[] State_Precision(object st) { var s = (GoFmtState)st; return new object?[] { (long)(s.Prec < 0 ? 0 : s.Prec), s.Prec >= 0 }; }
    public static bool State_Flag(object st, long c)
    {
        var s = (GoFmtState)st;
        return (int)c switch { '-' => s.Minus, '+' => s.Plus, ' ' => s.Space, '#' => s.Hash, '0' => s.Zero, _ => false };
    }

    // fmt.Formatter: a user type's Format(f fmt.State, verb rune) controls every verb. Build
    // a State carrying this verb's flags/width/precision, drive Format through the callback
    // bridge, and return what it wrote. The Formatter owns its own padding (via State.Width),
    // so the caller must not pad the result further.
    private static bool TryFormatter(object? v, Spec sp, out string result)
    {
        result = "";
        if (v == null || !Bridge.HasMethod(v, "Format")) return false;
        var st = new GoFmtState
        {
            Width = sp.Width, Prec = sp.Prec,
            Minus = sp.Minus, Plus = sp.Plus, Space = sp.Space, Hash = sp.Hash, Zero = sp.Zero,
        };
        Bridge.CallMethod(v, "Format", st, (int)sp.Verb);
        result = st.SB.ToString();
        return true;
    }

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
        // math/big values are Stringers (fmt.Println prints them via String()).
        if (v is GoBigRat) { s = Big.Rat_String(v).ToDotNetString(); return true; }
        if (v is GoBigInt) { s = Big.Int_String(v).ToDotNetString(); return true; }
        if (v is GoBigFloat) { s = Big.Float_String(v).ToDotNetString(); return true; }
        if (v is GoSignal sg) { s = sg.Name; return true; }
        if (v is GoNetipAddr) { s = Netip.Addr_String(v).ToDotNetString(); return true; }
        if (v is GoNetipAddrPort) { s = Netip.AddrPort_String(v).ToDotNetString(); return true; }
        if (v is GoNetipPrefix) { s = Netip.Prefix_String(v).ToDotNetString(); return true; }
        // net.TCPAddr/UDPAddr/IPNet (and other net.Addr shims) carry their String() in .Str.
        if (v is GoNetAddr nad) { s = nad.Str; return true; }
        // *url.URL prints via its String() (the shim object, not its struct fields).
        if (v is GoUrl gu) { s = Url.URL_String(gu).ToDotNetString(); return true; }
        // *mail.Address prints via its String() ("Name" <addr>), not its struct fields.
        if (v is GoMailAddress gma) { s = Mail.Address_String(gma).ToDotNetString(); return true; }
        // *url.Userinfo prints via its String() ("user:password"), not its struct fields.
        if (v is GoUserinfo gui) { s = Url.Userinfo_String(gui).ToDotNetString(); return true; }
        // *bytes.Buffer and *regexp.Regexp are Stringers — print the buffer contents / the
        // pattern, not their internal struct fields.
        if (v is GoBuffer gbuf) { s = BytesBuffer.String(gbuf).ToDotNetString(); return true; }
        if (v is GoRegexp gre) { s = Regexp.Re_String(gre).ToDotNetString(); return true; }
        // *time.Location prints its zone name (Local/UTC/IST/...).
        if (v is GoLocation gloc) { s = Time.Location_String(gloc).ToDotNetString(); return true; }
        if (v is GoNamed kn && Rt.NamedTypeName(kn.TypeId) == "reflect.Kind")
        { s = GoKind.Name((int)System.Convert.ToInt64(kn.Value ?? 0L)); return true; }
        // Shim named scalar types whose String() is a runtime shim (not a lowered Go
        // method, so no stringer closure is registered): dispatch by the typed-box name.
        if (v is GoNamed tnm)
        {
            switch (Rt.NamedTypeName(tnm.TypeId))
            {
                case "time.Duration": s = Time.Duration_String(System.Convert.ToInt64(tnm.Value ?? 0L)).ToDotNetString(); return true;
                case "time.Month": s = Time.Month_String(System.Convert.ToInt64(tnm.Value ?? 0L)).ToDotNetString(); return true;
                case "time.Weekday": s = Time.Weekday_String(System.Convert.ToInt64(tnm.Value ?? 0L)).ToDotNetString(); return true;
                case "slog.Level": s = Slog.Level_String(System.Convert.ToInt64(tnm.Value ?? 0L)).ToDotNetString(); return true;
                case "fs.FileMode": s = Fs.Mode_String(System.Convert.ToUInt32(tnm.Value ?? 0u)).ToDotNetString(); return true;
                // json.Delim is a rune whose String() is the single character (its shim
                // method has no lowered body, so no stringer closure is registered).
                case "json.Delim": s = Json.Delim_String((int)System.Convert.ToInt64(tnm.Value ?? 0L)).ToDotNetString(); return true;
                // net.IP/IPMask/HardwareAddr are named []byte whose String() is a shim; the
                // typed box carries the GoSlice, so format it via the matching shim method.
                case "net.IP" when tnm.Value is GoSlice ipv: s = Net.IP_String(ipv).ToDotNetString(); return true;
                case "net.IPMask" when tnm.Value is GoSlice mv: s = Net.IPMask_String(mv).ToDotNetString(); return true;
                case "net.HardwareAddr" when tnm.Value is GoSlice hv: s = Net.HardwareAddr_String(hv).ToDotNetString(); return true;
                case "net.Flags": s = Net.Flags_String(System.Convert.ToUInt64(tnm.Value ?? 0u)).ToDotNetString(); return true;
            }
        }
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
        var wraps = FindWrapArgs(f, a);
        // Go: 0 %w -> errors.errorString, 1 -> *fmt.wrapError, 2+ -> *fmt.wrapErrors
        // (Unwrap() []error), so errors.Is/As must search every wrapped error.
        if (wraps.Count >= 2) return new GoError(GoString.FromDotNetString(msg)) { Multi = wraps.ToArray() };
        return new GoError(GoString.FromDotNetString(msg), wraps.Count == 1 ? wraps[0] : null);
    }

    // The arguments consumed by %w verbs (the wrapped errors), in order, mirroring the
    // argument-advance rules of DoSprintf.
    private static System.Collections.Generic.List<object?> FindWrapArgs(string f, object?[] args)
    {
        var wraps = new System.Collections.Generic.List<object?>();
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
            if (verb == 'w') { if (ai < args.Length) wraps.Add(args[ai]); ai++; continue; }
            ai++;
        }
        return wraps;
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
            case IGoWriter gw: gw.GoWrite(Encoding.UTF8.GetBytes(s)); return n;
            // A bufio.Writer buffers; Fprint* must append to its buffer (NOT punch through to
            // the underlying sink) so its bytes stay ordered against WriteString/Byte/Rune and
            // only reach the sink on Flush — like Go's fmt.Fprintf(bufio.NewWriter(w), …).
            case GoBufWriter bw: bw.Buf.AddRange(Encoding.UTF8.GetBytes(s)); return n;
            // A bufio.ReadWriter embeds the *Writer; Fprint* buffers through it, like Go.
            case GoBufReadWriter rw when rw.W is GoBufWriter rww: rww.Buf.AddRange(Encoding.UTF8.GetBytes(s)); return n;
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
        bool reordered = false;
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
            // explicit argument index [n] before the width / value (Go: %[2]d, %[2]*d)
            ParseArgIndex(f, ref i, ref ai, ref reordered);
            // width (number or *)
            if (i < f.Length && f[i] == '*') { sp.Width = ai < args.Length ? (int)ToLong(args[ai++]) : 0; if (sp.Width < 0) { sp.Minus = true; sp.Width = -sp.Width; } i++; }
            else { int ws = i; while (i < f.Length && char.IsDigit(f[i])) i++; if (i > ws) sp.Width = int.Parse(f.Substring(ws, i - ws), Inv); }
            // precision
            if (i < f.Length && f[i] == '.')
            {
                i++;
                ParseArgIndex(f, ref i, ref ai, ref reordered); // [n] may index the precision arg too
                if (i < f.Length && f[i] == '*') { sp.Prec = ai < args.Length ? (int)ToLong(args[ai++]) : 0; i++; }
                else { int ps = i; while (i < f.Length && char.IsDigit(f[i])) i++; sp.Prec = (i > ps) ? int.Parse(f.Substring(ps, i - ps), Inv) : 0; }
            }
            // explicit argument index immediately before the verb (Go: %[1]v, %6.2[1]f)
            ParseArgIndex(f, ref i, ref ai, ref reordered);
            if (i >= f.Length) { sb.Append('%'); break; }
            sp.Verb = f[i];
            if (sp.Verb == '%') { sb.Append('%'); continue; }
            object? arg = ai < args.Length ? args[ai++] : MissingArg;
            sb.Append(FmtElem(arg, sp));
        }
        // Leftover arguments are reported as %!(EXTRA type=val, ...) — unless an explicit
        // argument index appeared (Go suppresses the check once arguments are reordered).
        if (!reordered && ai < args.Length)
        {
            sb.Append("%!(EXTRA ");
            for (int k = ai; k < args.Length; k++)
            {
                if (k > ai) sb.Append(", ");
                sb.Append(GoTypeName(args[k])).Append('=').Append(Format(args[k], 'v', false, false));
            }
            sb.Append(')');
        }
        return sb.ToString();
    }

    private static readonly object MissingArg = new();

    // Go's explicit argument index: "[n]" sets the current 1-based argument to n (so the next
    // value consumed — a '*' width/precision or the verb — reads args[n-1], and the implicit
    // counter continues from there). A no-op if there is no valid "[digits]" at f[i].
    private static void ParseArgIndex(string f, ref int i, ref int ai, ref bool reordered)
    {
        if (i >= f.Length || f[i] != '[') return;
        int j = i + 1, start = j;
        while (j < f.Length && char.IsDigit(f[j])) j++;
        if (j < f.Length && f[j] == ']' && j > start)
        {
            ai = int.Parse(f.Substring(start, j - start), Inv) - 1;
            i = j + 1;
            reordered = true; // an explicit [n] disables the trailing %!(EXTRA ...) report
        }
    }

    // FormatVerb with the width/precision applied at the right level: a numeric verb
    // recursing into a slice/map/struct pads each ELEMENT (Go: %03d of []int{5,42} is
    // "[005 042]", not the whole composite), so the pad is skipped at the composite level
    // and applied inside the recursion. Scalars and the non-recursing verbs pad as before.
    private static string FmtElem(object? v, Spec sp) =>
        // A fmt.Formatter owns its rendering (including width), so it is not padded here.
        TryFormatter(v, sp, out var fr) ? fr
        : RecursesPerElement(v, sp.Verb) ? FormatVerb(v, sp) : Pad(FormatVerb(v, sp), sp);

    private static bool RecursesPerElement(object? v, char verb)
    {
        if (verb is not ('d' or 'b' or 'o' or 'c' or 'U' or 'f' or 'F' or 'e' or 'E' or 'g' or 'G' or 't'))
            return false; // only numeric verbs recurse element-wise with no byte/string ambiguity
        return v is GoSlice || v is GoMap || IsStructVal(v) || (v is GoPtr p && IsStructVal(GoPtrs.Get(p)));
    }

    // Pad a formatted core string to the spec width (space- or zero-justified).
    private static string Pad(string core, Spec sp)
    {
        if (sp.Width < 0 || core.Length >= sp.Width) return core;
        int n = sp.Width - core.Length;
        if (sp.Minus) return core + new string(' ', n);
        // Go ignores the 0 flag for an integer verb when a precision is given (the precision
        // already controls the digit count), so such a field is space-padded.
        bool zeroIgnored = sp.Prec >= 0 && sp.Verb is 'd' or 'b' or 'o' or 'O' or 'x' or 'X';
        if (sp.Zero && !zeroIgnored && !IsBadVerb(core))
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

    // A typed composite whose element type has runtime identity (a Stringer enum, an
    // error, …): its elements are stored bare, so re-tag each with the element id (kept in
    // the registry) so the per-element format dispatches String()/Error(). The GoNamed
    // wrapper is preserved so %T and the []byte fast paths still see the composite's name.
    private static object? MaybeRetag(object? v)
    {
        if (v is not GoNamed nm) return v;
        if (nm.Value is GoSlice sl && sl.Data != null)
        {
            long eid = Rt.CompositeElemId(nm.TypeId);
            if (eid == 0) return v;
            var nd = new object?[sl.Len];
            for (int i = 0; i < sl.Len; i++) { var e = sl.Data[sl.Off + i]; nd[i] = e is GoNamed ? e : Rt.MakeNamed(e, eid); }
            return new GoNamed(nm.TypeId, new GoSlice { Data = nd, Off = 0, Len = sl.Len, Cap = sl.Len });
        }
        if (nm.Value is GoMap m && m.Data != null)
        {
            long eid = Rt.CompositeElemId(nm.TypeId);
            long kid = Rt.CompositeKeyId(nm.TypeId);
            if (eid == 0 && kid == 0) return v;
            var nmap = new GoMap { Data = new System.Collections.Generic.Dictionary<object, object?>(m.Data.Count) };
            foreach (var kv in m.Data)
            {
                object key = kid != 0 && kv.Key is not GoNamed ? Rt.MakeNamed(kv.Key, kid)! : kv.Key;
                object? val = eid != 0 && kv.Value is not GoNamed ? Rt.MakeNamed(kv.Value, eid) : kv.Value;
                nmap.Data[key] = val;
            }
            return new GoNamed(nm.TypeId, nmap);
        }
        return v;
    }

    // A struct field whose Go type carries identity (a Stringer enum, a typed slice/map) is
    // stored as a bare value; re-tag it with the registered field type id so it dispatches
    // String()/names its element. Idempotent (an already-tagged or untagged-type value is
    // returned unchanged).
    private static object? RetagField(string structName, string field, object? fv)
    {
        if (fv is null or GoNamed) return fv;
        long id = Rt.FieldTypeId(structName, field);
        return id == 0 ? fv : Rt.MakeNamed(fv, id);
    }

    // The ordering key for a map key when sorting fmt's output: a re-tagged (GoNamed) key
    // sorts by its underlying value, so map[Suit]int still orders Hearts(0) before Spades(3).
    private static string MapKeySortStr(object? k)
    {
        if (k is GoNamed n) k = n.Value;
        return k is GoString g ? g.ToDotNetString() : k?.ToString() ?? "";
    }

    // Go orders map keys by their type's natural order: integers/floats numerically (not
    // lexically, so 2 < 10), bool false<true, everything else by its string form.
    private static int MapKeyCompare(object? a, object? b)
    {
        if (a is GoNamed na) a = na.Value;
        if (b is GoNamed nb) b = nb.Value;
        if (a is long or int or short or sbyte or byte or ushort or uint)
            return System.Convert.ToInt64(a).CompareTo(System.Convert.ToInt64(b));
        if (a is ulong ua && b is ulong ub) return ua.CompareTo(ub);
        if (a is double or float) return System.Convert.ToDouble(a).CompareTo(System.Convert.ToDouble(b));
        if (a is bool ba && b is bool bb) return ba.CompareTo(bb);
        return string.CompareOrdinal(MapKeySortStr(a), MapKeySortStr(b));
    }

    private static string FormatVerb(object? v, Spec sp)
    {
        if (ReferenceEquals(v, MissingArg)) return "%!" + sp.Verb + "(MISSING)";
        // An untyped nil interface: %v/%w print "<nil>" and %T prints "<nil>", but every
        // other verb is a bad verb rendered as "%!verb(<nil>)" — with no "type=value" tail,
        // since a nil interface carries no dynamic type. (A typed nil *int would print 0 for
        // integer verbs in Go, but goclr erases a typed nil pointer to a bare null, so it
        // cannot be told apart here; matching the untyped form is the faithful choice.)
        if (v is null)
        {
            char nv = sp.Verb;
            if (nv == 'T') return "<nil>";
            if (nv == 'v' || nv == 'w') return sp.Hash ? FormatGoSyntax(null) : Format(null, 'v', sp.Plus, sp.Hash);
            return "%!" + nv + "(<nil>)";
        }
        if (v is string vraw) v = GoString.FromDotNetString(vraw); // a shim struct's raw C# string field
        v = MaybeRetag(v); // re-tag a []Stringer's bare elements before any verb dispatch
        // *big.Float's %x is a Formatter case that differs from a plain float64: it uses a
        // default precision of 6 hex-mantissa digits (a float64 uses the shortest form).
        if (v is GoBigFloat bfg && sp.Verb == 'x')
        {
            int hprec = sp.Prec >= 0 ? sp.Prec : 6;
            return FloatVerb(bfg.V, sp, () => Strconv.FormatFloat(bfg.V, 'x', hprec, 64).ToDotNetString(), 'x');
        }
        // big.Int and big.Float are Formatters with a fixed supported-verb set; any other
        // (non-Go-syntax, non-%T/%p) verb yields a bad-verb string naming the Go type and the
        // value, exactly as their Format methods do (big.Float's value is Text('g', 10)).
        if (v is GoBigInt gi9 && !sp.Hash && sp.Verb != 'T' && sp.Verb != 'p'
            && "boOdsvxX".IndexOf(sp.Verb) < 0)
            return "%!" + sp.Verb + "(big.Int=" + Big.Int_String(gi9).ToDotNetString() + ")";
        if (v is GoBigFloat gf9 && !sp.Hash && sp.Verb != 'T' && sp.Verb != 'p'
            && "eEfFgGxbv".IndexOf(sp.Verb) < 0)
            return "%!" + sp.Verb + "(*big.Float=" + Big.Float_String(gf9).ToDotNetString() + ")";
        if (v is GoBigFloat bfv) v = bfv.V; // *big.Float (double-backed) formats like its value
        char verb = sp.Verb;
        // A typed nil pointer/func/chan is a typed box whose underlying value is nil. Go's
        // fmtPointer formats it as the zero address: %v -> "<nil>", %T -> the type name,
        // %p -> "0x0", the integer verbs b/o/d/x/X -> the address 0 (honoring # and width),
        // and any other verb -> "%!verb(T=<nil>)". (v/w/T fall through to the normal paths,
        // which already render "<nil>", "(*T)(nil)" for %#v, and the type name for %T.)
        if (v is GoNamed gnil && gnil.Value is null)
        {
            switch (verb)
            {
                case 'd': return IntVerb(0L, sp, 10, false);
                case 'b': return IntVerb(0L, sp, 2, false);
                case 'o': return IntVerb(0L, sp, 8, sp.Hash);
                case 'O': return IntVerb(0L, sp, 8, sp.Hash);
                case 'x': return IntVerb(0L, sp, 16, sp.Hash);
                case 'X': return IntVerb(0L, sp, -16, sp.Hash);
                case 'p': return "0x0";
                case 'v': case 'w': case 'T': break;
                default: return "%!" + verb + "(" + GoTypeName(v) + "=<nil>)";
            }
        }
        // The typed-box name (if any) before unwrapping, so %x can tell a []byte ("[]uint8",
        // formatted as a hex string) from a []int ("[]int", which recurses element-wise).
        string? wname = v is GoNamed g0 ? Rt.NamedTypeName(g0.TypeId) : null;
        // A named []byte type (json.RawMessage, xml.CharData) also formats as bytes for %x/%q.
        bool wByteNamed = v is GoNamed g0b && Rt.IsByteNamed(g0b.TypeId);
        // A typed-box value dispatches its Stringer for %v/%s and names itself for
        // %T (handled downstream); every other verb formats the underlying value —
        // unwrap here so the numeric/char/quote paths never see the wrapper (and so
        // a Stringer that itself uses %d can't recurse infinitely).
        // %q/%x/%X also dispatch a Stringer/error (so `type Color int` with String() quotes
        // or hex-encodes the name, not the rune) — keep the wrapper in those cases.
        if (v is GoNamed gn && verb != 'v' && verb != 's' && verb != 'T'
            && !((verb == 'q' || verb == 'x' || verb == 'X') && TryStringer(gn, out _))) v = gn.Value;
        // A scalar verb applied to a slice/array or map recurses element-wise, as Go does
        // (%d of []int{1,2} -> "[1 2]", %d of map[int]int -> "map[1:2]"). %v/%s/%T/%p and
        // the []byte-special x/X/q paths are handled by their own cases below.
        if (verb is 'd' or 'b' or 'o' or 'c' or 'U' or 'f' or 'F' or 'e' or 'E' or 'g' or 'G' or 't')
        {
            if (v is GoSlice rsl) return RecurseSlice(rsl, sp);
            if (v is GoMap rmp) return RecurseMap(rmp, sp);
            if (IsStructVal(v)) return RecurseStruct(v!, sp);
            if (v is GoPtr rgp && IsStructVal(GoPtrs.Get(rgp))) return "&" + RecurseStruct(GoPtrs.Get(rgp)!, sp);
        }
        // Go's Stringer/error rule applies to %x/%X/%q too (not only %v/%s): a value with
        // Error()/String() formats its string representation, which is then hex-encoded or
        // quoted. The Go-syntax flag (%#x) and a raw string opt out. The math/big
        // Formatters Int and Float own their verb handling — %x is the hexadecimal of the
        // *number*, not the hex of its decimal string — so they are excluded; big.Rat is
        // NOT a Formatter and follows the Stringer rule (%x of a Rat is the hex of "1/3").
        if (verb is 'x' or 'X' or 'q' && !sp.Hash
            && v is not GoString && v is not GoBigInt && v is not GoBigFloat)
        {
            if (v is IGoError ge) v = ge.Error();
            else if (TryStringer(v, out var strv)) v = GoString.FromDotNetString(strv);
        }
        switch (verb)
        {
            case 'd': return IntVerb(v, sp, 10, false);
            case 'b': return IsFloaty(v) ? FloatVerb(v, sp, () => Strconv.FormatFloat(ToDouble(v), 'b', -1, v is float ? 32 : 64).ToDotNetString(), verb) : IntVerb(v, sp, 2, false);
            case 'o': return IntVerb(v, sp, 8, sp.Hash);
            case 'O': return IntVerb(v, sp, 8, sp.Hash); // 0o-prefixed octal (prefix added in IntVerb)
            case 'x': return v is GoString gx ? HexStr(gx, false, sp.Space, sp.Hash) : v is GoSlice sx ? ((IsByteSliceName(wname) || wByteNamed) ? HexSlice(sx, false, sp.Space, sp.Hash) : RecurseSlice(sx, sp)) : v is GoMap mx ? RecurseMap(mx, sp) : IsStructVal(v) ? RecurseStruct(v!, sp) : IsFloaty(v) ? FloatVerb(v, sp, () => Strconv.FormatFloat(ToDouble(v), 'x', sp.Prec, v is float ? 32 : 64).ToDotNetString(), verb) : IntVerb(v, sp, 16, sp.Hash);
            case 'X': return v is GoString gX ? HexStr(gX, true, sp.Space, sp.Hash) : v is GoSlice sX ? ((IsByteSliceName(wname) || wByteNamed) ? HexSlice(sX, true, sp.Space, sp.Hash) : RecurseSlice(sX, sp)) : v is GoMap mX ? RecurseMap(mX, sp) : IsStructVal(v) ? RecurseStruct(v!, sp) : IsFloaty(v) ? FloatVerb(v, sp, () => Strconv.FormatFloat(ToDouble(v), 'X', sp.Prec, v is float ? 32 : 64).ToDotNetString(), verb) : IntVerb(v, sp, -16, sp.Hash);
            case 't': return v is bool bb ? (bb ? "true" : "false") : BadVerb(verb, v);
            case 'c': return IsIntegral(v) ? char.ConvertFromUtf32((int)ToLong(v)) : BadVerb(verb, v);
            case 'U': return IsIntegral(v) ? UnicodeVerb(ToLong(v), sp.Hash) : BadVerb(verb, v);
            case 's': return StrVerb(v, sp);
            case 'q':
                // %q on a string honors precision by truncating to that many runes first.
                if (v is GoString qpre && sp.Prec >= 0) { var s = qpre.ToDotNetString(); v = GoString.FromDotNetString(s.Length > sp.Prec ? s.Substring(0, sp.Prec) : s); }
                return v is GoSlice qsl && (IsByteSliceName(wname) || wByteNamed) ? GoQuote(GoString.FromBytesOwned(SliceToBytes(qsl)), sp.Plus, sp.Hash) : QuoteVerb(v, sp.Plus, sp.Hash);
            case 'f':
            case 'F': return v is GoComplex cf ? ComplexVerb(cf, d => GoFtoa.FormatF(d, sp.Prec < 0 ? 6 : sp.Prec)) : FloatVerb(v, sp, () => GoFtoa.FormatF(ToDouble(v), sp.Prec < 0 ? 6 : sp.Prec), verb);
            case 'e': return v is GoComplex ce ? ComplexVerb(ce, d => GoFtoa.FormatE(d, sp.Prec < 0 ? 6 : sp.Prec)) : FloatVerb(v, sp, () => GoFtoa.FormatE(ToDouble(v), sp.Prec < 0 ? 6 : sp.Prec), verb);
            case 'E': return v is GoComplex cE ? ComplexVerb(cE, d => GoFtoa.FormatE(d, sp.Prec < 0 ? 6 : sp.Prec, 'E')) : FloatVerb(v, sp, () => GoFtoa.FormatE(ToDouble(v), sp.Prec < 0 ? 6 : sp.Prec, 'E'), verb);
            case 'g': return v is GoComplex cg ? ComplexVerb(cg, d => GFormat(d, sp, false, false)) : FloatVerb(v, sp, () => GFormat(ToDouble(v), sp, v is float, false), verb);
            case 'G': return v is GoComplex cG ? ComplexVerb(cG, d => GFormat(d, sp, false, true)) : FloatVerb(v, sp, () => GFormat(ToDouble(v), sp, v is float, true), verb);
            case 'p':
                if (v == null) return "<nil>";
                // Go's %p accepts only pointer-like kinds (pointer/slice/map/chan/func);
                // anything else is a bad verb, e.g. %!p(bool=true).
                if (v is GoPtr || v is GoSlice || v is GoMap || v is GoClosure ||
                    (v.GetType().IsGenericType && v.GetType().GetGenericTypeDefinition() == typeof(GoChan<>)))
                    return "0x" + (System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(v) & 0xffffff).ToString("x", Inv);
                return BadVerb(verb, v);
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
        bool upper = baseN < 0; int b = System.Math.Abs(baseN);
        string digits; bool neg = false;
        if (v is GoBigInt) { var parts = Big.IntFmtParts(v, b); neg = parts.neg; digits = parts.digits; } // *big.Int (arbitrary precision)
        else if (!IsIntegral(v)) return BadVerb(sp.Verb, v);
        else if (v is ulong u) digits = ToBase(u, b);
        else { long l = ToLong(v); neg = l < 0; digits = ToBase(neg ? (ulong)(-l) : (ulong)l, b); }
        if (upper) digits = digits.ToUpperInvariant();
        // Precision sets the minimum number of digits, zero-padded (Go: %.3d of 5 -> "005"),
        // and is applied before the prefix. The special case %.0d of 0 produces no digits.
        if (sp.Prec >= 0)
        {
            if (sp.Prec == 0 && digits == "0") digits = "";
            else if (digits.Length < sp.Prec) digits = new string('0', sp.Prec - digits.Length) + digits;
        }
        string prefix = "";
        if (hash && b == 16) prefix = upper ? "0X" : "0x";
        // %#o prepends a single 0 only if the (post-precision) digits don't already start with 0.
        if (hash && b == 8 && (digits.Length == 0 || digits[0] != '0')) prefix = "0";
        if (sp.Verb == 'O' && b == 8) prefix = "0o"; // %O always carries the 0o prefix (even for 0)
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
        // A String()/Error() method governs %s (like %v), ahead of the []byte-as-string
        // fallback below — so net.IP (a named []byte with String()) prints "10.0.0.1",
        // not its raw bytes. The Println/Sprint path checks this in Format; the printf
        // path reaches StrVerb directly, so check here too.
        if (TryStringer(v, out var ss))
        {
            if (sp.Prec >= 0 && ss.Length > sp.Prec) ss = ss.Substring(0, sp.Prec);
            return ss;
        }
        // %s of a struct with no String() formats each field with %s (Go recurses the verb);
        // a pointer to such a struct prints &{...}.
        if (IsStructVal(v)) return RecurseStruct(v!, sp);
        if (v is GoPtr sgp && IsStructVal(GoPtrs.Get(sgp))) return "&" + RecurseStruct(GoPtrs.Get(sgp)!, sp);
        // %s applies to strings, errors, and composites; a bare number/bool/char
        // is a bad verb in Go (it has no string form).
        if (IsIntegral(v) || IsFloaty(v) || v is bool) return BadVerb('s', v);
        // %s of a []byte is its string form (Go treats []byte as a string). The typed
        // box carries "[]uint8", so a byte slice is distinguishable from []int.
        if (v is GoNamed bnm && bnm.Value is GoSlice bsl && (IsByteTag(Rt.NamedTypeName(bnm.TypeId)) || Rt.IsByteNamed(bnm.TypeId)))
            v = GoString.FromBytesOwned(SliceToBytes(bsl));
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

    private static bool IsByteTag(string name) => name == "[]uint8" || name == "[]byte";

    private static byte[] SliceToBytes(GoSlice s)
    {
        var b = new byte[s.Len];
        for (int i = 0; i < s.Len; i++) b[i] = (byte)ToLong(s.Data![s.Off + i]);
        return b;
    }

    private static string QuoteVerb(object? v) => QuoteVerb(v, false, false);
    private static string QuoteVerb(object? v, bool plus, bool sharp)
    {
        // A value implementing Stringer/error: %q quotes its String()/Error() result (Go
        // invokes the method first — even for a named integer like `type Color int`, %q
        // yields the quoted String(), not a rune literal).
        if (TryStringer(v, out var sv)) return GoQuote(GoString.FromDotNetString(sv), plus, sharp);
        // A []byte (e.g. a nested element of [][]byte) quotes as its string, like Go.
        if (v is GoNamed bnm && bnm.Value is GoSlice bbs && IsByteTag(Rt.NamedTypeName(bnm.TypeId)))
            return GoQuote(GoString.FromBytesOwned(SliceToBytes(bbs)), plus, sharp);
        // Unwrap any other typed box (a nested []string/[]int element of a [][]T carries
        // its identity as a GoNamed) so the slice/scalar branches below can recurse.
        if (v is GoNamed nmv) v = nmv.Value;
        if (v is GoString gq) return GoQuote(gq, plus, sharp);
        if (IsIntegral(v)) return (plus ? Strconv.QuoteRuneToASCII((int)ToLong(v)) : Strconv.QuoteRune((int)ToLong(v))).ToDotNetString();
        // %q over a slice quotes each element.
        if (v is GoSlice sl)
        {
            var sb = new StringBuilder("[");
            for (int i = 0; i < sl.Len; i++) { if (i > 0) sb.Append(' '); sb.Append(QuoteVerb(sl.Data![sl.Off + i], plus, sharp)); }
            return sb.Append(']').ToString();
        }
        // %q over a map quotes each key and value, keys sorted (like %v).
        if (v is GoMap qm)
        {
            if (qm.Data == null) return "map[]";
            var keys = new System.Collections.Generic.List<(string s, object? k)>();
            foreach (var k in qm.Data.Keys) keys.Add((MapKeySortStr(k), k));
            keys.Sort((a, b) => MapKeyCompare(a.k, b.k));
            var sb = new StringBuilder("map[");
            for (int i = 0; i < keys.Count; i++)
            { if (i > 0) sb.Append(' '); sb.Append(QuoteVerb(keys[i].k, plus, sharp)).Append(':').Append(QuoteVerb(qm.Data[keys[i].k!], plus, sharp)); }
            return sb.Append(']').ToString();
        }
        // %q over a struct quotes each field.
        if (IsStructVal(v))
        {
            var t = v!.GetType();
            var fields = t.GetFields(BindingFlags.Public | BindingFlags.Instance);
            var sb = new StringBuilder("{");
            for (int i = 0; i < fields.Length; i++)
            { if (i > 0) sb.Append(' '); sb.Append(QuoteVerb(RetagField(t.Name, fields[i].Name, fields[i].GetValue(v)), plus, sharp)); }
            return sb.Append('}').ToString();
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

    // %x/%X of a byte string/slice; the space flag (% x) puts a space between each byte pair.
    // %G uppercases the exponent marker only (e->E), leaving +Inf/NaN as-is.
    private static string UpperExp(string s) => s.IndexOf('e') >= 0 ? s.Replace('e', 'E') : s;

    // %g/%G core: the shortest (or precision-bounded) g form, with the '#' flag restoring
    // trailing zeros to `digits` significant figures (default 6) like Go's %#g, and %G
    // uppercasing the exponent.
    private static string GFormat(double d, Spec sp, bool isF32, bool upper)
    {
        string r = sp.Prec < 0 ? (isF32 ? GoFtoa.Shortest((float)d) : GoFtoa.Shortest(d)) : GoFtoa.FormatG(d, sp.Prec);
        if (sp.Hash) r = SharpG(r, sp.Prec < 0 ? 6 : sp.Prec);
        return upper ? UpperExp(r) : r;
    }

    // Go's %#g zero-padding: keep trailing zeros so the result shows `digits` significant
    // figures. Operates on a finite magnitude (FloatVerb handles Inf/NaN before this).
    private static string SharpG(string s, int digits)
    {
        string sign = "";
        if (s.Length > 0 && s[0] == '-') { sign = "-"; s = s.Substring(1); }
        string tail = "";
        int e = s.IndexOfAny(new[] { 'e', 'E' });
        if (e >= 0) { tail = s.Substring(e); s = s.Substring(0, e); }
        bool hasDot = s.IndexOf('.') >= 0;
        bool sawNonzero = false;
        int rem = digits;
        foreach (char c in s)
        {
            if (c == '.') continue;
            if (c != '0') sawNonzero = true;
            if (sawNonzero) rem--;
        }
        var sb = new StringBuilder(s);
        if (!hasDot)
        {
            if (s == "0") rem--; // Go: a leading digit 0 contributes once to the digit count
            sb.Append('.');
        }
        while (rem > 0) { sb.Append('0'); rem--; }
        sb.Append(tail);
        return sign + sb.ToString();
    }

    private static string HexStr(GoString s, bool upper, bool space, bool hash = false)
    {
        var by = s.Bytes;
        return HexBytes(i => by[i], by.Length, upper, space, hash);
    }

    private static string HexSlice(GoSlice s, bool upper, bool space, bool hash = false) =>
        HexBytes(i => (byte)ToLong(s.Data![s.Off + i]), s.Len, upper, space, hash);

    // Hex of a byte sequence for %x/%X. The '#' flag adds an 0x/0X prefix — once for the
    // whole run, or once per byte when combined with the space flag (Go: "0xde 0xad").
    private static string HexBytes(System.Func<int, byte> at, int len, bool upper, bool space, bool hash)
    {
        var sb = new StringBuilder();
        string prefix = upper ? "0X" : "0x";
        if (hash && !space && len > 0) sb.Append(prefix);
        for (int i = 0; i < len; i++)
        {
            if (space && i > 0) sb.Append(' ');
            if (hash && space) sb.Append(prefix);
            sb.Append(at(i).ToString(upper ? "X2" : "x2", Inv));
        }
        return sb.ToString();
    }

    // Go-style quoted string (%q): the byte-exact strconv.Quote (printable classification,
    // \a..\v shorthands, \xNN for invalid bytes, \u/\U for non-printable runes).
    private static string GoQuote(GoString gs) => GoQuote(gs, false, false);
    // %q flags: '#' prefers a raw-string literal (backquotes) when the value CanBackquote;
    // '+' escapes all non-ASCII (QuoteToASCII). The sharp/backquote test wins over plus.
    private static string GoQuote(GoString gs, bool plus, bool sharp)
    {
        if (sharp && Strconv.CanBackquote(gs)) return "`" + gs.ToDotNetString() + "`";
        return (plus ? Strconv.QuoteToASCII(gs) : Strconv.Quote(gs)).ToDotNetString();
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

    /// <summary>The Go type name of a boxed value (as %T renders it), for runtime
    /// messages such as a failed type assertion. Public so Rt can name the dynamic type.</summary>
    public static string TypeName(object? v) => GoTypeName(v);

    // Go type name for a boxed value (for %T and bad-verb messages). Slice/map
    // element types are erased at runtime, so those are approximate.
    private static string GoTypeName(object? v) => NormalizeByteRune(GoTypeNameRaw(v));

    // %T / reflect type names render the builtin aliases by their underlying type: a
    // byte is uint8 and a rune is int32 (go/types' "[]byte"/"[]rune" → "[]uint8"/"[]int32").
    private static readonly System.Text.RegularExpressions.Regex _byteRe =
        new System.Text.RegularExpressions.Regex(@"\bbyte\b", System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex _runeRe =
        new System.Text.RegularExpressions.Regex(@"\brune\b", System.Text.RegularExpressions.RegexOptions.Compiled);
    private static string NormalizeByteRune(string n)
    {
        if (n.IndexOf("byte", System.StringComparison.Ordinal) < 0 &&
            n.IndexOf("rune", System.StringComparison.Ordinal) < 0) return n;
        n = _byteRe.Replace(n, "uint8");
        n = _runeRe.Replace(n, "int32");
        return n;
    }

    private static string GoTypeNameRaw(object? v) => v switch
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
        Errors.JoinError => "*errors.joinError",       // errors.Join
        GoError gm when gm.Multi != null => "*fmt.wrapErrors", // fmt.Errorf with 2+ %w
        GoError ge when ge.Wrapped != null => "*fmt.wrapError", // fmt.Errorf with a single %w
        IGoError => "*errors.errorString",             // errors.New / fmt.Errorf without %w
        GoComplex => "complex128",
        GoSlice => "[]interface {}",
        GoMap => "map[string]interface {}",
        GoPtr p => PtrTypeName(p),
        GoNamed nm => NamedTypeNameOr(nm),
        _ => StructTypeName(v),
    };

    // %T of a plain struct value: a generic instantiation has a registered reflect
    // display name ("main.Pair[string,int]"); a non-generic struct's CLR name already
    // matches its Go name, so "main." + the type name.
    private static string StructTypeName(object v)
    {
        if (ShimTypes.GoNameOf(v) is string shimName) return shimName; // e.g. GoXmlName -> xml.Name
        var d = Rt.StructDisplay(v.GetType().Name);
        return d.Length > 0 ? d : "main." + v.GetType().Name;
    }

    // The pointee type name of a GoPtr for %T: prefer the pointee's registered named/struct
    // identity (so *Color reports main.Color, not the underlying int), falling back to the
    // boxed pointee value's type when the pointer carries no id (a nil pointer, *int, …).
    private static string PtrPointeeName(GoPtr p)
    {
        if (p.TypeId != 0)
        {
            var n = Rt.NamedTypeName(p.TypeId);
            if (n.Length > 0) return n;
        }
        return GoTypeName(p.Value);
    }

    // The full %T name of a pointer: its stamped "*T" display name if boxing recorded one
    // (precise even for a method-less pointee), else "*" + the pointee name.
    private static string PtrTypeName(GoPtr p)
    {
        if (p.PtrName != 0)
        {
            var n = Rt.NamedTypeName(p.PtrName);
            if (n.Length > 0) return n;
        }
        return "*" + PtrPointeeName(p);
    }

    // %T of a typed-box value: its registered Go display name (e.g. "main.Money",
    // "sort.StringSlice"), falling back to the underlying value's name.
    private static string NamedTypeNameOr(GoNamed nm)
    {
        var name = Rt.NamedTypeName(nm.TypeId);
        return name.Length > 0 ? name : GoTypeName(nm.Value);
    }

    private static string Format(object? v, char verb, bool plus, bool hash)
    {
        if (v is string vraw) v = GoString.FromDotNetString(vraw); // a shim struct's raw C# string field
        v = MaybeRetag(v); // re-tag a []Stringer's bare elements (Sprint/Println path)
        // fmt.Formatter governs the Print/Println/Sprint path too (verb 'v', no flags).
        if (TryFormatter(v, new Spec { Verb = verb, Width = -1, Prec = -1, Plus = plus, Hash = hash }, out var ffr)) return ffr;
        // *big.Int and *big.Float implement fmt.Formatter: under %v they honor the '+' flag as a
        // sign for a non-negative value (unlike a plain scalar, whose %+v adds field names only).
        if (plus && verb == 'v' && (v is GoBigInt || v is GoBigFloat))
        {
            string bs = v is GoBigInt gbi ? Big.Int_String(gbi).ToDotNetString() : Big.Float_String((GoBigFloat)v).ToDotNetString();
            return bs.Length > 0 && bs[0] != '-' ? "+" + bs : bs;
        }
        if (v is GoBigFloat bfv) v = bfv.V; // *big.Float (double-backed) prints like its value
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
            case float fl: return GoFtoa.Shortest(fl);
            case double d: return FormatFloatV(d);
            case GoString gs: return gs.ToDotNetString();
            case IGoError e: return e.Error().ToDotNetString();
            case GoComplex c: return "(" + FormatFloatV(c.Re) + ComplexImag(c.Im) + "i)";
            // Go prints &{...}/&[...]/&map[...] for pointers to a composite, but a
            // hex address for a pointer to a scalar.
            case GoPtr p when GoPtrs.Get(p) is var pv && (pv is GoSlice || pv is GoMap || IsStructVal(pv)):
                return "&" + Format(pv, verb, plus, hash);
            // A pointer whose cell holds null (a nil pointer boxed into an interface, or a
            // pointer to a nil interface) prints <nil> like Go — these are the only Value==null
            // GoPtrs, and Go would otherwise print a non-deterministic address (never byte-exact).
            case GoPtr p when p.Value is null && p.Arr is null && p.FGet is null: return "<nil>";
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
        // fmt.GoStringer: a user GoString() controls the %#v representation (a shim type
        // like time.Time carries no bridge adapter and falls through to its own case).
        if (v != null && Bridge.HasMethod(v, "GoString") && Bridge.CallMethod(v, "GoString") is GoString gstr)
            return gstr.ToDotNetString();
        if (v is string vraw) v = GoString.FromDotNetString(vraw); // a shim struct's raw C# string field
        switch (v)
        {
            case null: return "<nil>";
            case bool b: return b ? "true" : "false";
            case GoTime: return Time.Time_GoString(v).ToDotNetString(); // GoStringer
            case GoString gs: return GoQuote(gs);
            case ulong ul: return "0x" + ul.ToString("x", Inv); // Go's %#v renders unsigned ints in hex
            case uint ui: return "0x" + ui.ToString("x", Inv);
            case long or int: return Format(v, 'v', false, false);
            case double d: return FormatFloatV(d);
            case GoComplex c: return "(" + FormatFloatV(c.Re) + ComplexImag(c.Im) + "i)";
            case GoPtr p: return "&" + FormatGoSyntax(GoPtrs.Get(p)); // resolve field/element-alias pointers, not just p.Value
            // A typed box carries the precise composite type, so %#v can spell its real
            // element types ("[]int{...}", "main.IntHeap{...}") instead of the erased
            // "[]interface {}". A named scalar (Celsius) just renders its value.
            case GoNamed nm:
            {
                string nmName = Rt.NamedTypeName(nm.TypeId);
                // A nil typed pointer wrapped for its type identity: Go's %#v is "(*int)(nil)".
                if (nm.Value == null) return nmName.Length > 0 ? "(" + nmName + ")(nil)" : "<nil>";
                if (nmName.Length == 0) return FormatGoSyntax(nm.Value);
                return nm.Value switch
                {
                    GoSlice sl => SliceSyntax(sl, nmName),
                    GoMap m => MapSyntax(m, nmName),
                    _ => FormatGoSyntax(nm.Value),
                };
            }
            case GoSlice sl: return SliceSyntax(sl, "[]interface {}");
            case GoMap m: return MapSyntax(m, "map[string]interface {}");
            default:
            {
                var t = v.GetType();
                var fields = t.GetFields(BindingFlags.Public | BindingFlags.Instance);
                // An anonymous/generic struct has a registered reflect display name
                // ("struct { A int; B string }"); a plain named struct is "main.T".
                // A shim struct (xml.Name, …) names its Go type; else a registered reflect
                // display ("struct { … }" / "main.T"), else "main.T".
                string disp = ShimTypes.GoNameOf(v!) ?? Rt.StructDisplay(t.Name);
                var sb = new StringBuilder((disp.Length > 0 ? disp : "main." + t.Name) + "{");
                for (int i = 0; i < fields.Length; i++)
                {
                    if (i > 0) sb.Append(", ");
                    var fv = fields[i].GetValue(v);
                    sb.Append(fields[i].Name).Append(':');
                    // A nil map field is a bare null with no value to re-tag, but its static
                    // type name is registered — render map[K]V(nil) like Go rather than <nil>.
                    if (fv is null)
                    {
                        string fn = Rt.NamedTypeName(Rt.FieldTypeId(t.Name, fields[i].Name));
                        sb.Append(fn.StartsWith("map[", System.StringComparison.Ordinal) ? fn + "(nil)" : FormatGoSyntax(fv));
                    }
                    else sb.Append(FormatGoSyntax(RetagField(t.Name, fields[i].Name, fv)));
                }
                return sb.Append('}').ToString();
            }
        }
    }

    // %#v body for a slice/map with a given Go type-name prefix ("[]int", "main.IntHeap").
    // The element/key/value type names are derived from typeName and propagated, so a
    // nested composite ([][]int, map[int][]string) prints its real inner types rather
    // than the erased "[]interface {}".
    private static string SliceSyntax(GoSlice sl, string typeName)
    {
        // A nil slice renders as `[]T(nil)`, distinct from an empty `[]T{}` (Go's %#v).
        if (sl.Data == null) return typeName + "(nil)";
        string elemName = SliceElemName(typeName);
        var sb = new StringBuilder(typeName).Append('{');
        for (int i = 0; i < sl.Len; i++) { if (i > 0) sb.Append(", "); sb.Append(GoSyntaxElem(sl.Data![sl.Off + i], elemName)); }
        return sb.Append('}').ToString();
    }

    private static string MapSyntax(GoMap m, string typeName)
    {
        // A nil map renders as `map[K]V(nil)`, distinct from an empty `map[K]V{}`.
        if (m.Data == null) return typeName + "(nil)";
        var (keyName, valName) = MapKVNames(typeName);
        var sb = new StringBuilder(typeName).Append('{');
        var keys = new System.Collections.Generic.List<(string s, object? k)>();
        if (m.Data != null) foreach (var k in m.Data.Keys) keys.Add((MapKeySortStr(k), k));
        keys.Sort((a, b) => MapKeyCompare(a.k, b.k));
        for (int i = 0; i < keys.Count; i++) { if (i > 0) sb.Append(", "); sb.Append(GoSyntaxElem(keys[i].k, keyName)).Append(':').Append(GoSyntaxElem(m.Data![keys[i].k!], valName)); }
        return sb.Append('}').ToString();
    }

    // Formats a composite element with its derived type name (so it carries its own
    // prefix); a scalar/string/struct element falls to the normal %#v.
    private static string GoSyntaxElem(object? v, string typeName)
    {
        if (v is GoSlice sl && typeName.Length > 0 && typeName[0] == '[') return SliceSyntax(sl, typeName);
        if (v is GoMap m && typeName.StartsWith("map[", System.StringComparison.Ordinal)) return MapSyntax(m, typeName);
        // []byte / [N]byte elements render as hex bytes (Go's %#v: []byte{0x68, 0x69}).
        if ((typeName == "byte" || typeName == "uint8") && IsIntegral(v)) return "0x" + ((ulong)(ToLong(v) & 0xff)).ToString("x", Inv);
        return FormatGoSyntax(v);
    }

    // SliceElemName extracts the element type of a slice/array type name: "[]int" -> "int",
    // "[][]int" -> "[]int", "[3]int" -> "int". "" when not a slice/array spelling.
    private static string SliceElemName(string t)
    {
        if (t.StartsWith("[]", System.StringComparison.Ordinal)) return t.Substring(2);
        if (t.Length > 0 && t[0] == '[') { int j = t.IndexOf(']'); if (j > 0) return t.Substring(j + 1); }
        return "";
    }

    // MapKVNames splits "map[K]V" into (K, V), matching the bracket that closes the key
    // even when K is itself a composite (map[[2]int]string).
    private static (string, string) MapKVNames(string t)
    {
        if (!t.StartsWith("map[", System.StringComparison.Ordinal)) return ("", "");
        int depth = 0, i = 3;
        for (; i < t.Length; i++) { if (t[i] == '[') depth++; else if (t[i] == ']' && --depth == 0) break; }
        return i < t.Length ? (t.Substring(4, i - 4), t.Substring(i + 1)) : ("", "");
    }

    private static string FormatFloatV(double d) => GoFtoa.Shortest(d);

    // The imaginary part of a complex always carries an explicit sign (Go formats it with
    // plus forced). FormatFloatV already yields a leading sign for negatives, -0, ±Inf;
    // only finite non-negatives and NaN need a '+' prepended.
    private static string ComplexImag(double im)
    {
        string s = FormatFloatV(im);
        return s.Length > 0 && (s[0] == '+' || s[0] == '-') ? s : "+" + s;
    }

    // A complex under a float verb (%f/%e/%g/...) formats as "(re±imi)", each part via the
    // given per-double formatter; the imaginary part always carries an explicit sign.
    private static string ComplexVerb(GoComplex c, System.Func<double, string> fmt)
    {
        string re = fmt(c.Re), im = fmt(c.Im);
        if (im.Length == 0 || (im[0] != '+' && im[0] != '-')) im = "+" + im;
        return "(" + re + im + "i)";
    }

    private static string FormatSlice(GoSlice s, bool plus, bool hash)
    {
        var sb = new StringBuilder("[");
        for (int i = 0; i < s.Len; i++) { if (i > 0) sb.Append(' '); sb.Append(Format(s.Data[s.Off + i], 'v', plus, hash)); }
        return sb.Append(']').ToString();
    }

    // A null name (a bare slice, type erased) defaults to byte semantics — the common
    // %x case is hex-encoding a []byte; a []uint8/[]byte tag formats as a hex string,
    // any other slice type recurses element-wise.
    // Whether a slice/array static type is a byte sequence — so %x/%X/%q format it as
    // contiguous (zero-padded) hex / a quoted string. Matches []byte and a fixed-size
    // [N]byte array (sha512.Sum512 returns [64]byte, etc.).
    private static bool IsByteSliceName(string? name)
    {
        // A real []byte is always typed-boxed as "[]uint8"; an untagged slice (null name)
        // is NOT a byte slice (e.g. a bare []interface {} or an element-erased slice), so
        // %x/%q must recurse element-wise rather than treat it as raw bytes.
        if (name == null) return false;
        if (name == "[]uint8" || name == "[]byte") return true;
        if (name.Length > 3 && name[0] == '[')
        {
            int i = 1;
            while (i < name.Length && name[i] >= '0' && name[i] <= '9') i++;
            if (i > 1 && i < name.Length && name[i] == ']')
            {
                string elem = name.Substring(i + 1);
                return elem == "uint8" || elem == "byte";
            }
        }
        return false;
    }

    // Element-wise verb recursion: each element is formatted with the same verb/flags,
    // the way Go applies a scalar verb across a slice/array ("[1 2]") or map ("map[k:v]").
    private static string RecurseSlice(GoSlice s, Spec sp)
    {
        if (s.Data == null) return "[]";
        var sb = new StringBuilder("[");
        for (int i = 0; i < s.Len; i++) { if (i > 0) sb.Append(' '); sb.Append(FmtElem(s.Data[s.Off + i], sp)); }
        return sb.Append(']').ToString();
    }

    // A numeric/string verb applied to a struct formats each field with that same verb,
    // like Go (%d of {1 "x"} -> "{1 %!d(string=x)}"). Field names are not shown (only %+v
    // does that). The verb's Stringer dispatch, when applicable, is handled by the caller.
    private static string RecurseStruct(object v, Spec sp)
    {
        var t = v.GetType();
        var fields = t.GetFields(BindingFlags.Public | BindingFlags.Instance);
        var sb = new StringBuilder("{");
        for (int i = 0; i < fields.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            object? fv = fields[i].GetValue(v);
            if (fv == null && fields[i].FieldType == typeof(GoMap)) fv = NilMapValue;
            fv = RetagField(t.Name, fields[i].Name, fv);
            sb.Append(FmtElem(fv, sp));
        }
        return sb.Append('}').ToString();
    }

    private static string RecurseMap(GoMap m, Spec sp)
    {
        if (m.Data == null) return "map[]";
        var keys = new System.Collections.Generic.List<(string s, object? k)>();
        foreach (var k in m.Data.Keys) keys.Add((MapKeySortStr(k), k));
        keys.Sort((a, b) => MapKeyCompare(a.k, b.k));
        var sb = new StringBuilder("map[");
        for (int i = 0; i < keys.Count; i++)
        { if (i > 0) sb.Append(' '); sb.Append(FmtElem(keys[i].k, sp)).Append(':').Append(FmtElem(m.Data[keys[i].k!], sp)); }
        return sb.Append(']').ToString();
    }

    private static string FormatMap(GoMap m, bool plus, bool hash)
    {
        var keys = new System.Collections.Generic.List<(string s, object? k)>();
        foreach (var k in m.Data.Keys) keys.Add((MapKeySortStr(k), k));
        keys.Sort((a, b) => MapKeyCompare(a.k, b.k));
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
            object? fv = fields[i].GetValue(v);
            // A struct's nil map field is the CLR default (null), not a GoMap{Data:null};
            // render it as map[] (Go) rather than <nil>.
            if (fv == null && fields[i].FieldType == typeof(GoMap)) fv = NilMapValue;
            fv = RetagField(t.Name, fields[i].Name, fv); // re-tag a Stringer/typed field value
            sb.Append(Format(fv, 'v', plus, hash));
        }
        return sb.Append('}').ToString();
    }

    private static readonly GoMap NilMapValue = new();

}
