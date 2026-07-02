namespace GoCLR.Stdlib;

using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using GoCLR.Runtime;

/// <summary>encoding/gob.Encoder: streams type definitions + values to a writer.
/// Type ids are process-global (like Go's registry); which ids each stream has
/// already SENT is per-encoder state.</summary>
[GoShim("encoding/gob.Encoder")]
public sealed class GoGobEncoder
{
    public object? W;
    public readonly HashSet<int> Sent = new();
}

/// <summary>encoding/gob.Decoder: reads framed messages (type defs + values) from a
/// reader. The remote type table is per-decoder (remote ids are stream-scoped).</summary>
[GoShim("encoding/gob.Decoder")]
public sealed class GoGobDecoder
{
    public object? R;
    public byte[] Buf = System.Array.Empty<byte>();
    public int Pos;
    public bool Drained;
    public readonly Dictionary<long, Gob.WDef> Defs = new();
}

/// <summary>Shim for Go's <c>encoding/gob</c>: a faithful port of the wire format
/// (framed messages, zigzag ints, byte-reversed floats, delta-encoded struct fields,
/// wireType definitions with Go's exact id-assignment and naming rules) driven by a
/// compiler-injected static type descriptor — byte-exact vs <c>go run</c> for
/// scalars, strings, []byte, slices, arrays, maps, nested structs and pointers.
/// Not supported (documented): interface values (gob.Register), GobEncoder/
/// BinaryMarshaler/TextMarshaler custom methods.</summary>
public static class Gob
{
    // ---- bootstrap ids (ECMA'd into the format; see Go type.go) ----------------
    private const int TBool = 1, TInt = 2, TUint = 3, TFloat = 4, TBytes = 5, TString = 6, TComplex = 7, TInterface = 8;
    private const int FirstUserId = 64;

    // ---- process-global type registry (mirrors Go's package-global `types`) ----
    // A registered user type: its wire definition, referenced by the global id.
    public sealed class RDef
    {
        public int Id;
        public int Kind; // wireType union index: 0=ArrayT 1=SliceT 2=StructT 3=MapT
        public string Name = "";
        public readonly List<(string G, int Id)> Fields = new();
        public int Elem, Key;
        public long Len;
    }

    private static readonly object RegLock = new();
    private static readonly Dictionary<string, int> RegIds = new();  // type-string key -> id
    private static readonly Dictionary<int, RDef> RegDefs = new();
    private static int RegNext = FirstUserId;
    // Parsed descriptor cache (JsonDocuments kept alive for their JsonElements).
    private static readonly Dictionary<string, JsonElement> DescCache = new();

    private static JsonElement ParseDesc(string desc)
    {
        lock (DescCache)
        {
            if (DescCache.TryGetValue(desc, out var e)) return e;
            var doc = JsonDocument.Parse(desc); // intentionally not disposed: elements stay live
            DescCache[desc] = doc.RootElement;
            return doc.RootElement;
        }
    }

    private static string K(JsonElement t) => t.GetProperty("k").GetString() ?? "any";
    private static JsonElement UnwrapPtr(JsonElement t)
    {
        while (K(t) == "ptr") t = t.GetProperty("e");
        return t;
    }
    private static string Gn(JsonElement t) => t.TryGetProperty("gn", out var p) ? p.GetString() ?? "" : "";
    private static string Ts(JsonElement t) => t.TryGetProperty("ts", out var p) ? p.GetString() ?? "" : "";
    // Go's field-path type name: base type's Name(), else its String().
    private static string BaseName(JsonElement t)
    {
        var b = UnwrapPtr(t);
        string gn = Gn(b);
        return gn.Length > 0 ? gn : Ts(b);
    }

