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

    public static object?[] Print(GoSlice args) { var s = Sprint(args); System.Console.Out.Write(s.ToDotNetString()); return new object?[] { (long)s.Len, null }; }
    public static object?[] Println(GoSlice args) { var s = Sprintln(args); System.Console.Out.Write(s.ToDotNetString()); return new object?[] { (long)s.Len, null }; }
    public static object?[] Printf(GoString format, GoSlice args) { var s = DoSprintf(format.ToDotNetString(), Args(args)); System.Console.Out.Write(s); return new object?[] { (long)Encoding.UTF8.GetByteCount(s), null }; }

    public static object Errorf(GoString format, GoSlice args) =>
        new GoError(GoString.FromDotNetString(DoSprintf(format.ToDotNetString(), Args(args))));

    private static string DoSprintf(string f, object?[] args)
    {
        var sb = new StringBuilder();
        int ai = 0;
        for (int i = 0; i < f.Length; i++)
        {
            if (f[i] != '%') { sb.Append(f[i]); continue; }
            i++;
            if (i >= f.Length) break;
            bool plus = false, hash = false;
            while (i < f.Length && (f[i] == '+' || f[i] == '#' || f[i] == '-' || f[i] == '0' || f[i] == ' '))
            { if (f[i] == '+') plus = true; if (f[i] == '#') hash = true; i++; }
            // precision/width digits and '.' (ignored except for floats)
            int dotPrec = -1;
            while (i < f.Length && char.IsDigit(f[i])) i++;
            if (i < f.Length && f[i] == '.') { i++; int ps = i; while (i < f.Length && char.IsDigit(f[i])) i++; if (i > ps) dotPrec = int.Parse(f.Substring(ps, i - ps)); }
            if (i >= f.Length) break;
            char verb = f[i];
            if (verb == '%') { sb.Append('%'); continue; }
            object? arg = ai < args.Length ? args[ai++] : null;
            sb.Append(FormatVerb(arg, verb, plus, hash, dotPrec));
        }
        return sb.ToString();
    }

    private static string FormatVerb(object? v, char verb, bool plus, bool hash, int prec) => verb switch
    {
        'd' => System.Convert.ToInt64(v ?? 0L).ToString(Inv),
        'b' => System.Convert.ToString(System.Convert.ToInt64(v ?? 0L), 2),
        'o' => System.Convert.ToString(System.Convert.ToInt64(v ?? 0L), 8),
        'x' => v is GoString gx ? HexStr(gx, false) : System.Convert.ToInt64(v ?? 0L).ToString("x", Inv),
        'X' => v is GoString gX ? HexStr(gX, true) : System.Convert.ToInt64(v ?? 0L).ToString("X", Inv),
        't' => (v is bool bb && bb) ? "true" : "false",
        's' => Format(v, 'v', plus, hash),
        'q' => "\"" + (v is GoString gq ? gq.ToDotNetString() : Format(v, 'v', false, false)) + "\"",
        'c' => char.ConvertFromUtf32((int)System.Convert.ToInt64(v ?? 0L)),
        'f' => System.Convert.ToDouble(v ?? 0.0).ToString("F" + (prec < 0 ? 6 : prec), Inv),
        'e' => System.Convert.ToDouble(v ?? 0.0).ToString("e" + (prec < 0 ? 6 : prec), Inv),
        'g' => System.Convert.ToDouble(v ?? 0.0).ToString("R", Inv),
        'T' => TypeName(v),
        'v' => Format(v, 'v', plus, hash),
        _ => Format(v, 'v', plus, hash),
    };

    private static string HexStr(GoString s, bool upper)
    {
        var sb = new StringBuilder();
        foreach (byte b in s.Bytes) sb.Append(b.ToString(upper ? "X2" : "x2", Inv));
        return sb.ToString();
    }

    private static string Format(object? v, char verb, bool plus, bool hash)
    {
        switch (v)
        {
            case null: return "<nil>";
            case bool b: return b ? "true" : "false";
            case long l: return l.ToString(Inv);
            case int i: return i.ToString(Inv);
            case ulong u: return u.ToString(Inv);
            case double d: return FormatFloatV(d);
            case GoString gs: return gs.ToDotNetString();
            case IGoError e: return e.Error().ToDotNetString();
            case GoComplex c: return "(" + FormatFloatV(c.Re) + (c.Im < 0 ? "-" : "+") + FormatFloatV(System.Math.Abs(c.Im)) + "i)";
            case GoPtr p: return "&" + Format(p.Value, verb, plus, hash);
            case GoSlice sl: return FormatSlice(sl, plus, hash);
            case GoMap m: return FormatMap(m, plus, hash);
            default: return FormatStruct(v, plus, hash);
        }
    }

    private static string FormatFloatV(double d) =>
        d == System.Math.Floor(d) && !double.IsInfinity(d) && System.Math.Abs(d) < 1e21
            ? d.ToString("0", Inv) : d.ToString("R", Inv);

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

    private static string TypeName(object? v) => v switch
    {
        null => "<nil>", bool => "bool", long => "int", double => "float64",
        GoString => "string", GoSlice => "[]", GoMap => "map", GoPtr => "*",
        _ => v.GetType().Name,
    };
}
