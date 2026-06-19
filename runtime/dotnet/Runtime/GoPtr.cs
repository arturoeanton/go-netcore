namespace GoCLR.Runtime;

/// <summary>
/// GoPtr is a managed Go pointer (*T). Address-taken locals are boxed into a
/// GoPtr so that <c>&amp;x</c> and <c>*p</c> share mutable state without using
/// native CLR pointers. The nil pointer is represented by a null GoPtr reference.
/// </summary>
public sealed class GoPtr<T>
{
    public T Value;

    public GoPtr(T value) { Value = value; }
    public GoPtr() { Value = default!; }

    /// <summary>Dereference (*p). Panics on a nil pointer.</summary>
    public T Deref()
    {
        // A null reference reaches here only via reflection/unsafe paths; the
        // common nil case is a null GoPtr<T> handled by the caller before calling.
        return Value;
    }

    public static T Deref(GoPtr<T>? p)
    {
        if (p == null)
            throw new GoPanicException(GoString.FromDotNetString("runtime error: invalid memory address or nil pointer dereference"));
        return p.Value;
    }

    public static void Store(GoPtr<T>? p, T value)
    {
        if (p == null)
            throw new GoPanicException(GoString.FromDotNetString("runtime error: invalid memory address or nil pointer dereference"));
        p.Value = value;
    }
}