    // Register assigns global ids exactly in Go's construction order: a struct takes
    // its own id BEFORE its fields; slices/arrays/maps take theirs AFTER their
    // element/key types. pathName is the name this creation path would give the type
    // (only applied the first time the type is seen, as in Go).
    private static int Register(JsonElement t, string pathName)
    {
        t = UnwrapPtr(t);
        switch (K(t))
        {
            case "bool": return TBool;
            case "int": return TInt;
            case "uint": return TUint;
            case "float": return TFloat;
            case "bytes": return TBytes;
            case "string": return TString;
            case "complex": return TComplex;
            case "any": return TInterface;
        }
        lock (RegLock)
        {
            string key = Ts(t);
            if (RegIds.TryGetValue(key, out int have)) return have;
            switch (K(t))
            {
                case "struct":
                {
                    var rd = new RDef { Id = RegNext++, Kind = 2, Name = pathName };
                    RegIds[key] = rd.Id; RegDefs[rd.Id] = rd;
                    if (t.TryGetProperty("f", out var farr))
                        foreach (var f in farr.EnumerateArray())
                        {
                            var ft = f.GetProperty("t");
                            int cid = Register(ft, BaseName(ft));
                            rd.Fields.Add((f.GetProperty("g").GetString() ?? "", cid));
                        }
                    return rd.Id;
                }
                case "slice":
                {
                    var et = t.GetProperty("e");
                    int eid = Register(et, Gn(UnwrapPtr(et))); // slice elems are named by Name() only
                    if (RegIds.TryGetValue(key, out int again)) return again; // recursion via a struct
                    var rd = new RDef { Id = RegNext++, Kind = 1, Name = pathName, Elem = eid };
                    RegIds[key] = rd.Id; RegDefs[rd.Id] = rd;
                    return rd.Id;
                }
                case "array":
                {
                    int eid = Register(t.GetProperty("e"), "");
                    if (RegIds.TryGetValue(key, out int again)) return again;
                    var rd = new RDef { Id = RegNext++, Kind = 0, Name = pathName, Elem = eid, Len = t.GetProperty("len").GetInt64() };
                    RegIds[key] = rd.Id; RegDefs[rd.Id] = rd;
                    return rd.Id;
                }
                case "map":
                {
                    int kid = Register(t.GetProperty("key"), "");
                    int eid = Register(t.GetProperty("v"), "");
                    if (RegIds.TryGetValue(key, out int again)) return again;
                    var rd = new RDef { Id = RegNext++, Kind = 3, Name = pathName, Key = kid, Elem = eid };
                    RegIds[key] = rd.Id; RegDefs[rd.Id] = rd;
                    return rd.Id;
                }
            }
            throw new System.Exception("gob: unsupported type " + key);
        }
    }

    // ---- primitive wire encoders (see Go encode.go) ----------------------------
    private static void PutUint(List<byte> b, ulong x)
    {
        if (x <= 0x7F) { b.Add((byte)x); return; }
        int n = 8 - (System.Numerics.BitOperations.LeadingZeroCount(x) >> 3);
        b.Add((byte)(256 - n)); // -bytelen(x)
        for (int i = n - 1; i >= 0; i--) b.Add((byte)(x >> (8 * i)));
    }

    private static void PutInt(List<byte> b, long i)
    {
        ulong x = i < 0 ? ((ulong)(~i) << 1) | 1 : (ulong)i << 1;
        PutUint(b, x);
    }

    // Floats travel as the IEEE bits byte-reversed (exponent first -> short varints).
    private static ulong FloatBits(double f) =>
        System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(System.BitConverter.DoubleToUInt64Bits(f));

    private static void PutBytes(List<byte> b, byte[] raw)
    {
        PutUint(b, (ulong)raw.Length);
        b.AddRange(raw);
    }

    // A message is its body preceded by the body's byte count.
    private static void WriteMsg(List<byte> stream, List<byte> body)
    {
        PutUint(stream, (ulong)body.Count);
        stream.AddRange(body);
    }

    // ---- type-definition messages (wireType encoding) --------------------------
    private static void SendType(GoGobEncoder e, List<byte> stream, int id)
    {
        if (id < FirstUserId || !e.Sent.Add(id)) return;
        RDef rd;
        lock (RegLock) { rd = RegDefs[id]; }
        var body = new List<byte>();
        PutInt(body, -id);
        EncodeWireDef(body, rd);
        WriteMsg(stream, body);
        switch (rd.Kind)
        {
            case 2: foreach (var f in rd.Fields) SendType(e, stream, f.Id); break;
            case 0: case 1: SendType(e, stream, rd.Elem); break;
            case 3: SendType(e, stream, rd.Key); SendType(e, stream, rd.Elem); break;
        }
    }

    // The wireType value: a struct whose single set field (ArrayT/SliceT/StructT/MapT)
    // is a nested struct — CommonType{Name,Id} first, then the shape-specific fields.
    private static void EncodeWireDef(List<byte> b, RDef rd)
    {
        PutUint(b, (ulong)(rd.Kind + 1)); // delta from fieldnum -1 to the union field
        // CommonType (embedded struct field 0, always sent)
        PutUint(b, 1);
        int fn = -1;
        if (rd.Name.Length > 0)
        {
            PutUint(b, 1); PutBytes(b, Encoding.UTF8.GetBytes(rd.Name));
            fn = 0;
        }
        PutUint(b, (ulong)(1 - fn)); PutInt(b, rd.Id);
        PutUint(b, 0); // end CommonType
        switch (rd.Kind)
        {
            case 2: // structType.Field []fieldType
                if (rd.Fields.Count > 0)
                {
                    PutUint(b, 1);
                    PutUint(b, (ulong)rd.Fields.Count);
                    foreach (var f in rd.Fields)
                    {
                        PutUint(b, 1); PutBytes(b, Encoding.UTF8.GetBytes(f.G));
                        PutUint(b, 1); PutInt(b, f.Id);
                        PutUint(b, 0); // end fieldType
                    }
                }
                break;
            case 1: // sliceType.Elem
                PutUint(b, 1); PutInt(b, rd.Elem);
                break;
            case 0: // arrayType.Elem, .Len
                PutUint(b, 1); PutInt(b, rd.Elem);
                if (rd.Len != 0) { PutUint(b, 1); PutInt(b, rd.Len); }
                break;
            case 3: // mapType.Key, .Elem
                PutUint(b, 1); PutInt(b, rd.Key);
                PutUint(b, 1); PutInt(b, rd.Elem);
                break;
        }
        PutUint(b, 0); // end the shape struct
        PutUint(b, 0); // end wireType
    }

