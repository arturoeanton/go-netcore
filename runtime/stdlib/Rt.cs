namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>Small runtime helpers the lowering calls as externs (things that need
/// to inspect a value-type's internals, like a slice's nil backing array).</summary>
public static class Rt
{
    /// <summary>A slice is nil iff its backing array is null (Go's `s == nil`).</summary>
    public static bool SliceIsNil(GoSlice s) => s.Data == null;

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
