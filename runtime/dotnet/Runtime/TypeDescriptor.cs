namespace GoCLR.Runtime;

/// <summary>
/// GoKind enumerates Go's reflect.Kind values that the MVP runtime tracks.
/// </summary>
public enum GoKind
{
    Invalid = 0,
    Bool, Int, Int8, Int16, Int32, Int64,
    Uint, Uint8, Uint16, Uint32, Uint64, Uintptr,
    Float32, Float64, Complex64, Complex128,
    String, Slice, Array, Map, Struct, Pointer, Interface, Func, Chan,
}

/// <summary>
/// GoTypeDescriptor is the runtime's structural type identity for a Go type. It
/// backs interface dispatch, type assertions/switches, and the reflect overlay.
/// Descriptors are interned by the compiler so reference equality means type
/// identity.
/// </summary>
public sealed class GoTypeDescriptor
{
    public string Name { get; }
    public string PkgPath { get; }
    public GoKind Kind { get; }

    /// <summary>For composite kinds (Pointer/Slice/Array/Chan): the element type.</summary>
    public GoTypeDescriptor? Elem { get; }

    /// <summary>Method set, keyed by method name, for interface satisfaction checks.</summary>
    public GoMethodTable Methods { get; }

    public GoTypeDescriptor(string name, string pkgPath, GoKind kind,
        GoTypeDescriptor? elem = null, GoMethodTable? methods = null)
    {
        Name = name;
        PkgPath = pkgPath;
        Kind = kind;
        Elem = elem;
        Methods = methods ?? GoMethodTable.Empty;
    }

    /// <summary>Fully-qualified type string (e.g. "pkg.User", "*pkg.User", "[]int").</summary>
    public string String() => Kind switch
    {
        GoKind.Pointer => "*" + (Elem?.String() ?? "?"),
        GoKind.Slice => "[]" + (Elem?.String() ?? "?"),
        _ => string.IsNullOrEmpty(PkgPath) ? Name : ShortPkg(PkgPath) + "." + Name,
    };

    private static string ShortPkg(string pkgPath)
    {
        int i = pkgPath.LastIndexOf('/');
        return i >= 0 ? pkgPath[(i + 1)..] : pkgPath;
    }

    public override string ToString() => String();
}

/// <summary>
/// GoMethodTable maps method names to invokable delegates for a concrete type.
/// The first argument of each delegate is the receiver (boxed as object).
/// </summary>
public sealed class GoMethodTable
{
    public static readonly GoMethodTable Empty = new(new Dictionary<string, Func<object, object?[], object?>>());

    private readonly Dictionary<string, Func<object, object?[], object?>> _methods;

    public GoMethodTable(Dictionary<string, Func<object, object?[], object?>> methods) => _methods = methods;

    public bool Has(string name) => _methods.ContainsKey(name);

    public bool Implements(IEnumerable<string> required)
    {
        foreach (var m in required)
            if (!_methods.ContainsKey(m)) return false;
        return true;
    }

    public object? Invoke(string name, object receiver, params object?[] args)
    {
        if (!_methods.TryGetValue(name, out var fn))
            throw new GoPanicException(GoString.FromDotNetString($"runtime: method {name} not found"));
        return fn(receiver, args);
    }
}