    // ---- value encoding ---------------------------------------------------------
    private static object? Unbox(object? v)
    {
        while (v is GoNamed n) v = n.Value;
        return v;
    }

    private static object? Deref(object? v)
    {
        v = Unbox(v);
        while (v is GoPtr p)
        {
            object? inner;
            try { inner = GoPtrs.Get(p); } catch { return null; }
            v = Unbox(inner);
        }
        return v;
    }

    private static bool IsZero(JsonElement t, object? v)
    {
        t = UnwrapPtr(t);
        v = Deref(v);
        if (v == null) return true;
        switch (K(t))
        {
            case "bool": return v is bool b && !b;
            case "int": return System.Convert.ToInt64(v) == 0;
            case "uint": return System.Convert.ToUInt64(v) == 0;
            case "float": return System.Convert.ToDouble(v) == 0;
            case "string": return v is GoString s && s.Len == 0;
            case "bytes": return v is GoSlice bs && (bs.Data == null || bs.Len == 0);
            case "slice": return v is GoSlice sl && sl.Len == 0; // nil or empty both skip
            case "map": return v is GoMap m && m.Data == null;   // only a nil map skips
            case "complex": return v is GoComplex c && c.Re == 0 && c.Im == 0;
            case "struct": case "array": return false;           // always sent
            case "any": return true;                             // nil interface skips; non-nil errors later
        }
        return false;
    }

    private static byte[] StrBytes(GoString s) => s.ToDotNetString() is var d ? Encoding.UTF8.GetBytes(d) : System.Array.Empty<byte>();

    private static byte[] SliceBytes(GoSlice s)
    {
        var raw = new byte[s.Len];
        for (int i = 0; i < s.Len; i++) raw[i] = (byte)System.Convert.ToInt64(s.Data![s.Off + i]);
        return raw;
    }

    // Encode one value of descriptor type t (already at the value position: any field
    // delta has been written by the caller).
    private static void EncodeElem(List<byte> b, JsonElement t, object? v)
    {
        t = UnwrapPtr(t);
        v = Deref(v);
        switch (K(t))
        {
            case "bool": PutUint(b, v is bool tb && tb ? 1UL : 0UL); break;
            case "int": PutInt(b, System.Convert.ToInt64(v ?? 0L)); break;
            case "uint": PutUint(b, System.Convert.ToUInt64(v ?? 0UL)); break;
            case "float": PutUint(b, FloatBits(System.Convert.ToDouble(v ?? 0.0))); break;
            case "string": PutBytes(b, v is GoString gs ? StrBytes(gs) : System.Array.Empty<byte>()); break;
            case "bytes": PutBytes(b, v is GoSlice bs && bs.Data != null ? SliceBytes(bs) : System.Array.Empty<byte>()); break;
            case "complex":
            {
                var c = v as GoComplex ?? new GoComplex(0, 0);
                PutUint(b, FloatBits(c.Re)); PutUint(b, FloatBits(c.Im));
                break;
            }
            case "slice": case "array":
            {
                var sl = v as GoSlice? ?? default;
                var et = t.GetProperty("e");
                PutUint(b, (ulong)sl.Len);
                for (int i = 0; i < sl.Len; i++)
                {
                    var ev = sl.Data![sl.Off + i];
                    if (Deref(ev) == null) throw new System.Exception("gob: encodeArray: nil element");
                    EncodeElem(b, et, ev);
                }
                break;
            }
            case "map":
            {
                var m = v as GoMap;
                var kt = t.GetProperty("key");
                var vt = t.GetProperty("v");
                PutUint(b, (ulong)(m?.Data?.Count ?? 0));
                if (m?.Data != null)
                    foreach (var kv in m.Data) { EncodeElem(b, kt, kv.Key); EncodeElem(b, vt, kv.Value); }
                break;
            }
            case "struct": EncodeStructVal(b, t, v); break;
            case "any": throw new System.Exception("gob: type not registered for interface: " + Fmt.TypeName(Unbox(v)));
            default: throw new System.Exception("gob: unsupported kind " + K(t));
        }
    }

