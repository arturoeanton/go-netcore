namespace GoCLR.Stdlib;

using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.Json;
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
            // A nil slice/map (zero value: null backing) marshals as null, not []/{}.
            case GoSlice s when s.Data == null: sb.Append("null"); break;
            case GoMap m when m.Data == null: sb.Append("null"); break;
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

    // ---- Unmarshal ---------------------------------------------------------

    /// <summary>json.Unmarshal(data, &amp;v). desc is a compact JSON descriptor of
    /// v's static type (emitted by the compiler, since the runtime erases slice/map
    /// element types). The decoded value is written back through the GoPtr cell.</summary>
    public static object? Unmarshal(GoSlice data, object? target, GoString desc)
    {
        try
        {
            string json = SliceToString(data);
            using var doc = JsonDocument.Parse(json);
            using var ddoc = JsonDocument.Parse(desc.ToDotNetString());
            object? decoded = Decode(doc.RootElement, ddoc.RootElement);
            SetPtr(target, decoded);
            return null;
        }
        catch (System.Exception e)
        {
            return new GoError(GoString.FromDotNetString(e.Message));
        }
    }

    private static string SliceToString(GoSlice s)
    {
        var b = new byte[s.Len];
        for (int i = 0; i < s.Len; i++) b[i] = (byte)System.Convert.ToInt64(s.Data[s.Off + i]);
        return Encoding.UTF8.GetString(b);
    }

    private static void SetPtr(object? target, object? value)
    {
        if (target == null) throw new System.Exception("json: Unmarshal(nil)");
        var vf = target.GetType().GetField("Value");
        if (vf == null) throw new System.Exception("json: Unmarshal(non-pointer)");
        vf.SetValue(target, Coerce(value, vf.FieldType));
    }

    // Decode a JSON element into the canonical boxed Go value for a descriptor.
    private static object? Decode(JsonElement j, JsonElement desc)
    {
        string k = desc.GetProperty("k").GetString() ?? "any";
        if (j.ValueKind == JsonValueKind.Null) return DefaultFor(k);
        switch (k)
        {
            case "bool": return j.GetBoolean();
            case "int": return j.TryGetInt64(out long li) ? li : (long)j.GetDouble();
            case "uint": return j.TryGetUInt64(out ulong ui) ? ui : (ulong)j.GetDouble();
            case "float": return j.GetDouble();
            case "string": return GoString.FromDotNetString(j.GetString() ?? "");
            case "bytes":
            {
                var raw = System.Convert.FromBase64String(j.GetString() ?? "");
                var d = new object?[raw.Length];
                for (int i = 0; i < raw.Length; i++) d[i] = (int)raw[i];
                return new GoSlice { Data = d, Off = 0, Len = raw.Length, Cap = raw.Length };
            }
            case "ptr": return Decode(j, desc.GetProperty("e"));
            case "slice":
            {
                var et = desc.GetProperty("e");
                int n = j.GetArrayLength();
                var d = new object?[n];
                int idx = 0;
                foreach (var el in j.EnumerateArray()) d[idx++] = Decode(el, et);
                return new GoSlice { Data = d, Off = 0, Len = n, Cap = n };
            }
            case "map":
            {
                var vt = desc.GetProperty("v");
                var m = GoMaps.Make();
                foreach (var prop in j.EnumerateObject())
                    m.Data![GoString.FromDotNetString(prop.Name)] = Decode(prop.Value, vt);
                return m;
            }
            case "struct": return DecodeStruct(j, desc);
            default: return DecodeAny(j);
        }
    }

    private static object? DecodeStruct(JsonElement j, JsonElement desc)
    {
        string cname = desc.GetProperty("n").GetString() ?? "";
        var t = ResolveType(cname);
        if (t == null) throw new System.Exception("json: unknown type " + cname);
        object inst = System.Activator.CreateInstance(t)!;
        // index JSON members case-insensitively (Go matches that way)
        var members = new Dictionary<string, JsonElement>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var p in j.EnumerateObject()) members[p.Name] = p.Value;
        foreach (var fd in desc.GetProperty("f").EnumerateArray())
        {
            string jkey = fd.GetProperty("j").GetString() ?? "";
            if (!members.TryGetValue(jkey, out var jv)) continue;
            string cfield = fd.GetProperty("c").GetString() ?? "";
            var fi = t.GetField(cfield);
            if (fi == null) continue;
            object? val = Decode(jv, fd.GetProperty("t"));
            fi.SetValue(inst, Coerce(val, fi.FieldType));
        }
        return inst;
    }

    // Generic decode for interface{} targets — mirrors Go's default mapping.
    private static object? DecodeAny(JsonElement j)
    {
        switch (j.ValueKind)
        {
            case JsonValueKind.True: return true;
            case JsonValueKind.False: return false;
            case JsonValueKind.Number: return j.GetDouble();
            case JsonValueKind.String: return GoString.FromDotNetString(j.GetString() ?? "");
            case JsonValueKind.Array:
            {
                int n = j.GetArrayLength();
                var d = new object?[n];
                int i = 0;
                foreach (var el in j.EnumerateArray()) d[i++] = DecodeAny(el);
                return new GoSlice { Data = d, Off = 0, Len = n, Cap = n };
            }
            case JsonValueKind.Object:
            {
                var m = GoMaps.Make();
                foreach (var p in j.EnumerateObject()) m.Data![GoString.FromDotNetString(p.Name)] = DecodeAny(p.Value);
                return m;
            }
            default: return null;
        }
    }

    private static object? DefaultFor(string k) => k switch
    {
        "int" => (long)0, "uint" => (ulong)0, "float" => (double)0, "bool" => false, _ => null,
    };

    // Coerce a canonical boxed value to the concrete CLR field type (handles int
    // widths and pointer wrapping).
    private static object? Coerce(object? v, System.Type target)
    {
        if (v == null) return target.IsValueType ? System.Activator.CreateInstance(target) : null;
        if (target.IsInstanceOfType(v)) return v;
        if (target.IsGenericType && target.GetGenericTypeDefinition() == typeof(GoPtr<>))
        {
            var elem = target.GetGenericArguments()[0];
            return System.Activator.CreateInstance(target, Coerce(v, elem));
        }
        if (target == typeof(long) || target == typeof(int) || target == typeof(short) || target == typeof(sbyte)
            || target == typeof(ulong) || target == typeof(uint) || target == typeof(ushort) || target == typeof(byte))
            return System.Convert.ChangeType(v, target);
        if (target == typeof(double) || target == typeof(float))
            return System.Convert.ChangeType(v, target);
        return v;
    }

    private static readonly Dictionary<string, System.Type?> TypeCache = new();
    private static System.Type? ResolveType(string name)
    {
        if (TypeCache.TryGetValue(name, out var cached)) return cached;
        System.Type? found = null;
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            found = asm.GetType(name);
            if (found != null) break;
        }
        TypeCache[name] = found;
        return found;
    }
}
