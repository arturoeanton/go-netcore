namespace GoCLR.Stdlib;

using System.Buffers;
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

    // json.MarshalIndent(v, prefix, indent): compact-marshal, then re-indent.
    public static object?[] MarshalIndent(object? v, GoString prefix, GoString indent)
    {
        var sb = new StringBuilder();
        try { Write(sb, v); }
        catch (System.Exception e) { return new object?[] { default(GoSlice), new GoError(GoString.FromDotNetString("json: " + e.Message)) }; }
        return new object?[] { Bytes(IndentJson(sb.ToString(), prefix.ToDotNetString(), indent.ToDotNetString())), null };
    }

    // Re-indents compact JSON the way Go's json.Indent does: a newline + prefix +
    // depth*indent after '{'/'['/',', before '}'/']', and ": " after object keys.
    // Empty {} and [] stay on one line. String contents are passed through verbatim.
    private static string IndentJson(string s, string prefix, string indent)
    {
        var sb = new StringBuilder();
        int depth = 0;
        bool inStr = false;
        void NewLine()
        {
            sb.Append('\n').Append(prefix);
            for (int k = 0; k < depth; k++) sb.Append(indent);
        }
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (inStr)
            {
                sb.Append(c);
                if (c == '\\' && i + 1 < s.Length) sb.Append(s[++i]);
                else if (c == '"') inStr = false;
                continue;
            }
            switch (c)
            {
                case '"': inStr = true; sb.Append(c); break;
                case '{':
                case '[':
                    if (i + 1 < s.Length && (s[i + 1] == '}' || s[i + 1] == ']')) { sb.Append(c).Append(s[++i]); break; }
                    sb.Append(c); depth++; NewLine(); break;
                case '}':
                case ']': depth--; NewLine(); sb.Append(c); break;
                case ',': sb.Append(c); NewLine(); break;
                case ':': sb.Append(": "); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
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
            // time.Time marshals as its RFC3339 string (Time.MarshalJSON), not the raw struct.
            case GoTime gt: sb.Append(Time.JsonText(gt)); break;
            case GoNamed n: // typed box: special-case json's named types, else marshal underlying
            {
                string tn = Rt.NamedTypeName(n.TypeId);
                if (tn == "json.Number") { WriteRawNumber(sb, n.Value); break; }
                if (tn == "json.RawMessage") { WriteRawMessage(sb, n.Value); break; }
                Write(sb, n.Value);
                break;
            }
            case GoPtr p: Write(sb, GoPtrs.Get(p)); break;
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
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '<': sb.Append(_noEscapeHTML ? "<" : "\\u003c"); break;
                case '>': sb.Append(_noEscapeHTML ? ">" : "\\u003e"); break;
                case '&': sb.Append(_noEscapeHTML ? "&" : "\\u0026"); break;
                default:
                    if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                    // U+2028/U+2029 are valid JSON but break JavaScript, so Go always
                    // escapes them regardless of SetEscapeHTML.
                    else if (c == '\u2028') sb.Append("\\u2028");
                    else if (c == '\u2029') sb.Append("\\u2029");
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
        sb.Append('{');
        bool first = true;
        WriteStructFields(sb, v, ref first);
        sb.Append('}');
    }

    // Emit a struct's fields, promoting embedded (anonymous) fields inline.
    private static void WriteStructFields(StringBuilder sb, object v, ref bool first)
    {
        var t = v.GetType();
        foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (f.Name.Length == 0 || !char.IsUpper(f.Name[0])) continue; // unexported
            string rawTag = Reflect.TagFor(t.Name, f.Name);
            string tag = Reflect.TagGet(rawTag, "json");
            // Embedded struct (field name == its type name) with no json tag: flatten.
            var val = f.GetValue(v);
            if (tag.Length == 0 && f.Name == f.FieldType.Name && val != null && f.FieldType.IsValueType && IsGoStruct(f.FieldType))
            {
                WriteStructFields(sb, val, ref first);
                continue;
            }
            string name = f.Name;
            bool omitempty = false, quoted = false;
            if (tag.Length > 0)
            {
                var parts = tag.Split(',');
                if (parts.Length == 1 && parts[0] == "-") continue; // json:"-" skips; json:"-," keeps the field named "-"
                if (parts[0].Length > 0) name = parts[0];
                for (int i = 1; i < parts.Length; i++) { if (parts[i] == "omitempty") omitempty = true; else if (parts[i] == "string") quoted = true; }
            }
            if (omitempty && IsEmpty(val)) continue;
            if (!first) sb.Append(',');
            first = false;
            WriteString(sb, name); sb.Append(':');
            // encoding/json's special named field types marshal specially: Number emits its
            // raw numeric text unquoted; RawMessage emits its bytes verbatim as JSON.
            long ftid = Rt.FieldTypeId(t.Name, f.Name);
            string ftn = ftid != 0 ? Rt.NamedTypeName(ftid) : "";
            // ,string: a scalar field is wrapped in a quoted JSON string ("port":"8080").
            if (quoted && IsQuotableScalar(val)) { WriteQuotedScalar(sb, val); }
            else if (ftn == "json.Number") { WriteRawNumber(sb, val); }
            else if (ftn == "json.RawMessage") { WriteRawMessage(sb, val); }
            else Write(sb, val);
        }
    }

    // The ,string option applies to bool / number / string scalar fields only (Go ignores it
    // for other kinds). A GoNamed scalar (e.g. a named int) unwraps to its underlying value.
    private static bool IsQuotableScalar(object? v) => v switch
    {
        bool or long or int or ulong or uint or double or float or GoString => true,
        GoNamed n => IsQuotableScalar(n.Value),
        _ => false,
    };

    // Render the value's normal JSON then wrap that text in a JSON string: int 8080 -> "8080",
    // bool true -> "true", string "abc" -> "\"abc\"".
    private static void WriteQuotedScalar(StringBuilder sb, object? val)
    {
        var inner = new StringBuilder();
        Write(inner, val);
        WriteString(sb, inner.ToString());
    }

    // json.Number marshals as the raw numeric literal (no quotes). An empty Number is
    // invalid JSON in Go (it errors); we emit 0 to stay valid rather than corrupt output.
    private static void WriteRawNumber(StringBuilder sb, object? val)
    {
        string s = val switch
        {
            GoString g => g.ToDotNetString(),
            GoNamed n when n.Value is GoString g2 => g2.ToDotNetString(),
            _ => "",
        };
        sb.Append(s.Length == 0 ? "0" : s);
    }

    // json.RawMessage marshals its bytes verbatim; a nil/empty RawMessage marshals as null.
    private static void WriteRawMessage(StringBuilder sb, object? val)
    {
        if (val is GoNamed n) val = n.Value;
        if (val is GoSlice s && s.Data != null && s.Len > 0) sb.Append(SliceToString(s));
        else sb.Append("null");
    }

    private static bool IsGoStruct(System.Type t) =>
        t.IsValueType && t != typeof(GoSlice) && t != typeof(GoComplex) && !t.IsPrimitive && t != typeof(GoString);

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
    // json.Unmarshal as a func value (e.g. gin's internal/json.Unmarshal = json.Unmarshal):
    // the compiler cannot inject a static type descriptor through the indirection, so the
    // descriptor is derived from the target pointer's runtime .NET type. Type-erased
    // element types (slice/map elements) fall back to a generic decode.
    public static object? UnmarshalValue(GoSlice data, object? target)
    {
        if (target == null) return new GoError(GoString.FromDotNetString("json: Unmarshal(nil)"));
        var vf = target.GetType().GetField("Value");
        var elemType = vf?.FieldType ?? typeof(object);
        string desc = RuntimeDescriptor(elemType, new HashSet<System.Type>());
        return Unmarshal(data, target, GoString.FromDotNetString(desc));
    }

    // Build a json type descriptor from a runtime .NET type (mirror of the compiler's
    // jsonDescriptor; element types of GoSlice/GoMap are erased, hence "any").
    private static string RuntimeDescriptor(System.Type t, HashSet<System.Type> seen)
    {
        if (t == typeof(bool)) return "{\"k\":\"bool\"}";
        if (t == typeof(long) || t == typeof(int)) return "{\"k\":\"int\"}";
        if (t == typeof(ulong) || t == typeof(uint)) return "{\"k\":\"uint\"}";
        if (t == typeof(double) || t == typeof(float)) return "{\"k\":\"float\"}";
        if (t == typeof(GoString)) return "{\"k\":\"string\"}";
        if (t == typeof(GoSlice)) return "{\"k\":\"slice\",\"e\":{\"k\":\"any\"}}";
        if (t == typeof(GoMap)) return "{\"k\":\"map\",\"v\":{\"k\":\"any\"}}";
        if (t == typeof(GoPtr)) return "{\"k\":\"ptr\",\"e\":{\"k\":\"any\"}}";
        if (IsGoStruct(t) && !seen.Contains(t))
        {
            seen.Add(t);
            var sb = new StringBuilder();
            sb.Append("{\"k\":\"struct\",\"n\":\"").Append(t.Name).Append("\",\"f\":[");
            bool first = true;
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.Name.Length == 0 || !char.IsUpper(f.Name[0])) continue;
                string tag = Reflect.TagGet(Reflect.TagFor(t.Name, f.Name), "json");
                string key = f.Name;
                if (tag.Length > 0) { var parts = tag.Split(','); if (parts[0] == "-") continue; if (parts[0].Length > 0) key = parts[0]; }
                if (!first) sb.Append(',');
                first = false;
                sb.Append("{\"j\":\"").Append(key).Append("\",\"c\":\"").Append(f.Name).Append("\",\"t\":").Append(RuntimeDescriptor(f.FieldType, seen)).Append('}');
            }
            sb.Append("]}");
            seen.Remove(t);
            return sb.ToString();
        }
        return "{\"k\":\"any\"}";
    }

    public static object? Unmarshal(GoSlice data, object? target, GoString desc)
    {
        try
        {
            string json = SliceToString(data);
            _errStruct = null; _errFields?.Clear(); // reset per-call mismatch context
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
        // time.Time is a value-type shim whose pointer IS the same GoTime object, so a
        // top-level Unmarshal(&t) hands us the GoTime directly — copy the decoded fields into
        // it in place (there is no GoPtr cell to write through).
        if (target is GoTime tt && value is GoTime nv) { tt.N = nv.N; tt.IsZero = nv.IsZero; return; }
        // The non-generic GoPtr cell may alias a struct field (FSet) or slice element (Arr),
        // not just hold its own Value — write through GoPtrs.Set so the aliased storage is
        // updated. (Setting the unused Value field left e.g. &token.Header empty.)
        if (target is GoPtr gp)
        {
            object? cur = null;
            try { cur = GoPtrs.Get(gp); } catch { /* nil cell: coerce against object */ }
            // Go reuses a non-nil map target, merging the decoded entries in rather than
            // replacing the map — required when the map is shared by reference (jwt aliases
            // one MapClaims through token.Claims and a local copy: replacing would leave the
            // interface-held map empty).
            if (cur is GoMap curMap && curMap.Data != null && value is GoMap newMap && newMap.Data != null)
            {
                foreach (var kv in newMap.Data) curMap.Data[kv.Key] = kv.Value;
                return;
            }
            GoPtrs.Set(gp, Coerce(value, cur?.GetType() ?? typeof(object)));
            return;
        }
        var vf = target.GetType().GetField("Value");
        if (vf == null) throw new System.Exception("json: Unmarshal(non-pointer)");
        vf.SetValue(target, Coerce(value, vf.FieldType));
    }

    // Decode a JSON element into the canonical boxed Go value for a descriptor.
    // A json.Unmarshal type mismatch, carrying Go's exact error message.
    private sealed class GoJsonError : System.Exception { public GoJsonError(string m) : base(m) { } }
    // Error context for the struct-field path in a mismatch message (Go's errorContext):
    // the innermost struct's unqualified name and the chain of JSON field keys.
    [System.ThreadStatic] private static string? _errStruct;
    [System.ThreadStatic] private static System.Collections.Generic.List<string>? _errFields;
    private static GoJsonError Mismatch(JsonElement j, JsonElement desc)
    {
        string jk = j.ValueKind switch
        {
            JsonValueKind.String => "string",
            JsonValueKind.Number => "number",
            JsonValueKind.True or JsonValueKind.False => "bool",
            JsonValueKind.Array => "array",
            JsonValueKind.Object => "object",
            _ => "value",
        };
        string t = desc.TryGetProperty("t", out var tp) ? (tp.GetString() ?? "") : (desc.GetProperty("k").GetString() ?? "");
        if (_errFields != null && _errFields.Count > 0)
            return new GoJsonError($"json: cannot unmarshal {jk} into Go struct field {_errStruct}.{string.Join(".", _errFields)} of type {t}");
        return new GoJsonError($"json: cannot unmarshal {jk} into Go value of type {t}");
    }

    private static object? Decode(JsonElement j, JsonElement desc)
    {
        string k = desc.GetProperty("k").GetString() ?? "any";
        // json.RawMessage captures the raw bytes of any value, including a literal null.
        if (j.ValueKind == JsonValueKind.Null && k != "raw") return DefaultFor(k);
        switch (k)
        {
            // time.Time: parse the RFC3339 JSON string into a GoTime (Time.UnmarshalJSON).
            case "time": return Time.ParseRFC3339(j.GetString() ?? "");
            // json.Number: keep the source numeric literal as a string (named string type).
            case "number": return GoString.FromDotNetString(j.GetRawText());
            // json.RawMessage: store the value's raw JSON bytes verbatim (named []byte type).
            case "raw":
            {
                var rb = Encoding.UTF8.GetBytes(j.GetRawText());
                var rd = new object?[rb.Length];
                for (int i = 0; i < rb.Length; i++) rd[i] = (int)rb[i];
                return new GoSlice { Data = rd, Off = 0, Len = rb.Length, Cap = rb.Length };
            }
            case "bool": return j.ValueKind is JsonValueKind.True or JsonValueKind.False ? j.GetBoolean() : throw Mismatch(j, desc);
            case "int": return j.ValueKind == JsonValueKind.Number ? (j.TryGetInt64(out long li) ? li : (long)j.GetDouble()) : throw Mismatch(j, desc);
            case "uint": return j.ValueKind == JsonValueKind.Number ? (j.TryGetUInt64(out ulong ui) ? ui : (ulong)j.GetDouble()) : throw Mismatch(j, desc);
            case "float": return j.ValueKind == JsonValueKind.Number ? j.GetDouble() : throw Mismatch(j, desc);
            case "string": return j.ValueKind == JsonValueKind.String ? GoString.FromDotNetString(j.GetString() ?? "") : throw Mismatch(j, desc);
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
            string cfield = fd.GetProperty("c").GetString() ?? "";
            var fi = t.GetField(cfield);
            if (fi == null) continue;
            // An embedded (anonymous) struct: Go promotes its fields, so decode the SAME
            // object into it (its own descriptor matches its keys against this object).
            if (fd.TryGetProperty("embed", out var em) && em.GetBoolean())
            {
                fi.SetValue(inst, Coerce(Decode(j, fd.GetProperty("t")), fi.FieldType));
                continue;
            }
            string jkey = fd.GetProperty("j").GetString() ?? "";
            if (!members.TryGetValue(jkey, out var jv)) continue;
            var ft = fd.GetProperty("t");
            object? val;
            // Track the struct-field path for a type-mismatch error message (Go's errorContext);
            // the innermost struct's name wins, the JSON keys accumulate ("Foo.inner.a").
            _errFields ??= new System.Collections.Generic.List<string>();
            _errStruct = cname.StartsWith("__anon", System.StringComparison.Ordinal) ? "" : cname;
            _errFields.Add(jkey);
            try
            {
                // ,string option ("q":true): the value is carried as a quoted JSON string whose
                // content is the real JSON for the field (e.g. "9090" -> 9090, "true" -> true).
                if (fd.TryGetProperty("q", out var qp) && qp.GetBoolean() && jv.ValueKind == JsonValueKind.String)
                {
                    using var inner = JsonDocument.Parse(jv.GetString() ?? "null");
                    val = Decode(inner.RootElement, ft);
                }
                else { val = Decode(jv, ft); }
            }
            finally { _errFields.RemoveAt(_errFields.Count - 1); }
            fi.SetValue(inst, Coerce(val, fi.FieldType));
        }
        return inst;
    }

    // True while decoding through a json.Decoder that called UseNumber(), so an interface{}
    // number is kept as json.Number. Thread-static: a concurrent goroutine decode is isolated.
    [System.ThreadStatic] private static bool _useNumber;
    // Set by a json.Encoder with SetEscapeHTML(false) so WriteString leaves <, >, & literal.
    [System.ThreadStatic] private static bool _noEscapeHTML;

    // Generic decode for interface{} targets — mirrors Go's default mapping.
    private static object? DecodeAny(JsonElement j)
    {
        switch (j.ValueKind)
        {
            case JsonValueKind.True: return true;
            case JsonValueKind.False: return false;
            case JsonValueKind.Number:
                // Under a Decoder with UseNumber, an interface{} number keeps its source text
                // as a json.Number (a named string) rather than collapsing to float64.
                return _useNumber ? Rt.MakeNamedByName("json.Number", GoString.FromDotNetString(j.GetRawText()))
                                  : j.GetDouble();
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
        // *T struct/field pointers use the non-generic GoPtr cell.
        if (target == typeof(GoPtr)) return new GoPtr { Value = v };
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

    // ---- Streaming Decoder / Encoder --------------------------------------
    //
    // json.Decoder is used as a pull tokenizer: Token() returns one of json.Delim
    // ('{','}','[',']'), string, float64 (or json.Number under UseNumber), bool, or
    // nil, with io.EOF past the end — and the caller (e.g. a JS engine's JSON.parse)
    // type-switches on the dynamic type. We tokenize the whole input once with
    // System.Text.Json and replay it; the Delim/Number named types are minted as
    // typed boxes (GoNamed) carrying this build's id so the Go type switch matches.

    public static object? NewDecoder(object? reader)
    {
        var d = new GoJsonDecoder();
        d.Tokenize(Readers.Drain(reader));
        return d;
    }

    public static void Decoder_UseNumber(object dec) => ((GoJsonDecoder)dec).UseNumber = true;
    public static void Decoder_DisallowUnknownFields(object dec) { /* lenient decoder: accepted, not enforced */ }

    public static bool Decoder_More(object dec) => ((GoJsonDecoder)dec).More();

    // Buffered() returns an io.Reader over the bytes after the most recent token.
    public static object? Decoder_Buffered(object dec) => ((GoJsonDecoder)dec).Buffered();

    public static object?[] Decoder_Token(object dec) => ((GoJsonDecoder)dec).Token();

    // Decode(&v) reads the next whole JSON value into v. The compiler can't pass a
    // type descriptor through a method call, so this decodes into the canonical Go
    // shape (Go's interface{} mapping) and writes it through the pointer — exact for
    // map[string]any / []any / *any targets, best-effort widening for sized fields.
    // json/xml error types referenced by type switches (echo's binder reads .Type/.Offset/
    // .Line and calls .Error()). The shim decode path always returns a plain GoError, so a
    // real instance is never produced — these getters exist only so the opaque type compiles;
    // a `case *json.SyntaxError` is matched by the PRECISE IsShimKindStrict path (keyed on the
    // registered [GoShim] CLR class), so it never captures an unrelated GoError.
    public static GoString UTE_Error(object e) => GoString.FromDotNetString("json: cannot unmarshal value into Go value");
    public static object? UTE_Type(object e) => null;
    public static GoString UTE_Value(object e) => GoString.FromDotNetString("");
    public static GoString UTE_Field(object e) => GoString.FromDotNetString("");
    public static GoString UTE_Struct(object e) => GoString.FromDotNetString("");
    public static long UTE_Offset(object e) => 0;
    public static GoString SyntaxErr_Error(object e) => GoString.FromDotNetString("json: syntax error");
    public static long SyntaxErr_Offset(object e) => 0;

    public static long Decoder_InputOffset(object dec) => ((GoJsonDecoder)dec).InputOffset();
    // json.Delim is a rune; its String() is the single character.
    public static GoString Delim_String(int d) => GoString.FromDotNetString(char.ConvertFromUtf32(d));

    // RawMessage ([]byte) round-trips its bytes verbatim.
    public static object?[] RawMessage_MarshalJSON(GoSlice m) =>
        m.Data == null || m.Len == 0
            ? new object?[] { GoStrings.ToByteSlice(GoString.FromDotNetString("null")), null }
            : new object?[] { m, null };
    // (*RawMessage).UnmarshalJSON: the pointer-to-named-slice receiver lowers to the GoSlice
    // value, so an explicit call copies into the receiver's backing array where it has room
    // (the json decoder fills RawMessage struct fields directly; this serves the rare direct
    // call). Returns nil.
    public static object? RawMessage_UnmarshalJSON(GoSlice m, GoSlice data)
    {
        if (m.Data != null)
            for (int i = 0; i < data.Len && i < m.Cap; i++) m.Data[m.Off + i] = data.Data![data.Off + i];
        return null;
    }

    // json.Compact / HTMLEscape / Indent: stream src into the *bytes.Buffer dst.
    public static object? Compact(object? dst, GoSlice src) { Fmt.WriteTo(dst, CompactStr(SliceToString(src))); return null; }
    public static void HTMLEscape(object? dst, GoSlice src)
    {
        var sb = new StringBuilder();
        foreach (char c in SliceToString(src))
            sb.Append(c switch { '<' => "\\u003c", '>' => "\\u003e", '&' => "\\u0026", '\u2028' => "\\u2028", '\u2029' => "\\u2029", _ => c.ToString() });
        Fmt.WriteTo(dst, sb.ToString());
    }
    public static object? Indent(object? dst, GoSlice src, GoString prefix, GoString indent)
    {
        Fmt.WriteTo(dst, IndentStr(SliceToString(src), prefix.ToDotNetString(), indent.ToDotNetString()));
        return null;
    }
    private static string CompactStr(string s)
    {
        var sb = new StringBuilder(s.Length);
        bool inStr = false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (inStr) { sb.Append(c); if (c == '\\' && i + 1 < s.Length) sb.Append(s[++i]); else if (c == '"') inStr = false; }
            else if (c == '"') { inStr = true; sb.Append(c); }
            else if (c != ' ' && c != '\t' && c != '\n' && c != '\r') sb.Append(c);
        }
        return sb.ToString();
    }
    private static string IndentStr(string raw, string prefix, string indent)
    {
        string s = CompactStr(raw);
        var sb = new StringBuilder();
        bool inStr = false;
        int depth = 0;
        void NL(int d) { sb.Append('\n').Append(prefix); for (int k = 0; k < d; k++) sb.Append(indent); }
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (inStr) { sb.Append(c); if (c == '\\' && i + 1 < s.Length) sb.Append(s[++i]); else if (c == '"') inStr = false; continue; }
            switch (c)
            {
                case '"': inStr = true; sb.Append(c); break;
                case '{': case '[':
                    if (i + 1 < s.Length && s[i + 1] == (c == '{' ? '}' : ']')) { sb.Append(c).Append(s[++i]); }
                    else { sb.Append(c); depth++; NL(depth); }
                    break;
                case '}': case ']': depth--; NL(depth); sb.Append(c); break;
                case ',': sb.Append(c); NL(depth); break;
                case ':': sb.Append(": "); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }

    // Error-type Error()/Unwrap() — the canonical message prefixes (Go embeds the
    // offending type/value; these rarely-inspected values carry the stable message head).
    public static GoString UnsupportedTypeError_Error(object e) => GoString.FromDotNetString("json: unsupported type");
    public static GoString UnsupportedValueError_Error(object e) => GoString.FromDotNetString("json: unsupported value");
    public static GoString InvalidUTF8Error_Error(object e) => GoString.FromDotNetString("json: invalid UTF-8 in string");
    public static GoString InvalidUnmarshalError_Error(object e) => GoString.FromDotNetString("json: Unmarshal(nil)");
    public static GoString UnmarshalFieldError_Error(object e) => GoString.FromDotNetString("json: cannot unmarshal");
    public static GoString MarshalerError_Error(object e) => GoString.FromDotNetString("json: error calling MarshalJSON");
    public static object? MarshalerError_Unwrap(object e) => null;

    // json.Number (a named string carrying numeric text): Float64()/Int64()/String(). The
    // value-receiver methods are dispatched with the underlying string representation, so the
    // shim takes a GoString.
    private static string NumStr(object? n) =>
        (n is GoNamed gn ? gn.Value : n) is GoString s ? s.ToDotNetString() : "";
    public static object?[] Number_Float64(GoString n) =>
        double.TryParse(NumStr(n), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d)
            ? new object?[] { d, null }
            : new object?[] { 0.0, new GoError(GoString.FromDotNetString("strconv.ParseFloat: invalid syntax")) };
    public static object?[] Number_Int64(GoString n) =>
        long.TryParse(NumStr(n), out var v)
            ? new object?[] { v, null }
            : new object?[] { 0L, new GoError(GoString.FromDotNetString("strconv.ParseInt: invalid syntax")) };
    public static GoString Number_String(GoString n) => GoString.FromDotNetString(NumStr(n));

    public static object? Decoder_Decode(object dec, object? target)
    {
        try { SetPtr(target, ((GoJsonDecoder)dec).DecodeValue()); return null; }
        catch (System.Exception e) { return new GoError(GoString.FromDotNetString(e.Message)); }
    }

    // Decoder_DecodeTyped is Decode with the target's static-type descriptor, so the
    // next value decodes into the concrete struct rather than a generic map. When the
    // static descriptor is the erased "any" (the target was passed as interface{}, as
    // gin's BindJSON does), the concrete shape is recovered from the target's runtime
    // type instead.
    public static object? Decoder_DecodeTyped(object dec, object? target, GoString desc)
    {
        string raw;
        try { raw = ((GoJsonDecoder)dec).NextRawValue(); }
        catch { return Io.EOFSentinel; }
        try
        {
            string d = desc.ToDotNetString();
            if (d == "{\"k\":\"any\"}") d = TargetDescriptor(target);
            using var doc = JsonDocument.Parse(raw);
            using var ddoc = JsonDocument.Parse(d);
            _useNumber = ((GoJsonDecoder)dec).UseNumber;
            try { SetPtr(target, Decode(doc.RootElement, ddoc.RootElement)); }
            finally { _useNumber = false; }
            return null;
        }
        catch (System.Exception e) { return new GoError(GoString.FromDotNetString(e.Message)); }
    }

    // The json type descriptor recovered from a pointer target's runtime type, used
    // when the static descriptor was erased (an interface{} destination). A generic
    // GoPtr&lt;T&gt; carries the concrete pointee in its Value field's declared type; a
    // non-generic GoPtr carries it only in the current pointee value's runtime type.
    private static string TargetDescriptor(object? target)
    {
        if (target == null) return "{\"k\":\"any\"}";
        var vf = target.GetType().GetField("Value");
        if (vf != null && vf.FieldType != typeof(object))
            return RuntimeDescriptor(vf.FieldType, new HashSet<System.Type>());
        if (target is GoPtr gp)
        {
            object? v = null;
            try { v = GoPtrs.Get(gp); } catch { }
            if (v != null) return RuntimeDescriptor(v.GetType(), new HashSet<System.Type>());
        }
        return "{\"k\":\"any\"}";
    }

    public static object? NewEncoder(object? writer) => new GoJsonEncoder { Writer = writer };

    public static void Encoder_SetIndent(object enc, GoString prefix, GoString indent)
    {
        var e = (GoJsonEncoder)enc;
        e.Prefix = prefix.ToDotNetString();
        e.Indent = indent.ToDotNetString();
    }

    public static void Encoder_SetEscapeHTML(object enc, bool on) => ((GoJsonEncoder)enc).EscapeHTML = on;

    public static object? Encoder_Encode(object enc, object? v)
    {
        var e = (GoJsonEncoder)enc;
        var sb = new StringBuilder();
        bool prevEsc = _noEscapeHTML;
        _noEscapeHTML = !e.EscapeHTML;
        try { Write(sb, v); }
        catch (System.Exception ex) { return new GoError(GoString.FromDotNetString("json: " + ex.Message)); }
        finally { _noEscapeHTML = prevEsc; }
        string s = e.Indent.Length > 0 ? IndentJson(sb.ToString(), e.Prefix, e.Indent) : sb.ToString();
        Fmt.WriteTo(e.Writer, s + "\n"); // Encoder.Encode always appends a newline
        return null;
    }

    // json.Valid(data) reports whether data is well-formed JSON.
    public static bool Valid(GoSlice data)
    {
        try { using var _ = JsonDocument.Parse(SliceToString(data)); return true; }
        catch { return false; }
    }
}

/// <summary>Pull-tokenizer state for json.Decoder: a flat replay of the input's
/// tokens plus a cursor. Number tokens keep their source text so UseNumber can
/// choose float64 vs json.Number at read time.</summary>
public sealed class GoJsonDecoder
{
    public bool UseNumber;
    private readonly List<object?> _toks = new();
    private readonly List<int> _ends = new(); // byte offset just past each token
    private byte[] _raw = System.Array.Empty<byte>();
    private int _pos;

    // The byte offset of the input stream just past the most recently read token.
    public long InputOffset() => _pos > 0 && _pos <= _ends.Count ? _ends[_pos - 1] : 0;

    // A JSON null token, distinct from "end of stream".
    private static readonly object NullTok = new();
    // A number token carrying its raw text (resolved to float64/json.Number lazily).
    private sealed class NumberTok { public string Text = ""; }

    public void Tokenize(byte[] data)
    {
        _raw = data;
        // A json.Decoder reads a stream of concatenated values ({…}{…}); .NET's reader
        // throws on a second top-level value, so tokenize one value at a time: read until
        // the reader rejects the next value, then restart past the consumed bytes. Token
        // end-offsets are recorded absolute (into the whole input).
        int baseOff = 0;
        while (baseOff < data.Length)
        {
            while (baseOff < data.Length && IsJsonWs(data[baseOff])) baseOff++;
            if (baseOff >= data.Length) break;
            var reader = new Utf8JsonReader(new System.ReadOnlySpan<byte>(data, baseOff, data.Length - baseOff), isFinalBlock: true, state: default);
            int lastEnd = 0;
            while (true)
            {
                bool ok;
                try { ok = reader.Read(); }
                catch (JsonException) { break; } // start of the next top-level value
                if (!ok) break;
                int end = baseOff + (int)reader.BytesConsumed;
                switch (reader.TokenType)
                {
                    case JsonTokenType.StartObject: _toks.Add(Delim('{')); _ends.Add(end); break;
                    case JsonTokenType.EndObject: _toks.Add(Delim('}')); _ends.Add(end); break;
                    case JsonTokenType.StartArray: _toks.Add(Delim('[')); _ends.Add(end); break;
                    case JsonTokenType.EndArray: _toks.Add(Delim(']')); _ends.Add(end); break;
                    case JsonTokenType.PropertyName:
                    case JsonTokenType.String: _toks.Add(GoString.FromDotNetString(reader.GetString() ?? "")); _ends.Add(end); break;
                    case JsonTokenType.Number: _toks.Add(new NumberTok { Text = NumberText(ref reader) }); _ends.Add(end); break;
                    case JsonTokenType.True: _toks.Add(true); _ends.Add(end); break;
                    case JsonTokenType.False: _toks.Add(false); _ends.Add(end); break;
                    case JsonTokenType.Null: _toks.Add(NullTok); _ends.Add(end); break;
                }
                lastEnd = (int)reader.BytesConsumed;
            }
            if (lastEnd == 0) break; // no progress (malformed) — stop
            baseOff += lastEnd;
        }
    }

    private static bool IsJsonWs(byte b) => b == (byte)' ' || b == (byte)'\t' || b == (byte)'\n' || b == (byte)'\r';

    private static string NumberText(ref Utf8JsonReader reader)
    {
        if (reader.HasValueSequence)
        {
            var seq = reader.ValueSequence;
            var arr = new byte[(int)seq.Length];
            seq.CopyTo(arr);
            return System.Text.Encoding.UTF8.GetString(arr);
        }
        return System.Text.Encoding.UTF8.GetString(reader.ValueSpan);
    }

    private static object? Delim(char c) => Rt.MakeNamedByName("json.Delim", (int)c);

    public object?[] Token()
    {
        if (_pos >= _toks.Count) return new object?[] { null, Io.EOFSentinel };
        var t = _toks[_pos++];
        if (ReferenceEquals(t, NullTok)) return new object?[] { null, null };
        if (t is NumberTok n) return new object?[] { ResolveNumber(n.Text), null };
        return new object?[] { t, null };
    }

    private object? ResolveNumber(string text) => UseNumber
        ? Rt.MakeNamedByName("json.Number", GoString.FromDotNetString(text))
        : double.Parse(text, System.Globalization.CultureInfo.InvariantCulture);

    public bool More()
    {
        if (_pos >= _toks.Count) return false;
        var t = _toks[_pos];
        // More() is false at the ']' / '}' that closes the current array/object.
        if (t is GoNamed gn && gn.Value is int ch) return ch != ']' && ch != '}';
        return true;
    }

    // Bytes not yet tokenized, as an in-memory reader (io.Reader).
    public object? Buffered()
    {
        int off = _pos > 0 && _pos <= _ends.Count ? _ends[_pos - 1] : 0;
        int n = _raw.Length - off;
        var rem = new byte[n < 0 ? 0 : n];
        if (n > 0) System.Array.Copy(_raw, off, rem, 0, n);
        return new GoReader { Data = rem };
    }

    // Decode the next whole JSON value (Go interface{} mapping). Only valid at a
    // value boundary; intended for Decode() on a freshly created decoder.
    public object? DecodeValue()
    {
        if (_pos >= _toks.Count) throw new System.Exception("EOF");
        int start = SkipSeparators(_pos == 0 ? 0 : _ends[_pos - 1]);
        var reader = new Utf8JsonReader(new System.ReadOnlySpan<byte>(_raw, start, _raw.Length - start), isFinalBlock: true, state: default);
        using var doc = JsonDocument.ParseValue(ref reader);
        // Advance the token cursor past the consumed value.
        long consumedEnd = start + reader.BytesConsumed;
        while (_pos < _ends.Count && _ends[_pos] <= consumedEnd) _pos++;
        return DecodeElement(doc.RootElement);
    }

    // The raw text of the next whole JSON value, advancing the cursor past it. Lets a
    // typed Decode re-parse it under a type descriptor. Throws "EOF" at end of stream.
    public string NextRawValue()
    {
        if (_pos >= _toks.Count) throw new System.Exception("EOF");
        int start = SkipSeparators(_pos == 0 ? 0 : _ends[_pos - 1]);
        var reader = new Utf8JsonReader(new System.ReadOnlySpan<byte>(_raw, start, _raw.Length - start), isFinalBlock: true, state: default);
        using var doc = JsonDocument.ParseValue(ref reader);
        long consumedEnd = start + reader.BytesConsumed;
        while (_pos < _ends.Count && _ends[_pos] <= consumedEnd) _pos++;
        return System.Text.Encoding.UTF8.GetString(_raw, start, (int)reader.BytesConsumed);
    }

    // Advance past whitespace and any structural separator (',' between elements, ':' between
    // a key and its value) so DecodeValue/NextRawValue start at the next value — important when
    // Token() has consumed the opening '[' / a key and Decode() resumes mid-container.
    private int SkipSeparators(int start)
    {
        while (start < _raw.Length && (IsJsonWs(_raw[start]) || _raw[start] == (byte)',' || _raw[start] == (byte)':')) start++;
        return start;
    }

    private static object? DecodeElement(JsonElement j)
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
                foreach (var el in j.EnumerateArray()) d[i++] = DecodeElement(el);
                return new GoSlice { Data = d, Off = 0, Len = n, Cap = n };
            }
            case JsonValueKind.Object:
            {
                var m = GoMaps.Make();
                foreach (var p in j.EnumerateObject()) m.Data![GoString.FromDotNetString(p.Name)] = DecodeElement(p.Value);
                return m;
            }
            default: return null;
        }
    }
}

/// <summary>json.Encoder state: the destination writer and indent/escape options.</summary>
public sealed class GoJsonEncoder
{
    public object? Writer;
    public string Prefix = "";
    public string Indent = "";
    public bool EscapeHTML = true;
}
