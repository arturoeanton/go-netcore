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

    /// <summary>
    /// Id of this pointer's own type display name ("*main.Color", "*[]int") for %T,
    /// stamped when the pointer is boxed into an interface. 0 = unstamped (fmt falls
    /// back to "*" + the pointee value's type). Distinct from <see cref="TypeId"/>,
    /// which dispatch uses and which a method-less pointee leaves 0.
    /// </summary>
    public long PtrName;

    /// <summary>
    /// When non-null, this pointer aliases element <see cref="Idx"/> of a slice's
    /// backing array (from &amp;s[i]); Get/Set read/write through it so the slice
    /// and the pointer observe the same storage. Value/TypeId are unused then.
    /// </summary>
    public object?[]? Arr;
    public int Idx;

    /// <summary>
    /// When non-null, this pointer aliases a struct field (from &amp;s.field). FGet
    /// re-reads and FSet re-writes the field through its stable container each call,
    /// so the pointer and the field observe the same storage even though structs are
    /// boxed value types. Re-navigating (rather than caching a copy) is what makes
    /// sync/atomic on a struct field correct under a shared lock.
    /// </summary>
    public GoClosure? FGet;
    public GoClosure? FSet;
}

/// <summary>GoPtr operations the compiler calls into.</summary>
public static class GoPtrs
{
    /// <summary>Allocate a new cell holding the boxed value (for &amp;literal, new(T), &amp;x).</summary>
    public static GoPtr New(object? value, long typeId) => new() { Value = value, TypeId = typeId };

    /// <summary>The pointee type id of a cell (0 if untyped or nil).</summary>
    public static long TypeIdOf(GoPtr? p) => p?.TypeId ?? 0;

    /// <summary>
    /// A small code identifying the runtime representation of a GoPtr's pointee, so a
    /// type assertion/switch can tell *int64 from *string from *[]byte — pointers to
    /// non-struct types that otherwise share this one cell type. database/sql's
    /// convertAssign and Row.Scan rely on exactly these `dest.(*int64)` / `dest.(*string)`
    /// distinctions. 0 = not a GoPtr or an unclassified pointee (caller keeps the plain
    /// GoPtr match). *[]byte and *RawBytes still share code 2 (same representation).
    /// </summary>
    public static long PointeeKind(object? p)
    {
        if (p is not GoPtr gp) return 0;
        object? v;
        try { v = Get(gp); } catch { return 0; }
        return v switch
        {
            GoString => 1,
            GoSlice => 2,
            long => 3,
            int => 4,
            bool => 5,
            double => 6,
            ulong => 7,
            uint => 8,
            float => 9,
            _ => 0,
        };
    }

    /// <summary>*p — the boxed pointee. Panics on a nil pointer.</summary>
    public static object? Get(GoPtr? p)
    {
        if (p == null) throw NilDeref();
        if (p.FGet != null) return GoRuntime.InvokeArgs(p.FGet);
        return p.Arr != null ? p.Arr[p.Idx] : p.Value;
    }

    /// <summary>*p = v. Panics on a nil pointer.</summary>
    public static void Set(GoPtr? p, object? value)
    {
        if (p == null) throw NilDeref();
        if (p.FSet != null) { GoRuntime.InvokeArgs(p.FSet, value); return; }
        if (p.Arr != null) p.Arr[p.Idx] = value; else p.Value = value;
    }

    private static GoPanicException NilDeref() =>
        new(GoString.FromDotNetString("runtime error: invalid memory address or nil pointer dereference"));
}