    // A struct value: (field delta, value) pairs for non-zero fields, 0 terminator.
    private static void EncodeStructVal(List<byte> b, JsonElement t, object? v)
    {
        int fieldnum = -1, wire = 0;
        if (t.TryGetProperty("f", out var farr))
            foreach (var f in farr.EnumerateArray())
            {
                int idx = wire++;
                var ft = f.GetProperty("t");
                object? fv = null;
                if (v != null)
                {
                    var fi = v.GetType().GetField(f.GetProperty("c").GetString() ?? "");
                    fv = fi?.GetValue(v);
                }
                if (IsZero(ft, fv)) continue;
                PutUint(b, (ulong)(idx - fieldnum));
                fieldnum = idx;
                EncodeElem(b, ft, fv);
            }
        PutUint(b, 0);
    }

    // ---- public API --------------------------------------------------------------
    public static object? NewEncoder(object? w) => new GoGobEncoder { W = w };

    public static object? NewDecoder(object? r) => new GoGobDecoder { R = r };

    // gob.Register / RegisterName: interface transmission is unsupported; accepted as
    // no-ops so programs registering concrete types still compile and run their
    // non-interface paths.
    public static void RegisterValue(object? v) { }
    public static void RegisterName(GoString name, object? v) { }

    /// <summary>Encoder.Encode(v) with the compiler-injected static type descriptor.</summary>
    public static object? Encoder_EncodeTyped(object enc, object? v, GoString desc)
    {
        var e = (GoGobEncoder)enc;
        try
        {
            var root = ParseDesc(desc.ToDotNetString());
            // A nil top-level pointer panics in Go (it cannot be transmitted).
            if (K(root) == "ptr" && Deref(v) == null)
                throw new GoPanicException(GoString.FromDotNetString("gob: cannot encode nil pointer of type *" + Ts(UnwrapPtr(root))));
            var baseT = UnwrapPtr(root);
            if (K(baseT) == "any")
            {
                // The static type was erased (interface arg): recover from the runtime value.
                string rd = RuntimeDescriptor(Deref(v));
                if (rd.Length == 0) return new GoError(GoString.FromDotNetString("gob: cannot encode value of erased type"));
                baseT = ParseDesc(rd);
            }
            int id = Register(baseT, Gn(baseT));
            var stream = new List<byte>();
            SendType(e, stream, id);
            // top-level singleton basics still mark the id as "sent" conceptually; the
            // per-encoder Sent set only tracks user ids, which SendType already added.
            var body = new List<byte>();
            PutInt(body, id);
            if (K(baseT) == "struct")
            {
                EncodeStructVal(body, baseT, Deref(v));
            }
            else
            {
                PutUint(body, 0); // the singleton's field delta
                EncodeElem(body, baseT, v);
            }
            WriteMsg(stream, body);
            Compress.WriteRaw(e.W, stream.ToArray());
            return null;
        }
        catch (GoPanicException) { throw; }
        catch (System.Exception ex)
        {
            string m = ex.Message;
            return new GoError(GoString.FromDotNetString(m.StartsWith("gob") ? m : "gob: " + m));
        }
    }

