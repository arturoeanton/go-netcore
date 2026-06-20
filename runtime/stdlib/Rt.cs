namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>Small runtime helpers the lowering calls as externs (things that need
/// to inspect a value-type's internals, like a slice's nil backing array).</summary>
public static class Rt
{
    /// <summary>A slice is nil iff its backing array is null (Go's `s == nil`).</summary>
    public static bool SliceIsNil(GoSlice s) => s.Data == null;

    /// <summary>Go value equality (==) for structs and fixed arrays: compares fields
    /// and elements recursively, matching Go's element-wise semantics rather than the
    /// reference identity of the boxed runtime objects.</summary>
    public static bool ValueEqual(object? a, object? b)
    {
        if (a == null || b == null) return ReferenceEquals(a, b);

        // Fixed arrays are slice-backed: equal length and element-wise equal.
        if (a is GoSlice sa && b is GoSlice sb)
        {
            if (sa.Len != sb.Len) return false;
            for (int i = 0; i < sa.Len; i++)
                if (!ValueEqual(sa.Data![sa.Off + i], sb.Data![sb.Off + i]))
                    return false;
            return true;
        }
        if (a is GoString ga && b is GoString gb) return GoStrings.Equal(ga, gb);

        var t = a.GetType();
        if (t != b.GetType()) return false;

        // A struct compares field by field; a pointer cell by what it points at.
        if (a is GoPtr pa && b is GoPtr pb) return ReferenceEquals(pa, pb);
        var fields = t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (fields.Length > 0 && !t.IsPrimitive && !t.IsEnum)
        {
            foreach (var f in fields)
                if (!ValueEqual(f.GetValue(a), f.GetValue(b)))
                    return false;
            return true;
        }
        // Boxed primitives/enums and other leaf values: value equality.
        return a.Equals(b);
    }

    /// <summary>clear(m): remove all entries from a map.</summary>
    public static void ClearMap(GoMap? m) => m?.Data?.Clear();

    /// <summary>clear(s): set every live element of a slice to its zero value.</summary>
    public static void ClearSlice(GoSlice s, object? zero)
    {
        if (s.Data == null) return;
        for (int i = 0; i < s.Len; i++) s.Data[s.Off + i] = zero;
    }

    /// <summary>&amp;s[i] — a pointer aliasing element i of the slice's backing array,
    /// so the slice and the pointer share storage.</summary>
    public static GoPtr ElemAddr(GoSlice s, long i)
    {
        if (s.Data == null || i < 0 || i >= s.Len)
            throw new GoPanicException(GoString.FromDotNetString(
                $"runtime error: index out of range [{i}] with length {s.Len}"));
        return new GoPtr { Arr = s.Data, Idx = (int)(s.Off + i) };
    }

    /// <summary>The nil slice value (zero value of every slice type): a GoSlice with
    /// a null backing array, so `s == nil` is true and append still works.</summary>
    public static GoSlice NilSlice() => default;

    /// <summary>append(s, other...) — append all of other's elements.</summary>
    public static GoSlice AppendSlice(GoSlice s, GoSlice other)
    {
        for (int i = 0; i < other.Len; i++) s = GoSlices.AppendOne(s, other.Data![other.Off + i]);
        return s;
    }

    /// <summary>append(b, str...) where b is []byte — append the string's bytes.</summary>
    public static GoSlice AppendString(GoSlice s, GoString str)
    {
        foreach (byte b in str.Bytes) s = GoSlices.AppendOne(s, (int)b);
        return s;
    }

    /// <summary>s[low:high] for a string — the byte subrange (Go slices strings by
    /// byte offset). Panics out of range, like Go.</summary>
    public static GoString StrSlice(GoString s, long low, long high)
    {
        byte[] b = s.Bytes;
        if (low < 0 || high < low || high > b.Length)
            throw new GoPanicException(GoString.FromDotNetString("runtime error: slice bounds out of range"));
        var r = new byte[high - low];
        System.Array.Copy(b, (int)low, r, 0, (int)(high - low));
        return GoString.FromBytes(r);
    }
}
