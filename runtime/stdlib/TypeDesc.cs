namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>A reflect.Kind, matching Go's reflect package constants exactly so a
/// descriptor's Kind and the values validator/json switch on line up.</summary>
public static class GoKind
{
    public const int Invalid = 0, Bool = 1, Int = 2, Int8 = 3, Int16 = 4, Int32 = 5, Int64 = 6,
        Uint = 7, Uint8 = 8, Uint16 = 9, Uint32 = 10, Uint64 = 11, Uintptr = 12,
        Float32 = 13, Float64 = 14, Complex64 = 15, Complex128 = 16,
        Array = 17, Chan = 18, Func = 19, Interface = 20, Map = 21, Ptr = 22,
        Slice = 23, String = 24, Struct = 25, UnsafePointer = 26;

    private static readonly string[] Names =
    {
        "invalid", "bool", "int", "int8", "int16", "int32", "int64",
        "uint", "uint8", "uint16", "uint32", "uint64", "uintptr",
        "float32", "float64", "complex64", "complex128",
        "array", "chan", "func", "interface", "map", "ptr",
        "slice", "string", "struct", "unsafe.Pointer",
    };

    public static string Name(int k) => k >= 0 && k < Names.Length ? Names[k] : "kind" + k;
}

/// <summary>A struct field within a type descriptor.</summary>
public sealed class GoFieldDesc
{
    public string Name = "";
    public string Tag = "";
    public int TypeId = -1; // descriptor id of the field's type
    public bool Anonymous;
}

/// <summary>A runtime type descriptor — goclr's *rtype. Built at compile time from
/// the static type and registered at startup, so reflect has full, precise type
/// information (kind, name, fields, element/key types) without needing a sample
/// value. Composite types reference their parts by id (lazily resolved through the
/// registry), which lets recursive types register in any order.</summary>
public sealed class GoTypeDesc
{
    public int Id;
    public int Kind;
    public string Name = "";    // "User"; "" for an unnamed/composite type
    public string PkgPath = "";  // "main"; "" for predeclared/unnamed
    public string Str = "";      // the type string: "int", "[]string", "map[string]int", "main.User"
    public int ElemId = -1;      // slice/array/ptr/map/chan element type (compile-time id)
    public int KeyId = -1;       // map key type (compile-time id)
    public int ArrayLen;
    public System.Collections.Generic.List<GoFieldDesc> Fields = new();
    public System.Collections.Generic.List<string> Methods = new(); // method-set names (or an interface's requirements)
    // Direct element/key descriptors for a runtime-synthesized composite (reflect.
    // MapOf/SliceOf/PtrTo/ArrayOf), used in preference to the id when set.
    public GoTypeDesc? ElemDesc;
    public GoTypeDesc? KeyDesc;

    public GoTypeDesc? Elem() => ElemDesc ?? TypeReg.ById(ElemId);
    public GoTypeDesc? Key() => KeyDesc ?? TypeReg.ById(KeyId);
}

/// <summary>The process-wide type descriptor registry. Compile-time emission calls
/// Register*/and reflect resolves descriptors by id.</summary>
public static class TypeReg
{
    private static readonly System.Collections.Generic.Dictionary<int, GoTypeDesc> _byId = new();

    /// <summary>Records a type descriptor (without fields; fields are added by
    /// RegisterField so recursive struct types can reference each other).</summary>
    public static void RegisterType(int id, int kind, GoString name, GoString pkgPath, GoString str, int elemId, int keyId, int arrayLen)
    {
        _byId[id] = new GoTypeDesc
        {
            Id = id,
            Kind = kind,
            Name = name.ToDotNetString(),
            PkgPath = pkgPath.ToDotNetString(),
            Str = str.ToDotNetString(),
            ElemId = elemId,
            KeyId = keyId,
            ArrayLen = arrayLen,
        };
    }

    /// <summary>Appends a struct field to an already-registered type descriptor.</summary>
    public static void RegisterField(int typeId, GoString name, GoString tag, int fieldTypeId, bool anonymous)
    {
        if (_byId.TryGetValue(typeId, out var d))
            d.Fields.Add(new GoFieldDesc { Name = name.ToDotNetString(), Tag = tag.ToDotNetString(), TypeId = fieldTypeId, Anonymous = anonymous });
    }

    /// <summary>Appends a method name to a type descriptor's method set.</summary>
    public static void RegisterMethod(int typeId, GoString name)
    {
        if (_byId.TryGetValue(typeId, out var d)) d.Methods.Add(name.ToDotNetString());
    }

    public static GoTypeDesc? ById(int id) => id >= 0 && _byId.TryGetValue(id, out var d) ? d : null;

    // A runtime-synthesized composite descriptor (reflect.MapOf/SliceOf/PtrTo/ArrayOf),
    // carrying direct element/key descriptors. Deduplicated by type string so the same
    // constructed type compares equal.
    private static readonly System.Collections.Generic.Dictionary<string, GoTypeDesc> _synth = new();
    public static GoTypeDesc Synth(int kind, string str, GoTypeDesc? elem, GoTypeDesc? key, int arrayLen)
    {
        if (_synth.TryGetValue(str, out var d)) return d;
        _synth[str] = d = new GoTypeDesc { Id = -1, Kind = kind, Str = str, ElemDesc = elem, KeyDesc = key, ArrayLen = arrayLen };
        return d;
    }

    // Dynamic-identity links: recover a descriptor for a value reached through an
    // interface (no static descriptor) from its emitted struct type name or its
    // typed-box id.
    private static readonly System.Collections.Generic.Dictionary<string, int> _byClr = new();
    private static readonly System.Collections.Generic.Dictionary<long, int> _byNamed = new();
    public static void LinkClr(GoString clrName, int id) => _byClr[clrName.ToDotNetString()] = id;
    public static void LinkNamed(long namedId, int id) => _byNamed[namedId] = id;

    /// <summary>The descriptor for a runtime value's dynamic type, or null.</summary>
    public static GoTypeDesc? FromValue(object? v)
    {
        switch (v)
        {
            case null: return null;
            case GoNamed nm:
                if (_byNamed.TryGetValue(nm.TypeId, out var nid)) return ById(nid);
                return FromValue(nm.Value);
            case GoString: return Predeclared(GoKind.String);
            case bool: return Predeclared(GoKind.Bool);
            case long: return Predeclared(GoKind.Int);
            case ulong: return Predeclared(GoKind.Uint);
            case double: return Predeclared(GoKind.Float64);
            default:
                if (_byClr.TryGetValue(v.GetType().Name, out var cid)) return ById(cid);
                return null;
        }
    }

    // Synthetic descriptors for predeclared types reached dynamically (a boxed
    // primitive carries no width, so this is the best the representation allows).
    private static readonly System.Collections.Generic.Dictionary<int, GoTypeDesc> _pre = new();
    private static GoTypeDesc Predeclared(int kind)
    {
        if (!_pre.TryGetValue(kind, out var d))
            _pre[kind] = d = new GoTypeDesc { Id = -1, Kind = kind, Name = GoKind.Name(kind), Str = GoKind.Name(kind) };
        return d;
    }
}
