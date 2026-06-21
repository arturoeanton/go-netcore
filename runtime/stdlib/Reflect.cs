namespace GoCLR.Stdlib;

using System;
using System.Reflection;
using GoCLR.Runtime;

/// <summary>reflect.Type handle. Desc is the precise compile-time type descriptor
/// (kind/name/fields/element/key); Sample is the legacy representative value, used
/// only when no descriptor is available (a dynamic value reached through an
/// interface).</summary>
public sealed class GoReflectType { public object? Sample; public GoTypeDesc? Desc; }

/// <summary>reflect.Value handle: wraps a (boxed) value plus its type descriptor.
/// When settable (reached via Elem of a pointer, or a Field of a settable struct),
/// Setter writes a new boxed value back into the underlying storage (the GoPtr cell,
/// threading through parent structs as needed).</summary>
public sealed class GoReflectValue { public object? V; public System.Action<object?>? Setter; public GoTypeDesc? Desc; }

/// <summary>Shim for a subset of Go's <c>reflect</c> package (the read path used
/// by fmt and encoding/json).</summary>
public static class Reflect
{
    // reflect.Kind constants.
    private const ulong KInvalid = 0, KBool = 1, KInt = 2, KUint = 7, KFloat64 = 14,
        KComplex128 = 16, KMap = 21, KPtr = 22, KSlice = 23, KString = 24, KStruct = 25;

    private static ulong KindOf(object? v) => v switch
    {
        null => KInvalid,
        bool => KBool,
        long => KInt,
        int => KInt,
        ulong => KUint,
        uint => KUint,
        double => KFloat64,
        GoString => KString,
        GoSlice => KSlice,
        GoMap => KMap,
        GoPtr => KPtr,
        GoComplex => KComplex128,
        _ => v.GetType().IsValueType ? KStruct : KStruct,
    };

    private static long LenOf(object? v) => v switch
    {
        GoSlice s => s.Len,
        GoString gs => gs.Len,
        GoMap m => GoCLR.Runtime.GoMaps.Len(m),
        _ => 0,
    };

    // Struct field tags, registered at startup, keyed by CLR type name then field.
    private static readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>> Tags = new();

    public static void RegisterTag(GoString clrName, GoString field, GoString tag)
    {
        string t = clrName.ToDotNetString();
        if (!Tags.TryGetValue(t, out var fm)) { fm = new(); Tags[t] = fm; }
        fm[field.ToDotNetString()] = tag.ToDotNetString();
    }

    /// <summary>The raw struct tag of a field (by CLR type name + field), or "".</summary>
    public static string TagFor(string clrType, string field) =>
        Tags.TryGetValue(clrType, out var fm) && fm.TryGetValue(field, out var t) ? t : "";

    /// <summary>StructTag.Get(key): extract the `key:"value"` entry from a raw tag.</summary>
    public static string TagGet(string rawTag, string key)
    {
        int i = 0;
        while (i < rawTag.Length)
        {
            while (i < rawTag.Length && rawTag[i] == ' ') i++;
            int ks = i;
            while (i < rawTag.Length && rawTag[i] != ':' && rawTag[i] != ' ') i++;
            if (i >= rawTag.Length || rawTag[i] != ':') break;
            string k = rawTag.Substring(ks, i - ks);
            i++; // ':'
            if (i >= rawTag.Length || rawTag[i] != '"') break;
            i++; // opening quote
            int vs = i;
            while (i < rawTag.Length && rawTag[i] != '"') i++;
            string v = rawTag.Substring(vs, i - vs);
            i++; // closing quote
            if (k == key) return v;
        }
        return "";
    }

    public static object TypeOf(object? x, int descId) => new GoReflectType { Sample = x, Desc = TypeReg.ById(descId) };
    public static object ValueOf(object? x, int descId) => new GoReflectValue { V = x, Desc = TypeReg.ById(descId) };

    // A reflect.Type for a descriptor id (used by Elem/Key/Field.Type).
    private static object RTypeById(int id)
    {
        var d = TypeReg.ById(id);
        return new GoReflectType { Desc = d };
    }
    private static GoTypeDesc? TDesc(object t) => (t as GoReflectType)?.Desc;

