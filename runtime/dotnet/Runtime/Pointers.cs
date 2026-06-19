namespace GoCLR.Runtime;

/// <summary>
/// GoPtr is the cell a Go pointer points at: a heap object holding a boxed value.
/// Address-taken locals are stored as a GoPtr so that &amp;x, *p, and writes
/// through the pointer all alias the same cell. nil is a null GoPtr reference.
/// </summary>
public sealed class GoPtr
{
    public object? Value;

    /// <summary>
    /// 1-based id of the pointee's named struct type (0 = untyped). Used at
    /// interface dispatch to match pointer-receiver implementers, whose concrete
    /// type is otherwise erased by this single non-generic cell type.
    /// </summary>
    public long TypeId;
}

/// <summary>GoPtr operations the compiler calls into.</summary>
public static class GoPtrs
{
    /// <summary>Allocate a new cell holding the boxed value (for &amp;literal, new(T), &amp;x).</summary>
    public static GoPtr New(object? value, long typeId) => new() { Value = value, TypeId = typeId };

    /// <summary>The pointee type id of a cell (0 if untyped or nil).</summary>
    public static long TypeIdOf(GoPtr? p) => p?.TypeId ?? 0;

    /// <summary>*p — the boxed pointee. Panics on a nil pointer.</summary>
    public static object? Get(GoPtr? p)
    {
        if (p == null) throw NilDeref();
        return p.Value;
    }

    /// <summary>*p = v. Panics on a nil pointer.</summary>
    public static void Set(GoPtr? p, object? value)
    {
        if (p == null) throw NilDeref();
        p.Value = value;
    }

    private static GoPanicException NilDeref() =>
        new(GoString.FromDotNetString("runtime error: invalid memory address or nil pointer dereference"));
}
