namespace GoCLR.Runtime;

/// <summary>
/// GoSlice models a Go slice: a (array, offset, len, cap) view over a backing
/// array. The zero value is a nil slice (len == cap == 0, null backing array).
/// </summary>
public struct GoSlice<T>
{
    public T[]? Array;
    public int Offset;
    public int Len;
    public int Cap;

    public GoSlice(T[]? array, int offset, int len, int cap)
    {
        Array = array;
        Offset = offset;
        Len = len;
        Cap = cap;
    }

    public static GoSlice<T> Nil => default;

    public bool IsNil => Array == null;

    /// <summary>make([]T, len, cap).</summary>
    public static GoSlice<T> Make(int len, int cap)
    {
        if (len < 0 || cap < len)
            throw new GoPanicException(GoString.FromDotNetString("runtime error: makeslice: len out of range"));
        return new GoSlice<T>(cap == 0 ? System.Array.Empty<T>() : new T[cap], 0, len, cap);
    }

    /// <summary>make([]T, len).</summary>
    public static GoSlice<T> Make(int len) => Make(len, len);

    /// <summary>Wraps an existing array as a full slice (e.g. from a composite literal).</summary>
    public static GoSlice<T> FromArray(T[] array) => new(array, 0, array.Length, array.Length);

    public T this[int i]
    {
        get
        {
            if ((uint)i >= (uint)Len) throw IndexPanic(i);
            return Array![Offset + i];
        }
        set
        {
            if ((uint)i >= (uint)Len) throw IndexPanic(i);
            Array![Offset + i] = value;
        }
    }

    /// <summary>s[low:high].</summary>
    public GoSlice<T> Slice(int low, int high)
    {
        if (low < 0 || high < low || high > Cap) throw SlicePanic();
        return new GoSlice<T>(Array, Offset + low, high - low, Cap - low);
    }

    /// <summary>s[low:high:max] (three-index slice).</summary>
    public GoSlice<T> Slice3(int low, int high, int max)
    {
        if (low < 0 || high < low || max < high || max > Cap) throw SlicePanic();
        return new GoSlice<T>(Array, Offset + low, high - low, max - low);
    }

    /// <summary>append(s, items...).</summary>
    public static GoSlice<T> Append(GoSlice<T> s, params T[] items)
    {
        int need = s.Len + items.Length;
        if (need <= s.Cap && s.Array != null)
        {
            System.Array.Copy(items, 0, s.Array, s.Offset + s.Len, items.Length);
            return new GoSlice<T>(s.Array, s.Offset, need, s.Cap);
        }
        int newCap = GrowCap(s.Cap, need);
        var arr = new T[newCap];
        if (s.Array != null) System.Array.Copy(s.Array, s.Offset, arr, 0, s.Len);
        System.Array.Copy(items, 0, arr, s.Len, items.Length);
        return new GoSlice<T>(arr, 0, need, newCap);
    }

    /// <summary>copy(dst, src) — returns the number of elements copied.</summary>
    public static int Copy(GoSlice<T> dst, GoSlice<T> src)
    {
        int n = Math.Min(dst.Len, src.Len);
        if (n > 0) System.Array.Copy(src.Array!, src.Offset, dst.Array!, dst.Offset, n);
        return n;
    }

    private static int GrowCap(int oldCap, int need)
    {
        int newCap = oldCap == 0 ? need : oldCap;
        while (newCap < need) newCap = newCap < 1024 ? newCap * 2 : newCap + newCap / 4;
        return newCap;
    }

    private static GoPanicException IndexPanic(int i) =>
        new(GoString.FromDotNetString($"runtime error: index out of range [{i}]"));

    private static GoPanicException SlicePanic() =>
        new(GoString.FromDotNetString("runtime error: slice bounds out of range"));

    /// <summary>Materializes the live window as a span for fast iteration.</summary>
    public ReadOnlySpan<T> AsSpan() => Array == null ? ReadOnlySpan<T>.Empty : Array.AsSpan(Offset, Len);
}