    // reflect.MakeSlice(typ, len, cap): a slice Value of the type's element kind;
    // elements default to null (the zero for the interface element type goja uses),
    // and the caller writes them via Index(i).Set.
    public static object MakeSlice(object typ, long len, long cap)
    {
        if (cap < len) cap = len;
        return new GoReflectValue { V = new GoSlice { Data = new object?[cap], Off = 0, Len = (int)len, Cap = (int)cap } };
    }
    public static object MakeMap(object typ) =>
        new GoReflectValue { V = GoCLR.Runtime.GoMaps.Make() };
    // reflect.Zero(typ): the type's zero value, inferred from the type's sample.
    public static object Zero(object typ)
    {
        var s = ((GoReflectType)typ).Sample;
        object? z = s switch
        {
            long => 0L, int => 0, ulong => (ulong)0, uint => (uint)0,
            double => 0.0, float => 0f, bool => false,
            GoString => GoString.FromDotNetString(""),
            GoSlice => new GoSlice { Data = null, Off = 0, Len = 0, Cap = 0 },
            _ => null,
        };
        return new GoReflectValue { V = z };
    }
    public static bool DeepEqual(object? a, object? b) => Eq(a, b);

    // --- reflect.Type methods (prefer the precise descriptor; fall back to sample) ---
    public static ulong Type_Kind(object t) { var d = TDesc(t); return d != null ? (ulong)d.Kind : KindOf(((GoReflectType)t).Sample); }
    public static GoString Type_Name(object t) { var d = TDesc(t); return GoString.FromDotNetString(d != null ? d.Name : TypeName(((GoReflectType)t).Sample)); }
    public static GoString Type_String(object t) { var d = TDesc(t); return GoString.FromDotNetString(d != null ? d.Str : TypeName(((GoReflectType)t).Sample)); }
    public static long Type_NumField(object t) { var d = TDesc(t); return d != null ? d.Fields.Count : (Fields(((GoReflectType)t).Sample)?.Length ?? 0); }
    public static object Type_Elem(object t) { var d = TDesc(t); return d != null ? RTypeById(d.ElemId) : new GoReflectType { Sample = ElemSample(((GoReflectType)t).Sample) }; }

