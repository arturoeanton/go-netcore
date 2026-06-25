namespace GoCLR.Runtime;

/// <summary>
/// GoSlice is the non-generic slice header the M1 backend emits: an object[]
/// backing array plus offset/len/cap. Elements are stored boxed, which lets the
/// compiler use one slice representation for every element type without .NET
/// generics. (A future increment can specialize hot element types to avoid the
/// per-element boxing.) The backing array is shared, so a write through a copy of
/// the header is still visible — matching Go's slice aliasing.
/// </summary>
public struct GoSlice
{
    public object?[] Data;
    public int Off;
    public int Len;
    public int Cap;
}

/// <summary>
/// GoSlices holds the slice operations the compiler calls into, taking and
/// returning <see cref="GoSlice"/> by value (its backing array is shared).
/// </summary>
public static class GoSlices
{
    /// <summary>make([]T, len, cap) with each live element set to the boxed zero value.</summary>
    public static GoSlice Make(long len, long cap, object? zero)
    {
        int n = (int)len, c = (int)cap;
        if (n < 0 || c < n)
            throw new GoPanicException(GoString.FromDotNetString("runtime error: makeslice: len out of range"));
        var data = c == 0 ? System.Array.Empty<object?>() : new object?[c];
        // Zero the whole backing [0, cap), like Go: code may reslice into the capacity
        // region (s[len:cap]) and read those elements, which must hold the element's
        // zero value (e.g. an empty GoString), not a null reference.
        if (zero != null) { for (int i = 0; i < c; i++) data[i] = zero; }
        return new GoSlice { Data = data, Off = 0, Len = n, Cap = c };
    }

    // The boxed zero value matching v's runtime type, for filling a grown slice's
    // capacity region. Reference-typed elements (null is their zero) and types whose
    // zero we don't synthesize here return null (left as the array default).
    private static object? ZeroLike(object? v) => v switch
    {
        GoString => GoString.FromDotNetString(""),
        long => 0L,
        int => 0,
        ulong => (ulong)0,
        uint => (uint)0,
        double => 0.0,
        float => 0f,
        bool => false,
        // A goclr struct element is a CLR value type extending System.ValueType: its
        // default instance is the Go zero value (value-type fields like GoSlice/GoString
        // default to their own zero, reference-typed fields to nil). Synthesize it so a
        // grown []struct capacity region holds zero structs, not nulls — otherwise code
        // that reslices into the capacity and takes &elem (e.g. fasthttp's allocArg)
        // dereferences a null. Reference-typed elements keep null as their nil zero.
        null => null,
        _ => v.GetType().IsValueType ? System.Activator.CreateInstance(v.GetType()) : null,
    };

    public static long Len(GoSlice s) => s.Len;
    public static long Cap(GoSlice s) => s.Cap;

    /// <summary>s[i] — boxed element (the compiler unboxes to the element type).</summary>
    public static object? Get(GoSlice s, long i)
    {
        if ((ulong)i >= (ulong)s.Len) throw IndexPanic(i, s.Len);
        return s.Data[s.Off + (int)i];
    }

    /// <summary>s[i] = v (v already boxed). Writes through the shared backing array.</summary>
    public static void Set(GoSlice s, long i, object? v)
    {
        if ((ulong)i >= (ulong)s.Len) throw IndexPanic(i, s.Len);
        s.Data[s.Off + (int)i] = v;
    }

