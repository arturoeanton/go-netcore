namespace GoCLR.Stdlib;

using System.Reflection;
using System.Text;
using GoCLR.Runtime;

/// <summary>Shim for a subset of Go's <c>encoding/json</c> (Marshal). Uses .NET
/// reflection over the boxed value plus the registered struct tags. Map keys are
/// sorted (as Go does); struct fields use their json tag (name, omitempty, -).</summary>
public static class Json
{
    public static object?[] Marshal(object? v)
    {
        var sb = new StringBuilder();
        try { Write(sb, v); }
        catch (System.Exception e) { return new object?[] { default(GoSlice), new GoError(GoString.FromDotNetString("json: " + e.Message)) }; }
        return new object?[] { Bytes(sb.ToString()), null };
    }

    private static GoSlice Bytes(string s)
    {
        var b = Encoding.UTF8.GetBytes(s);
        var d = new object?[b.Length];
        for (int i = 0; i < b.Length; i++) d[i] = (int)b[i];
        return new GoSlice { Data = d, Off = 0, Len = b.Length, Cap = b.Length };
    }

    private static void Write(StringBuilder sb, object? v)
    {
        switch (v)
        {
            case null: sb.Append("null"); break;
            case bool b: sb.Append(b ? "true" : "false"); break;
            case long l: sb.Append(l.ToString(System.Globalization.CultureInfo.InvariantCulture)); break;
            case int i: sb.Append(i.ToString(System.Globalization.CultureInfo.InvariantCulture)); break;
            case ulong u: sb.Append(u.ToString(System.Globalization.CultureInfo.InvariantCulture)); break;
            case double d: WriteNumber(sb, d); break;
            case GoString gs: WriteString(sb, gs.ToDotNetString()); break;
            case GoPtr p: Write(sb, p.Value); break;
            case GoSlice s: WriteSlice(sb, s); break;
            case GoMap m: WriteMap(sb, m); break;
            default: WriteStruct(sb, v); break;
        }
    }

    private static void WriteNumber(StringBuilder sb, double d)
    {
        if (d == System.Math.Floor(d) && !double.IsInfinity(d))
            sb.Append(((long)d).ToString(System.Globalization.CultureInfo.InvariantCulture));
        else
            sb.Append(d.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
    }

    private static void WriteString(StringBuilder sb, string s)
    {
        sb.Append('"');
        foreach (char c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\t': sb.Append("\\t"); break;
                case '\r': sb.Append("\\r"); break;
                case '<': sb.Append("\\u003c"); break;
                case '>': sb.Append("\\u003e"); break;
                case '&': sb.Append("\\u0026"); break;
                default:
                    if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
    }

    private static void WriteSlice(StringBuilder sb, GoSlice s)
    {
        sb.Append('[');
        for (int i = 0; i < s.Len; i++)
        {
            if (i > 0) sb.Append(',');
            Write(sb, s.Data[s.Off + i]);
        }
        sb.Append(']');
    }

    private static void WriteMap(StringBuilder sb, GoMap m)
    {
        sb.Append('{');
        var keys = new System.Collections.Generic.List<string>();
        var byKey = new System.Collections.Generic.Dictionary<string, object?>();
        foreach (var k in m.Data.Keys)
        {
            string ks = k is GoString g ? g.ToDotNetString() : k?.ToString() ?? "";
            keys.Add(ks); byKey[ks] = m.Data[k];
        }
        keys.Sort(System.StringComparer.Ordinal);
        for (int i = 0; i < keys.Count; i++)
        {
            if (i > 0) sb.Append(',');
            WriteString(sb, keys[i]); sb.Append(':'); Write(sb, byKey[keys[i]]);
        }
        sb.Append('}');
    }

    private static void WriteStruct(StringBuilder sb, object v)
    {
        var t = v.GetType();
        var fields = t.GetFields(BindingFlags.Public | BindingFlags.Instance);
        sb.Append('{');
        bool first = true;
        foreach (var f in fields)
        {
            if (f.Name.Length == 0 || !char.IsUpper(f.Name[0])) continue; // unexported
            string tag = Reflect.TagGet(Reflect.TagFor(t.Name, f.Name), "json");
            string name = f.Name;
            bool omitempty = false;
            if (tag.Length > 0)
            {
                var parts = tag.Split(',');
                if (parts[0] == "-") continue;
                if (parts[0].Length > 0) name = parts[0];
                for (int i = 1; i < parts.Length; i++) if (parts[i] == "omitempty") omitempty = true;
            }
            var val = f.GetValue(v);
            if (omitempty && IsEmpty(val)) continue;
            if (!first) sb.Append(',');
            first = false;
            WriteString(sb, name); sb.Append(':'); Write(sb, val);
        }
        sb.Append('}');
    }

    private static bool IsEmpty(object? v) => v switch
    {
        null => true,
        bool b => !b,
        long l => l == 0,
        double d => d == 0,
        GoString s => s.Len == 0,
        GoSlice sl => sl.Len == 0,
        GoMap m => m.Data.Count == 0,
        _ => false,
    };
}