    // --- reflect.Value methods (null receiver = the zero reflect.Value) ---
    private static object? RVal(object? v) => (v as GoReflectValue)?.V;
    private static GoTypeDesc? VDesc(object? v) => (v as GoReflectValue)?.Desc;
    public static ulong Value_Kind(object? v) { var d = VDesc(v); return d != null ? (ulong)d.Kind : KindOf(RVal(v)); }
    public static object Value_Type(object? v) => new GoReflectType { Sample = RVal(v), Desc = VDesc(v) };
    public static object? Value_Interface(object? v) => RVal(v);
    public static long Value_Int(object? v) => Convert.ToInt64(RVal(v) ?? 0L);
    public static ulong Value_Uint(object? v) => Convert.ToUInt64(RVal(v) ?? (ulong)0);
    public static double Value_Float(object? v) => Convert.ToDouble(RVal(v) ?? 0.0);
    public static GoString Value_String(object? v)
    {
        var x = RVal(v);
        if (x is GoString gs) return gs;
        // reflect.Value.String() of a non-string returns "<T Value>"
        return GoString.FromDotNetString(x == null ? "<invalid Value>" : "<" + KindName(KindOf(x)) + " Value>");
    }
    public static bool Value_Bool(object? v) => RVal(v) is bool b && b;
    public static long Value_Len(object? v) => LenOf(RVal(v));
    public static long Value_NumField(object? v) => Fields(RVal(v))?.Length ?? 0;
    public static bool Value_IsNil(object? v) { var x = RVal(v); return x == null || (x is GoSlice s && s.Data == null) || (x is GoMap m && m.Data == null); }
    public static bool Value_IsValid(object? v) => RVal(v) != null;
    public static bool Value_IsZero(object? v)
    {
        var x = RVal(v);
        return x switch { null => true, long l => l == 0, int i => i == 0, ulong u => u == 0, double d => d == 0, bool b => !b, GoString s => s.Len == 0, GoSlice sl => sl.Data == null, GoMap mp => mp.Data == null, _ => false };
    }
    public static object Value_Index(object? v, long i)
    {
        var s = (GoSlice)RVal(v)!;
        int idx = s.Off + (int)i;
        var ed = VDesc(v) is GoTypeDesc d ? TypeReg.ById(d.ElemId) : null;
        // Settable: writes through the slice's shared backing array.
        return new GoReflectValue { V = s.Data![idx], Setter = nv => s.Data[idx] = nv, Desc = ed };
    }
    public static object Value_Field(object? v, long i)
    {
        var parent = (GoReflectValue)v!;
        var obj = parent.V!;
        var f = Fields(obj)![(int)i];
        GoTypeDesc? fd = parent.Desc != null && (int)i < parent.Desc.Fields.Count ? TypeReg.ById(parent.Desc.Fields[(int)i].TypeId) : null;
        var fv = new GoReflectValue { V = f.GetValue(obj), Desc = fd };
        if (parent.Setter != null)
            fv.Setter = nv => { f.SetValue(obj, Coerce(nv, f.FieldType)); parent.Setter(obj); };
        return fv;
    }
    public static object Value_Elem(object v)
    {
        var rv = (GoReflectValue)v;
        var ed = rv.Desc != null ? TypeReg.ById(rv.Desc.ElemId) : null;
        if (rv.V is GoPtr p)
            return new GoReflectValue { V = p.Value, Setter = nv => p.Value = nv, Desc = ed };
        return new GoReflectValue { V = rv.V, Desc = ed };
    }

    // --- reflect.Value write path (settable values) ---
    public static bool Value_CanSet(object v) => ((GoReflectValue)v).Setter != null;
    public static bool Value_CanAddr(object v) => ((GoReflectValue)v).Setter != null;
    private static void DoSet(object v, object? nv)
    {
        var rv = (GoReflectValue)v;
        if (rv.Setter == null) throw new GoPanicException(GoString.FromDotNetString("reflect: reflect.Value.Set using unaddressable value"));
        rv.Setter(nv);
        rv.V = nv;
    }
    public static void Value_SetInt(object v, long n) => DoSet(v, Coerce(n, ((GoReflectValue)v).V?.GetType() ?? typeof(long)));
    public static void Value_SetUint(object v, ulong n) => DoSet(v, Coerce(n, ((GoReflectValue)v).V?.GetType() ?? typeof(ulong)));
    public static void Value_SetFloat(object v, double n) => DoSet(v, Coerce(n, ((GoReflectValue)v).V?.GetType() ?? typeof(double)));
    public static void Value_SetBool(object v, bool b) => DoSet(v, b);
    public static void Value_SetString(object v, GoString s) => DoSet(v, s);
    public static void Value_Set(object v, object other) => DoSet(v, ((GoReflectValue)other).V);

    // Coerce a boxed numeric/value to a concrete CLR type (int widths, GoPtr<>).
    private static object? Coerce(object? v, System.Type target)
    {
        if (v == null) return target.IsValueType ? System.Activator.CreateInstance(target) : null;
        if (target.IsInstanceOfType(v)) return v;
        if (target == typeof(long) || target == typeof(int) || target == typeof(short) || target == typeof(sbyte)
            || target == typeof(ulong) || target == typeof(uint) || target == typeof(ushort) || target == typeof(byte)
            || target == typeof(double) || target == typeof(float))
            return System.Convert.ChangeType(v, target);
        return v;
    }

