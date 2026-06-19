namespace GoCLR.Runtime;

/// <summary>
/// GoInterface is the runtime representation of a Go interface value. Go uses
/// structural typing, so an interface is a pair (dynamic type descriptor, data)
/// plus the concrete type's method table. The nil interface has a null
/// descriptor; an interface holding a nil concrete pointer is NOT nil (a classic
/// Go gotcha the runtime must preserve).
/// </summary>
public readonly struct GoInterface
{
    public readonly GoTypeDescriptor? Type;
    public readonly object? Data;
    public readonly GoMethodTable MethodTable;

    public GoInterface(GoTypeDescriptor type, object? data)
    {
        Type = type;
        Data = data;
        MethodTable = type.Methods;
    }

    public static readonly GoInterface Nil = default;

    /// <summary>True only when no concrete type is stored (typed-nil is not nil).</summary>
    public bool IsNil => Type == null;

    /// <summary>Boxes a concrete value into an interface.</summary>
    public static GoInterface Of(GoTypeDescriptor type, object? data) => new(type, data);

    /// <summary>
    /// Type assertion x.(T) with the comma-ok form: returns (data, true) when the
    /// dynamic type matches <paramref name="target"/> by identity, else (null,false).
    /// </summary>
    public (object? value, bool ok) Assert(GoTypeDescriptor target)
    {
        if (Type != null && ReferenceEquals(Type, target)) return (Data, true);
        return (null, false);
    }

    /// <summary>
    /// Interface-to-interface assertion: does the dynamic type implement every
    /// method named in <paramref name="required"/>?
    /// </summary>
    public (GoInterface value, bool ok) AssertInterface(IEnumerable<string> required)
    {
        if (Type != null && Type.Methods.Implements(required)) return (this, true);
        return (Nil, false);
    }

    /// <summary>Dynamic method dispatch (i.Method(args...)).</summary>
    public object? Call(string method, params object?[] args)
    {
        if (Type == null)
            throw new GoPanicException(GoString.FromDotNetString("runtime error: invalid memory address or nil pointer dereference"));
        return MethodTable.Invoke(method, Data!, args);
    }

    /// <summary>The dynamic type's name for type switches and reflect.</summary>
    public string DynamicTypeName() => Type?.String() ?? "<nil>";
}