    /// <summary>append(s, v) for a single element (the compiler chains for more).</summary>
    public static GoSlice AppendOne(GoSlice s, object? v)
    {
        int need = s.Len + 1;
        if (s.Data != null && need <= s.Cap)
        {
            s.Data[s.Off + s.Len] = v;
            return new GoSlice { Data = s.Data, Off = s.Off, Len = need, Cap = s.Cap };
        }
        int newCap = s.Cap == 0 ? 1 : s.Cap;
        while (newCap < need) newCap = newCap < 1024 ? newCap * 2 : newCap + newCap / 4;
        var data = new object?[newCap];
        if (s.Data != null) System.Array.Copy(s.Data, s.Off, data, 0, s.Len);
        data[s.Len] = v;
        // Zero the grown capacity region [need, newCap), like Go: code may reslice
        // into it (s[len:cap]) and read those elements, which must be the element's
        // zero value (e.g. an empty GoString), not a null reference. The element type
        // is erased here, so infer the zero from the appended value.
        object? zero = ZeroLike(v);
        if (zero != null) { for (int i = need; i < newCap; i++) data[i] = zero; }
        return new GoSlice { Data = data, Off = 0, Len = need, Cap = newCap };
    }

    /// <summary>append(s, add[0..m]...) as a single operation. The added elements must
    /// already be snapshotted by the caller (they may alias s's backing array). Like Go,
    /// the total length is computed up front: it reallocates once when it exceeds cap and
    /// never writes into the original backing array element-by-element. Appending the
    /// elements one at a time (via AppendOne) is wrong — the first element can fall inside
    /// the spare capacity and clobber a slot still aliased by another sub-slice, before a
    /// later element forces the reallocation.</summary>
    public static GoSlice AppendN(GoSlice s, object?[] add, int m)
    {
        if (m == 0) return s;
        int need = s.Len + m;
        if (s.Data != null && need <= s.Cap)
        {
            for (int i = 0; i < m; i++) s.Data[s.Off + s.Len + i] = add[i];
            return new GoSlice { Data = s.Data, Off = s.Off, Len = need, Cap = s.Cap };
        }
        int newCap = s.Cap == 0 ? need : s.Cap;
        while (newCap < need) newCap = newCap < 1024 ? newCap * 2 : newCap + newCap / 4;
        var data = new object?[newCap];
        if (s.Data != null) System.Array.Copy(s.Data, s.Off, data, 0, s.Len);
        for (int i = 0; i < m; i++) data[s.Len + i] = add[i];
        // Zero the grown capacity region [need, newCap), like AppendOne/Make.
        object? zero = ZeroLike(add[m - 1]);
        if (zero != null) { for (int i = need; i < newCap; i++) data[i] = zero; }
        return new GoSlice { Data = data, Off = 0, Len = need, Cap = newCap };
    }

    /// <summary>s[lo:hi].</summary>
    public static GoSlice Slice(GoSlice s, long lo, long hi)
    {
        // Go reports the offending bound: a high past cap as "[:hi] with capacity cap".
        if (hi > s.Cap) throw Bounds($"[:{hi}] with capacity {s.Cap}");
        if (lo < 0) throw Bounds($"[{lo}:]");
        if (hi < lo) throw Bounds($"[{lo}:{hi}]");
        return new GoSlice { Data = s.Data, Off = s.Off + (int)lo, Len = (int)(hi - lo), Cap = s.Cap - (int)lo };
    }

    /// <summary>s[lo:hi:max] — the full-slice expression. Same backing window as s[lo:hi]
    /// but the capacity is capped at max-lo, so a later append past it reallocates instead
    /// of writing into s's tail. Bounds are checked in Go's order (cap, then hi, then lo).</summary>
    public static GoSlice Slice3(GoSlice s, long lo, long hi, long max)
    {
        if (max > s.Cap) throw Bounds($"[::{max}] with capacity {s.Cap}");
        if (hi > max) throw Bounds($"[:{hi}:{max}]");
        if (lo > hi) throw Bounds($"[{lo}:{hi}:]");
        if (lo < 0) throw Bounds($"[{lo}::]");
        return new GoSlice { Data = s.Data, Off = s.Off + (int)lo, Len = (int)(hi - lo), Cap = (int)(max - lo) };
    }

    private static GoPanicException Bounds(string detail) =>
        new(GoString.FromDotNetString("runtime error: slice bounds out of range " + detail));

    private static GoPanicException IndexPanic(long i, int len) =>
        new(GoString.FromDotNetString($"runtime error: index out of range [{i}] with length {len}"));
}