    // reflect.New(t): a Value holding a pointer to a freshly zeroed t.
    public static object New(object t)
    {
        var sample = ((GoReflectType)t).Sample;
        object? zero = sample switch
        {
            null => null,
            bool => false,
            long => (long)0,
            int => 0,
            ulong => (ulong)0,
            double => (double)0,
            GoString => GoString.FromDotNetString(""),
            _ => sample.GetType().IsValueType ? System.Activator.CreateInstance(sample.GetType()) : null,
        };
        return new GoReflectValue { V = new GoPtr { Value = zero } };
    }
    public static GoSlice Value_MapKeys(object? v)
    {
        var raw = GoCLR.Runtime.GoMaps.Keys((GoMap)RVal(v)!);
        // []reflect.Value: wrap each key.
        var d = new object?[raw.Len];
        for (int i = 0; i < raw.Len; i++) d[i] = new GoReflectValue { V = raw.Data[raw.Off + i] };
        return new GoSlice { Data = d, Off = 0, Len = raw.Len, Cap = raw.Len };
    }
    public static object Value_MapIndex(object? v, object? keyVal)
    {
        var m = (GoMap)RVal(v)!;
        var key = RVal(keyVal);
        return new GoReflectValue { V = GoCLR.Runtime.GoMaps.Get(m, key!, null) };
    }

