namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>Shim for Go's <c>slices</c> package. The element type parameter erases to a boxed
/// element (long/ulong/double/GoString/…) inside the GoSlice, so the generic functions reduce
/// to operations over object?[] with an ordered comparison or the structural == comparer.</summary>
public static class Slices
{
    private static GoSlice S(object o) => (GoSlice)(o is GoNamed n ? n.Value : o)!;
    private static object? At(GoSlice s, int i) => s.Data![s.Off + i];

    // cmp.Ordered comparison of two boxed elements (same dynamic type, per the constraint).
    internal static int OrderedCompare(object? x, object? y)
    {
        if (x is GoString gx) return GoString.Compare(gx, (GoString)y!);
        if (x is double || y is double || x is float || y is float)
            return System.Convert.ToDouble(x).CompareTo(System.Convert.ToDouble(y));
        if (x is ulong || y is ulong) return System.Convert.ToUInt64(x).CompareTo(System.Convert.ToUInt64(y));
        return System.Convert.ToInt64(x).CompareTo(System.Convert.ToInt64(y));
    }
    private static bool Eq(object? a, object? b) => GoKeyComparer.Instance.Equals(a!, b!);

    public static void Sort(object slice)
    {
        var s = S(slice);
        System.Array.Sort(s.Data!, s.Off, s.Len, System.Collections.Generic.Comparer<object?>.Create(OrderedCompare));
    }
    // SortFunc/SortStableFunc: cmp returns negative/zero/positive (an int closure).
    public static void SortFunc(object slice, GoClosure cmp) => SortByClosure(S(slice), cmp, false);
    public static void SortStableFunc(object slice, GoClosure cmp) => SortByClosure(S(slice), cmp, true);
    private static void SortByClosure(GoSlice s, GoClosure cmp, bool stable)
    {
        var idx = new int[s.Len];
        for (int i = 0; i < s.Len; i++) idx[i] = i;
        System.Array.Sort(idx, (i, j) =>
        {
            long c = System.Convert.ToInt64(GoRuntime.InvokeArgs(cmp, At(s, i), At(s, j)));
            if (c != 0) return c < 0 ? -1 : 1;
            return stable ? i.CompareTo(j) : 0;
        });
        var copy = new object?[s.Len];
        for (int i = 0; i < s.Len; i++) copy[i] = At(s, idx[i]);
        for (int i = 0; i < s.Len; i++) s.Data![s.Off + i] = copy[i];
    }

    public static bool Contains(object slice, object? v)
    {
        var s = S(slice);
        for (int i = 0; i < s.Len; i++) if (Eq(At(s, i), v)) return true;
        return false;
    }
    public static bool ContainsFunc(object slice, GoClosure f)
    {
        var s = S(slice);
        for (int i = 0; i < s.Len; i++) if ((bool)GoRuntime.InvokeArgs(f, At(s, i))!) return true;
        return false;
    }
    public static long Index(object slice, object? v)
    {
        var s = S(slice);
        for (int i = 0; i < s.Len; i++) if (Eq(At(s, i), v)) return i;
        return -1;
    }
    public static long IndexFunc(object slice, GoClosure f)
    {
        var s = S(slice);
        for (int i = 0; i < s.Len; i++) if ((bool)GoRuntime.InvokeArgs(f, At(s, i))!) return i;
        return -1;
    }

    public static bool Equal(object a, object b)
    {
        var x = S(a); var y = S(b);
        if (x.Len != y.Len) return false;
        for (int i = 0; i < x.Len; i++) if (!Eq(At(x, i), At(y, i))) return false;
        return true;
    }
    public static bool EqualFunc(object a, object b, GoClosure f)
    {
        var x = S(a); var y = S(b);
        if (x.Len != y.Len) return false;
        for (int i = 0; i < x.Len; i++) if (!(bool)GoRuntime.InvokeArgs(f, At(x, i), At(y, i))!) return false;
        return true;
    }

    public static void Reverse(object slice)
    {
        var s = S(slice);
        for (int i = 0, j = s.Len - 1; i < j; i++, j--)
        { (s.Data![s.Off + i], s.Data[s.Off + j]) = (s.Data[s.Off + j], s.Data[s.Off + i]); }
    }

    public static bool IsSorted(object slice)
    {
        var s = S(slice);
        for (int i = 1; i < s.Len; i++) if (OrderedCompare(At(s, i), At(s, i - 1)) < 0) return false;
        return true;
    }
    public static bool IsSortedFunc(object slice, GoClosure cmp)
    {
        var s = S(slice);
        for (int i = 1; i < s.Len; i++) if (System.Convert.ToInt64(GoRuntime.InvokeArgs(cmp, At(s, i), At(s, i - 1))) < 0) return false;
        return true;
    }

    // BinarySearch returns (index, found): the leftmost position where target could be inserted.
    public static object?[] BinarySearch(object slice, object? target)
    {
        var s = S(slice);
        int lo = 0, hi = s.Len;
        while (lo < hi) { int mid = (int)(((uint)lo + (uint)hi) >> 1); if (OrderedCompare(At(s, mid), target) < 0) lo = mid + 1; else hi = mid; }
        bool found = lo < s.Len && OrderedCompare(At(s, lo), target) == 0;
        return new object?[] { (long)lo, found };
    }
    public static object?[] BinarySearchFunc(object slice, object? target, GoClosure cmp)
    {
        var s = S(slice);
        int lo = 0, hi = s.Len;
        while (lo < hi) { int mid = (int)(((uint)lo + (uint)hi) >> 1); if (System.Convert.ToInt64(GoRuntime.InvokeArgs(cmp, At(s, mid), target)) < 0) lo = mid + 1; else hi = mid; }
        bool found = lo < s.Len && System.Convert.ToInt64(GoRuntime.InvokeArgs(cmp, At(s, lo), target)) == 0;
        return new object?[] { (long)lo, found };
    }

}
