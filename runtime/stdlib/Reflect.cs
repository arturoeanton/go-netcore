namespace GoCLR.Stdlib;

using System;
using System.Reflection;
using GoCLR.Runtime;

/// <summary>reflect.Type handle: describes a value's type (kind inferred from the
/// boxed representation; struct fields via .NET reflection over the emitted
/// TypeDef).</summary>
public sealed class GoReflectType { public object? Sample; }

/// <summary>reflect.Value handle: wraps a (boxed) value. When settable (reached via
/// Elem of a pointer, or a Field of a settable struct), Setter writes a new boxed
/// value back into the underlying storage (the GoPtr cell, threading through parent
/// structs as needed).</summary>
public sealed class GoReflectValue { public object? V; public System.Action<object?>? Setter; }

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

    public static object TypeOf(object? x) => new GoReflectType { Sample = x };
    public static object ValueOf(object? x) => new GoReflectValue { V = x };
    public static bool DeepEqual(object? a, object? b) => Eq(a, b);

    // --- reflect.Type methods ---
    public static ulong Type_Kind(object t) => KindOf(((GoReflectType)t).Sample);
    public static GoString Type_Name(object t) => GoString.FromDotNetString(TypeName(((GoReflectType)t).Sample));
    public static GoString Type_String(object t) => Type_Name(t);
    public static long Type_NumField(object t) => Fields(((GoReflectType)t).Sample)?.Length ?? 0;
    public static object Type_Elem(object t) => new GoReflectType { Sample = ElemSample(((GoReflectType)t).Sample) };

    // --- reflect.Value methods ---
    public static ulong Value_Kind(object v) => KindOf(((GoReflectValue)v).V);
    public static object Value_Type(object v) => new GoReflectType { Sample = ((GoReflectValue)v).V };
    public static object? Value_Interface(object v) => ((GoReflectValue)v).V;
    public static long Value_Int(object v) => Convert.ToInt64(((GoReflectValue)v).V);
    public static ulong Value_Uint(object v) => Convert.ToUInt64(((GoReflectValue)v).V);
    public static double Value_Float(object v) => Convert.ToDouble(((GoReflectValue)v).V);
    public static GoString Value_String(object v) => ((GoReflectValue)v).V is GoString gs ? gs : GoString.FromDotNetString(((GoReflectValue)v).V?.ToString() ?? "");
    public static bool Value_Bool(object v) => (bool)((GoReflectValue)v).V!;
    public static long Value_Len(object v) => LenOf(((GoReflectValue)v).V);
    public static long Value_NumField(object v) => Fields(((GoReflectValue)v).V)?.Length ?? 0;
    public static bool Value_IsNil(object v) => ((GoReflectValue)v).V == null;
    public static bool Value_IsValid(object v) => ((GoReflectValue)v).V != null;
    public static bool Value_IsZero(object v)
    {
        var x = ((GoReflectValue)v).V;
        return x switch { null => true, long l => l == 0, double d => d == 0, bool b => !b, GoString s => s.Len == 0, _ => false };
    }
    public static object Value_Index(object v, long i) =>
        new GoReflectValue { V = GoCLR.Runtime.GoSlices.Get((GoSlice)((GoReflectValue)v).V!, i) };
    public static object Value_Field(object v, long i)
    {
        var parent = (GoReflectValue)v;
        var obj = parent.V!;
        var f = Fields(obj)![(int)i];
        var fv = new GoReflectValue { V = f.GetValue(obj) };
        if (parent.Setter != null)
            fv.Setter = nv => { f.SetValue(obj, Coerce(nv, f.FieldType)); parent.Setter(obj); };
        return fv;
    }
    public static object Value_Elem(object v)
    {
        var x = ((GoReflectValue)v).V;
        if (x is GoPtr p)
            return new GoReflectValue { V = p.Value, Setter = nv => p.Value = nv };
        return new GoReflectValue { V = x };
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
    public static GoSlice Value_MapKeys(object v) => GoCLR.Runtime.GoMaps.Keys((GoMap)((GoReflectValue)v).V!);
    public static object Value_MapIndex(object v, object keyVal)
    {
        var m = (GoMap)((GoReflectValue)v).V!;
        var key = ((GoReflectValue)keyVal).V;
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
        if (a == null || b == null) return ReferenceEquals(a, b);
        switch (a)
        {
            case GoString sa when b is GoString sb: return sa.Equals(sb);
            case GoSlice la when b is GoSlice lb:
                if (la.Len != lb.Len) return false;
                for (int i = 0; i < la.Len; i++) if (!Eq(la.Data[la.Off + i], lb.Data[lb.Off + i])) return false;
                return true;
            default: return a.Equals(b);
        }
    }
}