    // A best-effort descriptor from a runtime value, for Encode through an interface{}
    // static type. Structs are exact (reflection + registered tags); slice/map element
    // types are inferred from the first element.
    private static string RuntimeDescriptor(object? v)
    {
        switch (v)
        {
            case bool: return "{\"k\":\"bool\",\"ts\":\"bool\"}";
            case long or int or short or sbyte: return "{\"k\":\"int\",\"ts\":\"int\"}";
            case ulong or uint or ushort or byte: return "{\"k\":\"uint\",\"ts\":\"uint\"}";
            case double or float: return "{\"k\":\"float\",\"ts\":\"float64\"}";
            case GoString: return "{\"k\":\"string\",\"ts\":\"string\"}";
            case GoComplex: return "{\"k\":\"complex\",\"ts\":\"complex128\"}";
            case GoSlice s:
            {
                if (s.Len == 0) return "";
                var e0 = Deref(s.Data![s.Off]);
                if (e0 is int or long && AllBytes(s)) return "{\"k\":\"bytes\",\"ts\":\"[]uint8\"}";
                string ed = RuntimeDescriptor(e0);
                if (ed.Length == 0) return "";
                return "{\"k\":\"slice\",\"gn\":\"\",\"ts\":\"[]?\",\"e\":" + ed + "}";
            }
            case GoMap m:
            {
                if (m.Data == null || m.Data.Count == 0) return "";
                object? k0 = null, v0 = null;
                foreach (var kv in m.Data) { k0 = kv.Key; v0 = kv.Value; break; }
                string kd = RuntimeDescriptor(Deref(k0)), vd = RuntimeDescriptor(Deref(v0));
                if (kd.Length == 0 || vd.Length == 0) return "";
                return "{\"k\":\"map\",\"gn\":\"\",\"ts\":\"map[?]?\",\"key\":" + kd + ",\"v\":" + vd + "}";
            }
            case not null when IsGoStructInstance(v):
            {
                var t = v.GetType();
                var sb = new StringBuilder();
                sb.Append("{\"k\":\"struct\",\"gn\":\"").Append(t.Name).Append("\",\"ts\":\"main.").Append(t.Name)
                  .Append("\",\"n\":\"").Append(t.Name).Append("\",\"f\":[");
                bool first = true;
                foreach (var f in t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                {
                    if (f.Name.Length == 0 || !char.IsUpper(f.Name[0])) continue;
                    string fd = RuntimeDescriptor(Deref(f.GetValue(v)));
                    if (fd.Length == 0) return "";
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append("{\"g\":\"").Append(f.Name).Append("\",\"c\":\"").Append(f.Name).Append("\",\"t\":").Append(fd).Append('}');
                }
                sb.Append("]}");
                return sb.ToString();
            }
        }
        return "";
    }

    private static bool AllBytes(GoSlice s)
    {
        for (int i = 0; i < s.Len; i++)
        {
            if (Deref(s.Data![s.Off + i]) is not (int or long)) return false;
            long x = System.Convert.ToInt64(s.Data![s.Off + i]);
            if (x < 0 || x > 255) return false;
        }
        return true;
    }

    private static bool IsGoStructInstance(object v)
    {
        var t = v.GetType();
        return t.IsValueType && !t.IsPrimitive && t != typeof(GoString) && t != typeof(GoSlice) && t != typeof(GoComplex);
    }

    // ---- primitive wire decoders --------------------------------------------------
    private static ulong ReadUint(byte[] d, ref int pos)
    {
        if (pos >= d.Length) throw new System.Exception("gob: unexpected EOF");
        byte b0 = d[pos++];
        if (b0 <= 0x7F) return b0;
        int n = 256 - b0;
        if (n < 1 || n > 8 || pos + n > d.Length) throw new System.Exception("gob: bad uint length");
        ulong x = 0;
        for (int i = 0; i < n; i++) x = (x << 8) | d[pos++];
        return x;
    }

    private static long ReadInt(byte[] d, ref int pos)
    {
        ulong u = ReadUint(d, ref pos);
        return (u & 1) != 0 ? ~(long)(u >> 1) : (long)(u >> 1);
    }

    private static byte[] ReadRaw(byte[] d, ref int pos)
    {
        int n = (int)ReadUint(d, ref pos);
        if (pos + n > d.Length) throw new System.Exception("gob: unexpected EOF");
        var r = new byte[n];
        System.Array.Copy(d, pos, r, 0, n);
        pos += n;
        return r;
    }

    // ---- remote (stream) type table ------------------------------------------------
    public sealed class WDef
    {
        public int Kind; // 0=array 1=slice 2=struct 3=map (wireType union index)
        public string Name = "";
        public long Id;
        public readonly List<(string Name, long Id)> Fields = new();
        public long Elem, Key, Len;
    }

    // Decoded remote struct: an ordered bag of (fieldName, canonical value).
    private sealed class FieldBag
    {
        public readonly List<(string Name, object? Val)> Items = new();
        public string RemoteName = "";
    }

    private static WDef ParseWireType(byte[] d, ref int pos)
    {
        var w = new WDef();
        ulong delta = ReadUint(d, ref pos); // the union field selector (fieldnum -1 base)
        int kind = (int)delta - 1;
        if (kind < 0 || kind > 3) throw new System.Exception("gob: unsupported remote type (custom marshaler?)");
        w.Kind = kind;
        // the shape struct
        int fn = -1;
        while (true)
        {
            ulong dl = ReadUint(d, ref pos);
            if (dl == 0) break;
            fn += (int)dl;
            if (fn == 0)
            {
                // CommonType nested struct
                int cfn = -1;
                while (true)
                {
                    ulong cd = ReadUint(d, ref pos);
                    if (cd == 0) break;
                    cfn += (int)cd;
                    if (cfn == 0) w.Name = Encoding.UTF8.GetString(ReadRaw(d, ref pos));
                    else if (cfn == 1) w.Id = ReadInt(d, ref pos);
                    else throw new System.Exception("gob: bad CommonType field");
                }
            }
            else
            {
                switch (w.Kind)
                {
                    case 2: // structType: field 1 = Field []fieldType
                        if (fn != 1) throw new System.Exception("gob: bad structType field");
                        ulong count = ReadUint(d, ref pos);
                        for (ulong i = 0; i < count; i++)
                        {
                            string name = ""; long id = 0;
                            int ffn = -1;
                            while (true)
                            {
                                ulong fd = ReadUint(d, ref pos);
                                if (fd == 0) break;
                                ffn += (int)fd;
                                if (ffn == 0) name = Encoding.UTF8.GetString(ReadRaw(d, ref pos));
                                else if (ffn == 1) id = ReadInt(d, ref pos);
                                else throw new System.Exception("gob: bad fieldType field");
                            }
                            w.Fields.Add((name, id));
                        }
                        break;
                    case 1: // sliceType: field 1 = Elem
                        w.Elem = ReadInt(d, ref pos);
                        break;
                    case 0: // arrayType: field 1 = Elem, field 2 = Len
                        if (fn == 1) w.Elem = ReadInt(d, ref pos);
                        else if (fn == 2) w.Len = ReadInt(d, ref pos);
                        break;
                    case 3: // mapType: field 1 = Key, field 2 = Elem
                        if (fn == 1) w.Key = ReadInt(d, ref pos);
                        else if (fn == 2) w.Elem = ReadInt(d, ref pos);
                        break;
                }
            }
        }
        return w;
    }

    // ---- remote value decoding (canonical boxed values) ------------------------------
    private static object? DecodeRemoteVal(GoGobDecoder dec, long id, byte[] d, ref int pos)
    {
        switch (id)
        {
            case TBool: return ReadUint(d, ref pos) != 0;
            case TInt: return ReadInt(d, ref pos);
            case TUint: return ReadUint(d, ref pos);
            case TFloat: return System.BitConverter.UInt64BitsToDouble(System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(ReadUint(d, ref pos)));
            case TString: return GoString.FromDotNetString(Encoding.UTF8.GetString(ReadRaw(d, ref pos)));
            case TBytes:
            {
                var raw = ReadRaw(d, ref pos);
                var data = new object?[raw.Length];
                for (int i = 0; i < raw.Length; i++) data[i] = Boxes.I4(raw[i]);
                return new GoSlice { Data = data, Off = 0, Len = raw.Length, Cap = raw.Length };
            }
            case TComplex:
            {
                double re = System.BitConverter.UInt64BitsToDouble(System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(ReadUint(d, ref pos)));
                double im = System.BitConverter.UInt64BitsToDouble(System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(ReadUint(d, ref pos)));
                return new GoComplex(re, im);
            }
            case TInterface: throw new System.Exception("gob: cannot decode interface values (gob.Register unsupported)");
        }
        if (!dec.Defs.TryGetValue(id, out var w)) throw new System.Exception("gob: bad type id " + id);
        switch (w.Kind)
        {
            case 2:
            {
                var bag = new FieldBag { RemoteName = w.Name };
                int fn = -1;
                while (true)
                {
                    ulong delta = ReadUint(d, ref pos);
                    if (delta == 0) break;
                    fn += (int)delta;
                    if (fn < 0 || fn >= w.Fields.Count) throw new System.Exception("gob: field number out of range");
                    bag.Items.Add((w.Fields[fn].Name, DecodeRemoteVal(dec, w.Fields[fn].Id, d, ref pos)));
                }
                return bag;
            }
            case 0: case 1:
            {
                int n = (int)ReadUint(d, ref pos);
                var data = new object?[n];
                for (int i = 0; i < n; i++) data[i] = DecodeRemoteVal(dec, w.Elem, d, ref pos);
                return new GoSlice { Data = data, Off = 0, Len = n, Cap = n };
            }
            case 3:
            {
                int n = (int)ReadUint(d, ref pos);
                var m = GoMaps.Make();
                for (int i = 0; i < n; i++)
                {
                    var k = DecodeRemoteVal(dec, w.Key, d, ref pos);
                    var v = DecodeRemoteVal(dec, w.Elem, d, ref pos);
                    m.Data![k!] = v;
                }
                return m;
            }
        }
        throw new System.Exception("gob: bad remote kind");
    }

    // The remote type's display name for error messages.
    private static string RemoteName(GoGobDecoder dec, long id) => id switch
    {
        TBool => "bool", TInt => "int", TUint => "uint", TFloat => "float64",
        TBytes => "bytes", TString => "string", TComplex => "complex128", TInterface => "interface {}",
        _ => dec.Defs.TryGetValue(id, out var w) && w.Name.Length > 0 ? w.Name : "remote type " + id,
    };

    // ---- localization: canonical remote value -> the local descriptor's shape --------
    private static object? Localize(GoGobDecoder dec, object? v, JsonElement t, long remoteId)
    {
        t = UnwrapPtr(t);
        string k = K(t);
        switch (k)
        {
            case "bool":
                if (v is bool) return v;
                break;
            case "int":
                if (v is long) return v;
                break;
            case "uint":
                if (v is ulong) return v;
                break;
            case "float":
                if (v is double) return v;
                break;
            case "string":
                if (v is GoString) return v;
                break;
            case "bytes":
                if (v is GoSlice) return v;
                break;
            case "complex":
                if (v is GoComplex) return v;
                break;
            case "slice": case "array":
                if (v is GoSlice sl)
                {
                    var et = t.GetProperty("e");
                    long relem = dec.Defs.TryGetValue(remoteId, out var wd) ? wd.Elem : 0;
                    var data = new object?[sl.Len];
                    for (int i = 0; i < sl.Len; i++) data[i] = Localize(dec, sl.Data![sl.Off + i], et, relem);
                    if (k == "array" && t.TryGetProperty("len", out var lp))
                    {
                        long want = lp.GetInt64();
                        if (sl.Len > want) throw new System.Exception("gob: length mismatch in decodeArray");
                        if (sl.Len < want)
                        {
                            var full = new object?[want];
                            System.Array.Copy(data, full, sl.Len);
                            for (long i = sl.Len; i < want; i++) full[i] = ZeroFor(et);
                            data = full;
                        }
                    }
                    return new GoSlice { Data = data, Off = 0, Len = data.Length, Cap = data.Length };
                }
                break;
            case "map":
                if (v is GoMap m)
                {
                    var kt = t.GetProperty("key");
                    var vt = t.GetProperty("v");
                    long rk = 0, rv = 0;
                    if (dec.Defs.TryGetValue(remoteId, out var wm)) { rk = wm.Key; rv = wm.Elem; }
                    var nm = GoMaps.Make();
                    if (m.Data != null)
                        foreach (var kv in m.Data)
                            nm.Data![Localize(dec, kv.Key, kt, rk)!] = Localize(dec, kv.Value, vt, rv);
                    return nm;
                }
                break;
            case "struct":
                if (v is FieldBag bag)
                {
                    string cname = t.TryGetProperty("n", out var np) ? np.GetString() ?? "" : "";
                    var ct = ResolveType(cname);
                    if (ct == null) throw new System.Exception("gob: unknown local type " + cname);
                    object inst = System.Activator.CreateInstance(ct)!;
                    var remoteFieldId = new Dictionary<string, long>();
                    if (dec.Defs.TryGetValue(remoteId, out var ws))
                        foreach (var rf in ws.Fields) remoteFieldId[rf.Name] = rf.Id;
                    if (t.TryGetProperty("f", out var farr))
                        foreach (var f in farr.EnumerateArray())
                        {
                            string g = f.GetProperty("g").GetString() ?? "";
                            object? fv = null;
                            bool found = false;
                            foreach (var it in bag.Items)
                                if (it.Name == g) { fv = it.Val; found = true; break; }
                            if (!found) continue;
                            var fi = ct.GetField(f.GetProperty("c").GetString() ?? "");
                            if (fi == null) continue;
                            remoteFieldId.TryGetValue(g, out long rid);
                            var lv = Localize(dec, fv, f.GetProperty("t"), rid);
                            fi.SetValue(inst, CoerceField(lv, fi.FieldType, f.GetProperty("t")));
                        }
                    return inst;
                }
                break;
            case "any":
                // interface{} local target for a concrete remote type: Go rejects this.
                throw new System.Exception("gob: local interface type can only be decoded from remote interface type; received concrete type " + RemoteName(dec, remoteId));
        }
        throw new System.Exception("gob: decoding into local type *" + Ts(t) + ", received remote type " + RemoteName(dec, remoteId));
    }

    // Coerce a canonical local value onto the concrete CLR field type (int widths,
    // GoPtr wrapping for pointer fields).
    private static object? CoerceField(object? v, System.Type target, JsonElement desc)
    {
        if (v == null) return target.IsValueType ? System.Activator.CreateInstance(target) : null;
        if (target == typeof(GoPtr)) return new GoPtr { Value = v };
        if (target.IsInstanceOfType(v)) return v;
        if (target == typeof(long) || target == typeof(int) || target == typeof(short) || target == typeof(sbyte)
            || target == typeof(ulong) || target == typeof(uint) || target == typeof(ushort) || target == typeof(byte)
            || target == typeof(double) || target == typeof(float))
            return System.Convert.ChangeType(v, target, System.Globalization.CultureInfo.InvariantCulture);
        return v;
    }

    private static object? ZeroFor(JsonElement t)
    {
        t = UnwrapPtr(t);
        switch (K(t))
        {
            case "bool": return false;
            case "int": return 0L;
            case "uint": return (ulong)0;
            case "float": return 0.0;
            case "string": return GoString.FromDotNetString("");
            case "complex": return new GoComplex(0, 0);
            case "struct":
            {
                string cname = t.TryGetProperty("n", out var np) ? np.GetString() ?? "" : "";
                var ct = ResolveType(cname);
                return ct != null ? System.Activator.CreateInstance(ct) : null;
            }
            default: return null;
        }
    }

    private static readonly Dictionary<string, System.Type?> TypeCache = new();
    private static System.Type? ResolveType(string name)
    {
        lock (TypeCache)
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

    // Pull any newly-available bytes from the underlying reader (a gob stream can be
    // written to incrementally between Decode calls).
    private static void Refill(GoGobDecoder d)
    {
        byte[] more = Readers.Drain(d.R);
        if (more.Length == 0) return;
        var merged = new byte[d.Buf.Length + more.Length];
        d.Buf.CopyTo(merged, 0);
        more.CopyTo(merged, d.Buf.Length);
        d.Buf = merged;
    }

    /// <summary>Decoder.Decode(&amp;v) with the compiler-injected static type descriptor.</summary>
    public static object? Decoder_DecodeTyped(object dec, object? target, GoString desc)
    {
        var d = (GoGobDecoder)dec;
        try
        {
            if (target == null) return new GoError(GoString.FromDotNetString("gob: attempt to decode into a nil pointer"));
            if (!d.Drained) { Refill(d); d.Drained = true; }
            var root = ParseDesc(desc.ToDotNetString());
            while (true)
            {
                if (d.Pos >= d.Buf.Length)
                {
                    int before = d.Buf.Length;
                    Refill(d);
                    if (d.Buf.Length == before) return Io.EOFSentinel;
                }
                int pos = d.Pos;
                ulong msgLen = ReadUint(d.Buf, ref pos);
                int end = pos + (int)msgLen;
                if (end > d.Buf.Length) return Io.EOFSentinel; // incomplete message
                long id = ReadInt(d.Buf, ref pos);
                if (id < 0)
                {
                    var w = ParseWireType(d.Buf, ref pos);
                    d.Defs[-id] = w;
                    d.Pos = end;
                    continue;
                }
                // A value: struct values are a bare field stream; singletons carry a
                // leading zero field-delta.
                object? canonical;
                bool remoteStruct = d.Defs.TryGetValue(id, out var wd) && wd.Kind == 2;
                if (remoteStruct)
                {
                    canonical = DecodeRemoteVal(d, id, d.Buf, ref pos);
                }
                else
                {
                    ulong delta = ReadUint(d.Buf, ref pos);
                    if (delta != 0) throw new System.Exception("gob: decode: corrupted data: non-zero singleton delta");
                    canonical = DecodeRemoteVal(d, id, d.Buf, ref pos);
                }
                d.Pos = end;
                var local = Localize(d, canonical, root, id);
                SetPtr(target, local);
                return null;
            }
        }
        catch (System.Exception ex)
        {
            string m = ex.Message;
            return new GoError(GoString.FromDotNetString(m.StartsWith("gob") ? m : "gob: " + m));
        }
    }

    // Fallbacks for indirect calls (method values) where the compiler could not
    // inject a static descriptor: recover what we can from the runtime value/target.
    public static object? Encoder_Encode(object enc, object? v) =>
        Encoder_EncodeTyped(enc, v, GoString.FromDotNetString("{\"k\":\"any\"}"));

    public static object? Decoder_Decode(object dec, object? target)
    {
        string desc = "{\"k\":\"any\"}";
        if (target is GoPtr gp)
        {
            object? cur = null;
            try { cur = GoPtrs.Get(gp); } catch { }
            string rd = RuntimeDescriptor(Deref(cur));
            if (rd.Length > 0) desc = rd;
        }
        return Decoder_DecodeTyped(dec, target, GoString.FromDotNetString(desc));
    }

    // Write the decoded value back through the target pointer (GoPtr cell or a shim
    // struct's Value field), merging into a non-nil map target like Go does.
    private static void SetPtr(object target, object? value)
    {
        if (target is GoPtr gp)
        {
            object? cur = null;
            try { cur = GoPtrs.Get(gp); } catch { }
            if (cur is GoMap curMap && curMap.Data != null && value is GoMap newMap && newMap.Data != null)
            {
                foreach (var kv in newMap.Data) curMap.Data[kv.Key] = kv.Value;
                return;
            }
            GoPtrs.Set(gp, value);
            return;
        }
        var vf = target.GetType().GetField("Value");
        if (vf == null) throw new System.Exception("gob: attempt to decode into a non-pointer");
        vf.SetValue(target, value);
    }
}
