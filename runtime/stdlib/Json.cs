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
            bool omitempty = false;
            if (tag.Length > 0)
            {
                var parts = tag.Split(',');
                if (parts[0] == "-") continue;
                if (parts[0].Length > 0) name = parts[0];
                for (int i = 1; i < parts.Length; i++) if (parts[i] == "omitempty") omitempty = true;
            }
            if (omitempty && IsEmpty(val)) continue;
            if (!first) sb.Append(',');
            first = false;
            WriteString(sb, name); sb.Append(':'); Write(sb, val);
        }
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

    public static bool Decoder_More(object dec) => ((GoJsonDecoder)dec).More();

    // Buffered() returns an io.Reader over the bytes after the most recent token.
    public static object? Decoder_Buffered(object dec) => ((GoJsonDecoder)dec).Buffered();

    public static object?[] Decoder_Token(object dec) => ((GoJsonDecoder)dec).Token();

    // Decode(&v) reads the next whole JSON value into v. The compiler can't pass a
    // type descriptor through a method call, so this decodes into the canonical Go
    // shape (Go's interface{} mapping) and writes it through the pointer — exact for
    // map[string]any / []any / *any targets, best-effort widening for sized fields.
    public static object? Decoder_Decode(object dec, object? target)
    {
        try { SetPtr(target, ((GoJsonDecoder)dec).DecodeValue()); return null; }
        catch (System.Exception e) { return new GoError(GoString.FromDotNetString(e.Message)); }
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
        try { Write(sb, v); }
        catch (System.Exception ex) { return new GoError(GoString.FromDotNetString("json: " + ex.Message)); }
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

    // A JSON null token, distinct from "end of stream".
    private static readonly object NullTok = new();
    // A number token carrying its raw text (resolved to float64/json.Number lazily).
    private sealed class NumberTok { public string Text = ""; }

    public void Tokenize(byte[] data)
    {
        _raw = data;
        var reader = new Utf8JsonReader(data, isFinalBlock: true, state: default);
        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject: Add(Delim('{'), ref reader); break;
                case JsonTokenType.EndObject: Add(Delim('}'), ref reader); break;
                case JsonTokenType.StartArray: Add(Delim('['), ref reader); break;
                case JsonTokenType.EndArray: Add(Delim(']'), ref reader); break;
                case JsonTokenType.PropertyName:
                case JsonTokenType.String: Add(GoString.FromDotNetString(reader.GetString() ?? ""), ref reader); break;
                case JsonTokenType.Number: Add(new NumberTok { Text = NumberText(ref reader) }, ref reader); break;
                case JsonTokenType.True: Add(true, ref reader); break;
                case JsonTokenType.False: Add(false, ref reader); break;
                case JsonTokenType.Null: Add(NullTok, ref reader); break;
            }
        }
    }

    private void Add(object? tok, ref Utf8JsonReader reader)
    {
        _toks.Add(tok);
        // BytesConsumed is the offset just past the token just read.
        _ends.Add((int)reader.BytesConsumed);
    }

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
        int start = _pos == 0 ? 0 : _ends[_pos - 1];
        var reader = new Utf8JsonReader(new System.ReadOnlySpan<byte>(_raw, start, _raw.Length - start), isFinalBlock: true, state: default);
        using var doc = JsonDocument.ParseValue(ref reader);
        // Advance the token cursor past the consumed value.
        long consumedEnd = start + reader.BytesConsumed;
        while (_pos < _ends.Count && _ends[_pos] <= consumedEnd) _pos++;
        return DecodeElement(doc.RootElement);
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