    private static FieldInfo[]? Fields(object? v) =>
        v == null ? null : v.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);

    private static string TypeName(object? v) => v switch
    {
        null => "<nil>",
        bool => "bool",
        long => "int",
        double => "float64",
        GoString => "string",
        GoSlice => "slice",
        GoMap => "map",
        GoPtr => "ptr",
        _ => v.GetType().Name,
    };

    private static object? ElemSample(object? v) => v switch
    {
        GoSlice s => s.Len > 0 ? s.Data[s.Off] : null,
        GoPtr p => p.Value,
        _ => null,
    };

    private static bool Eq(object? a, object? b)
    {
        if (a == null || b == null) return ReferenceEquals(a, b) || (IsNilish(a) && IsNilish(b));
        switch (a)
        {
            case GoString sa when b is GoString sb: return sa.Equals(sb);
            case GoSlice la when b is GoSlice lb:
                if ((la.Data == null) != (lb.Data == null)) return false;
                if (la.Len != lb.Len) return false;
                for (int i = 0; i < la.Len; i++) if (!Eq(la.Data[la.Off + i], lb.Data[lb.Off + i])) return false;
                return true;
            case GoMap ma when b is GoMap mb:
                if ((ma.Data == null) != (mb.Data == null)) return false;
                if (ma.Data == null) return true;
                if (ma.Data.Count != mb.Data!.Count) return false;
                foreach (var kv in ma.Data) { if (!mb.Data.TryGetValue(kv.Key, out var bv) || !Eq(kv.Value, bv)) return false; }
                return true;
            case GoPtr pa when b is GoPtr pb: return Eq(pa.Value, pb.Value);
            case GoComplex ca when b is GoComplex cb: return ca.Re == cb.Re && ca.Im == cb.Im;
            default:
                // structs (value types): compare public fields recursively.
                if (a.GetType().IsValueType && a.GetType() == b.GetType() && Fields(a) is FieldInfo[] fs && fs.Length > 0)
                {
                    foreach (var f in fs) if (!Eq(f.GetValue(a), f.GetValue(b))) return false;
                    return true;
                }
                return a.Equals(b);
        }
    }

    private static bool IsNilish(object? v) => v == null || (v is GoSlice s && s.Data == null) || (v is GoMap m && m.Data == null);

    // --- reflect.Kind ---
    public static GoString Kind_String(ulong k) => GoString.FromDotNetString(KindName(k));
    private static string KindName(ulong k) => k switch
    {
        KBool => "bool", KInt => "int", 3 => "int8", 4 => "int16", 5 => "int32", 6 => "int64",
        KUint => "uint", 8 => "uint8", 9 => "uint16", 10 => "uint32", 11 => "uint64",
        13 => "float32", KFloat64 => "float64", KComplex128 => "complex128", 15 => "complex64",
        KMap => "map", KPtr => "ptr", KSlice => "slice", KString => "string", KStruct => "struct",
        18 => "func", 17 => "chan", 26 => "unsafe.Pointer", 20 => "interface",
        _ => "invalid",
    };

    // reflect.Type.Field(i) -> a StructField handle (Name, Tag, Type, Anonymous).
    public static object Type_Field(object t, long i)
    {
        var d = TDesc(t);
        if (d != null && (int)i < d.Fields.Count)
        {
            var fd = d.Fields[(int)i];
            return new GoStructField { Name = fd.Name, Tag = fd.Tag, FieldTypeId = fd.TypeId, Index = (int)i, Anonymous = fd.Anonymous };
        }
        var obj = ((GoReflectType)t).Sample;
        var f = Fields(obj)![(int)i];
        return new GoStructField
        {
            Name = f.Name,
            Tag = TagFor(obj!.GetType().Name, f.Name),
            FieldType = f.FieldType,
            Index = (int)i,
            Anonymous = f.Name == f.FieldType.Name, // an embedded field is named for its type
        };
    }

    // --- reflect.StructField field reads (opaque GoStructField handle) ------
    public static GoString StructField_Name(object f) => GoString.FromDotNetString(((GoStructField)f).Name);
    public static GoString StructField_Tag(object f) => GoString.FromDotNetString(((GoStructField)f).Tag);
    public static GoString StructField_PkgPath(object f) =>
        GoString.FromDotNetString(((GoStructField)f).Name.Length > 0 && char.IsUpper(((GoStructField)f).Name[0]) ? "" : "main");
    public static bool StructField_Anonymous(object f) => ((GoStructField)f).Anonymous;
    public static GoSlice StructField_Index(object f)
    {
        // []int{index}: a single-level field index path.
        return new GoSlice { Data = new object?[] { (long)((GoStructField)f).Index }, Off = 0, Len = 1, Cap = 1 };
    }
    public static object StructField_Type(object f)
    {
        var sf = (GoStructField)f;
        if (sf.FieldTypeId >= 0) return RTypeById(sf.FieldTypeId);
        return new GoReflectType { Sample = ZeroOf(sf.FieldType) };
    }

    // --- reflect.StructTag.Get / Lookup (receiver is the raw tag string) ----
    public static GoString StructTag_Get(GoString tag, GoString key) =>
        GoString.FromDotNetString(TagGet(tag.ToDotNetString(), key.ToDotNetString()));
    public static object?[] StructTag_Lookup(GoString tag, GoString key)
    {
        string raw = tag.ToDotNetString(), k = key.ToDotNetString();
        // Lookup reports ok=false only when the key is entirely absent.
        bool present = System.Text.RegularExpressions.Regex.IsMatch(raw, "(^|\\s)" + System.Text.RegularExpressions.Regex.Escape(k) + ":");
        return new object?[] { GoString.FromDotNetString(TagGet(raw, k)), present };
    }

    // --- reflect.Method field reads (handle; methods are not retained) ------
    public static GoString Method_Name(object m) => GoString.FromDotNetString(((GoReflectMethod)m).Name);
    public static long Method_Index(object m) => ((GoReflectMethod)m).Index;
    public static GoString Method_PkgPath(object m) => GoString.FromDotNetString("");

    private static object? ZeroOf(System.Type? t)
    {
        if (t == null) return null;
        if (t == typeof(GoString)) return GoString.FromDotNetString("");
        return t.IsValueType ? System.Activator.CreateInstance(t) : null;
    }

    // --- reflect.Type: more descriptor queries -----------------------------
    // The sample model carries a representative value, so element/key types are
    // recovered from a live element when present. Method-set and func-signature
    // queries return the conservative empty answer (the runtime does not retain
    // those descriptors); see LIMITATIONS.md.
    public static object Type_Key(object t)
    {
        var d = TDesc(t);
        if (d != null) return RTypeById(d.KeyId);
        var s = ((GoReflectType)t).Sample;
        if (s is GoMap m && m.Data != null)
            foreach (var k in m.Data.Keys) return new GoReflectType { Sample = k };
        return new GoReflectType { Sample = null };
    }
    public static long Type_NumMethod(object t) => 0;
    public static object Type_Method(object t, long i) => new GoReflectMethod { Index = (int)i };
    public static long Type_NumIn(object t) => 0;
    public static long Type_NumOut(object t) => 0;
    public static object Type_In(object t, long i) => new GoReflectType { Sample = null };
    public static object Type_Out(object t, long i) => new GoReflectType { Sample = null };
    public static GoString Type_PkgPath(object t) => GoString.FromDotNetString("");
    public static bool Type_Comparable(object t)
    {
        var k = KindOf(((GoReflectType)t).Sample);
        return k != KSlice && k != KMap && k != 18 /*func*/;
    }
    public static bool Type_AssignableTo(object a, object b) =>
        KindOf(((GoReflectType)a).Sample) == KindOf(((GoReflectType)b).Sample);
    public static bool Type_ConvertibleTo(object a, object b) => Type_AssignableTo(a, b);
    public static bool Type_Implements(object a, object iface) => false;

    // --- reflect: free constructors ---------------------------------------
    public static object PointerTo(object t) =>
        new GoReflectType { Sample = new GoPtr { Value = ((GoReflectType)t).Sample } };

    // reflect.MakeFunc(typ, fn): fn is the implementing Go closure; return a Value
    // carrying that callable so Value.Call() can invoke it.
    public static object MakeFunc(object typ, GoClosure fn) =>
        new GoReflectValue { V = fn };

    public static long Copy(object dst, object src)
    {
        if (RVal(dst) is not GoSlice dl || RVal(src) is not GoSlice sl || dl.Data == null || sl.Data == null) return 0;
        int n = System.Math.Min(dl.Len, sl.Len);
        for (int i = 0; i < n; i++) dl.Data[dl.Off + i] = sl.Data[sl.Off + i];
        return n;
    }

    public static object Indirect(object v)
    {
        var x = RVal(v);
        if (x is GoPtr p) return new GoReflectValue { V = p.Value, Setter = nv => p.Value = nv };
        return v;
    }

    public static object Append(object v, GoSlice elems)
    {
        GoSlice s = RVal(v) is GoSlice gs ? gs : default;
        for (int i = 0; i < elems.Len; i++)
            s = GoCLR.Runtime.GoSlices.AppendOne(s, RVal(elems.Data![elems.Off + i]));
        return new GoReflectValue { V = s };
    }

    // --- reflect.Value: more accessors / mutators --------------------------
    public static void Value_SetMapIndex(object v, object key, object elem)
    {
        var m = (GoMap)RVal(v)!;
        var k = RVal(key);
        // A zero (invalid) elem Value deletes the entry, as Go specifies.
        if (((GoReflectValue)elem).V == null && ((GoReflectValue)elem).Setter == null && !Value_IsValid(elem))
            { m.Data?.Remove(k!); return; }
        m.Data![k!] = RVal(elem);
    }
    public static object Value_Convert(object v, object t)
    {
        var src = RVal(v);
        var k = KindOf(((GoReflectType)t).Sample);
        object? conv = k switch
        {
            KInt or 3 or 4 or 5 or 6 => System.Convert.ToInt64(src ?? 0L),
            KUint or 8 or 9 or 10 or 11 => System.Convert.ToUInt64(src ?? (ulong)0),
            13 or KFloat64 => System.Convert.ToDouble(src ?? 0.0),
            KString => src is GoString ? src : GoString.FromDotNetString(System.Convert.ToString(src) ?? ""),
            _ => src,
        };
        return new GoReflectValue { V = conv };
    }
    public static object Value_Addr(object v)
    {
        var rv = (GoReflectValue)v;
        var ptr = new GoPtr { FGet = MakeGet(() => rv.V), FSet = MakeSet(nv => { rv.V = nv; rv.Setter?.Invoke(nv); }) };
        return new GoReflectValue { V = ptr };
    }
    public static long Value_Cap(object? v) { var x = RVal(v); return x is GoSlice s ? s.Cap : 0; }
    public static void Value_SetLen(object v, long n)
    {
        if (RVal(v) is GoSlice s) { s.Len = (int)n; DoSet(v, s); }
    }
    public static void Value_SetCap(object v, long n)
    {
        if (RVal(v) is GoSlice s) { s.Cap = (int)n; DoSet(v, s); }
    }
    public static object Value_Slice(object v, long lo, long hi)
    {
        var s = (GoSlice)RVal(v)!;
        return new GoReflectValue { V = GoCLR.Runtime.GoSlices.Slice(s, lo, hi) };
    }
    // reflect.Value.Pointer() returns uintptr (goclr: UInt64).
    public static ulong Value_Pointer(object? v)
    {
        var x = RVal(v);
        return x == null ? 0 : (ulong)(uint)System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(x);
    }
    public static long Value_NumMethod(object? v) => 0;
    public static bool Value_CanInterface(object? v) => true;
    // FieldByIndex walks a multi-level field path ([]int). The sample model tracks
    // a single level; navigate as far as the live value allows.
    public static object Value_FieldByIndex(object? v, GoSlice index)
    {
        object cur = v!;
        for (int i = 0; i < index.Len; i++)
            cur = Value_Field(cur, System.Convert.ToInt64(index.Data![index.Off + i]));
        return cur;
    }
    // Method(i) as a callable Value. The runtime does not retain method sets, so a
    // bound method Value is produced only on the (unreached) path where NumMethod>0.
    public static object Value_Method(object? v, long i) => new GoReflectValue { V = null };
    public static object Value_MethodByName(object? v, GoString name) => new GoReflectValue { V = null };
    public static bool Value_OverflowInt(object? v, long x) => false;

    // Call(in []Value) []Value: invoke the wrapped function with the unwrapped
    // argument Values and wrap each result as a Value. The wrapped value is a
    // GoClosure (a Go func bridged through reflect.ValueOf).
    public static GoSlice Value_Call(object? v, GoSlice inArgs)
    {
        var f = RVal(v) as GoClosure;
        var args = new object?[inArgs.Len];
        for (int i = 0; i < inArgs.Len; i++) args[i] = RVal(inArgs.Data![inArgs.Off + i]);
        object? res = f != null ? GoCLR.Runtime.GoRuntime.InvokeArgs(f, args) : null;
        // A multi-result function returns a boxed object[] tuple; a single result is
        // the bare value (null counts as a single nil result).
        object?[] outs = res is object?[] tuple ? tuple : new[] { res };
        var data = new object?[outs.Length];
        for (int i = 0; i < outs.Length; i++) data[i] = new GoReflectValue { V = outs[i] };
        return new GoSlice { Data = data, Off = 0, Len = data.Length, Cap = data.Length };
    }
    public static object Value_FieldByName(object? v, GoString name)
    {
        var obj = RVal(v);
        var f = obj?.GetType().GetField(name.ToDotNetString(), BindingFlags.Public | BindingFlags.Instance);
        if (f == null) return new GoReflectValue { V = null };
        var parent = (GoReflectValue)v!;
        var fv = new GoReflectValue { V = f.GetValue(obj) };
        if (parent.Setter != null)
            fv.Setter = nv => { f.SetValue(obj, Coerce(nv, f.FieldType)); parent.Setter(obj); };
        return fv;
    }

    private static GoClosure MakeGet(System.Func<object?> g) =>
        new GoClosure { Id = -1, Native = _ => g() };
    private static GoClosure MakeSet(System.Action<object?> sset) =>
        new GoClosure { Id = -1, Native = a => { sset(a != null && a.Length > 0 ? a[0] : null); return null; } };
}

/// <summary>reflect.StructField handle (the subset code commonly reads).</summary>
public sealed class GoStructField { public string Name = ""; public string Tag = ""; public System.Type? FieldType; public int FieldTypeId = -1; public int Index; public bool Anonymous; }

/// <summary>reflect.Method handle. The runtime does not retain method sets, so
/// these are produced only where a method list is reconstructed by name.</summary>
public sealed class GoReflectMethod { public string Name = ""; public int Index; }
