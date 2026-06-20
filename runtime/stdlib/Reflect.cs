namespace GoCLR.Stdlib;

using System;
using System.Reflection;
using GoCLR.Runtime;

/// <summary>reflect.Type handle: describes a value's type (kind inferred from the
/// boxed representation; struct fields via .NET reflection over the emitted
/// TypeDef).</summary>
public sealed class GoReflectType { public object? Sample; }

/// <summary>reflect.Value handle: wraps a (boxed) value.</summary>
public sealed class GoReflectValue { public object? V; }

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
        var obj = ((GoReflectValue)v).V!;
        var f = Fields(obj)![(int)i];
        return new GoReflectValue { V = f.GetValue(obj) };
    }
    public static object Value_Elem(object v)
    {
        var x = ((GoReflectValue)v).V;
        return new GoReflectValue { V = x is GoPtr p ? p.Value : x };
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
