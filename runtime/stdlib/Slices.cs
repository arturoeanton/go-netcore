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

    public static object? Max(object slice)
    {
        var s = S(slice);
        if (s.Len == 0) throw new GoPanicException(GoString.FromDotNetString("slices.Max: empty list"));
        object? m = At(s, 0);
        for (int i = 1; i < s.Len; i++) if (OrderedCompare(At(s, i), m) > 0) m = At(s, i);
        return m;
    }
    public static object? Min(object slice)
    {
        var s = S(slice);
        if (s.Len == 0) throw new GoPanicException(GoString.FromDotNetString("slices.Min: empty list"));
        object? m = At(s, 0);
        for (int i = 1; i < s.Len; i++) if (OrderedCompare(At(s, i), m) < 0) m = At(s, i);
        return m;
    }
    public static object? MaxFunc(object slice, GoClosure cmp)
    {
        var s = S(slice);
        if (s.Len == 0) throw new GoPanicException(GoString.FromDotNetString("slices.MaxFunc: empty list"));
        object? m = At(s, 0);
        for (int i = 1; i < s.Len; i++) if (System.Convert.ToInt64(GoRuntime.InvokeArgs(cmp, At(s, i), m)) > 0) m = At(s, i);
        return m;
    }
    public static object? MinFunc(object slice, GoClosure cmp)
    {
        var s = S(slice);
        if (s.Len == 0) throw new GoPanicException(GoString.FromDotNetString("slices.MinFunc: empty list"));
        object? m = At(s, 0);
        for (int i = 1; i < s.Len; i++) if (System.Convert.ToInt64(GoRuntime.InvokeArgs(cmp, At(s, i), m)) < 0) m = At(s, i);
        return m;
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

    public static object Clone(object slice)
    {
        var s = S(slice);
        if (s.Data == null) return s; // nil stays nil
        var d = new object?[s.Len];
        for (int i = 0; i < s.Len; i++) d[i] = At(s, i);
        return new GoSlice { Data = d, Off = 0, Len = s.Len, Cap = s.Len };
    }
    public static object Compact(object slice)
    {
        var s = S(slice);
        if (s.Len == 0) return s;
        var outp = new System.Collections.Generic.List<object?> { At(s, 0) };
        for (int i = 1; i < s.Len; i++) if (!Eq(At(s, i), At(s, i - 1))) outp.Add(At(s, i));
        return new GoSlice { Data = outp.ToArray(), Off = 0, Len = outp.Count, Cap = outp.Count };
    }
    public static object CompactFunc(object slice, GoClosure eq)
    {
        var s = S(slice);
        if (s.Len == 0) return s;
        var outp = new System.Collections.Generic.List<object?> { At(s, 0) };
        for (int i = 1; i < s.Len; i++) if (!(bool)GoRuntime.InvokeArgs(eq, At(s, i), outp[outp.Count - 1])!) outp.Add(At(s, i));
        return new GoSlice { Data = outp.ToArray(), Off = 0, Len = outp.Count, Cap = outp.Count };
    }
    public static object Concat(GoSlice slices)
    {
        var outp = new System.Collections.Generic.List<object?>();
        for (int i = 0; i < slices.Len; i++)
        {
            var inner = S(slices.Data![slices.Off + i]!);
            for (int j = 0; j < inner.Len; j++) outp.Add(At(inner, j));
        }
        return new GoSlice { Data = outp.ToArray(), Off = 0, Len = outp.Count, Cap = outp.Count };
    }
    private static GoSlice FromList(System.Collections.Generic.List<object?> l) =>
        new GoSlice { Data = l.ToArray(), Off = 0, Len = l.Count, Cap = l.Count };

    // --- iterators (iter.Seq) ---
    // Values(s): an iter.Seq[E] yielding each element in order.
    public static GoClosure Values(object slice)
    {
        var s = S(slice);
        return NativeClosures.Make(a =>
        {
            var yield = (GoClosure)a![0]!;
            for (int i = 0; i < s.Len; i++) if (!(bool)GoRuntime.InvokeArgs(yield, At(s, i))!) break;
            return null;
        });
    }
    // All(s): an iter.Seq2[int, E] yielding (index, element).
    public static GoClosure All(object slice)
    {
        var s = S(slice);
        return NativeClosures.Make(a =>
        {
            var yield = (GoClosure)a![0]!;
            for (int i = 0; i < s.Len; i++) if (!(bool)GoRuntime.InvokeArgs(yield, (long)i, At(s, i))!) break;
            return null;
        });
    }
    // Backward(s): an iter.Seq2[int, E] yielding (index, element) from the end.
    public static GoClosure Backward(object slice)
    {
        var s = S(slice);
        return NativeClosures.Make(a =>
        {
            var yield = (GoClosure)a![0]!;
            for (int i = s.Len - 1; i >= 0; i--) if (!(bool)GoRuntime.InvokeArgs(yield, (long)i, At(s, i))!) break;
            return null;
        });
    }
    // Collect(seq): drain an iter.Seq[E] into a new slice (insertion order).
    public static object Collect(GoClosure seq)
    {
        var outp = new System.Collections.Generic.List<object?>();
        var sink = NativeClosures.Make(a => { outp.Add(a![0]); return true; });
        GoRuntime.InvokeArgs(seq, sink);
        return FromList(outp);
    }
    // Sorted(seq) / SortedFunc(seq, cmp): collect then sort.
    public static object Sorted(GoClosure seq)
    {
        var r = (GoSlice)Collect(seq);
        if (r.Len > 1) System.Array.Sort(r.Data!, r.Off, r.Len, System.Collections.Generic.Comparer<object?>.Create(OrderedCompare));
        return r;
    }
    public static object SortedFunc(GoClosure seq, GoClosure cmp)
    {
        var r = (GoSlice)Collect(seq);
        SortByClosure(r, cmp, false);
        return r;
    }

    // Insert(s, i, vals...) -> s[:i] + vals + s[i:].
    public static object Insert(object slice, long i, GoSlice vals)
    {
        var s = S(slice); int idx = (int)i;
        var outp = new System.Collections.Generic.List<object?>(s.Len + vals.Len);
        for (int k = 0; k < idx; k++) outp.Add(At(s, k));
        for (int k = 0; k < vals.Len; k++) outp.Add(vals.Data![vals.Off + k]);
        for (int k = idx; k < s.Len; k++) outp.Add(At(s, k));
        return FromList(outp);
    }
    // Delete(s, i, j) -> s[:i] + s[j:].
    public static object Delete(object slice, long i, long j)
    {
        var s = S(slice); int a = (int)i, b = (int)j;
        var outp = new System.Collections.Generic.List<object?>(s.Len - (b - a));
        for (int k = 0; k < a; k++) outp.Add(At(s, k));
        for (int k = b; k < s.Len; k++) outp.Add(At(s, k));
        return FromList(outp);
    }
    // Replace(s, i, j, vals...) -> s[:i] + vals + s[j:].
    public static object Replace(object slice, long i, long j, GoSlice vals)
    {
        var s = S(slice); int a = (int)i, b = (int)j;
        var outp = new System.Collections.Generic.List<object?>();
        for (int k = 0; k < a; k++) outp.Add(At(s, k));
        for (int k = 0; k < vals.Len; k++) outp.Add(vals.Data![vals.Off + k]);
        for (int k = b; k < s.Len; k++) outp.Add(At(s, k));
        return FromList(outp);
    }
    // DeleteFunc(s, del) -> the elements for which del returns false, in order.
    public static object DeleteFunc(object slice, GoClosure del)
    {
        var s = S(slice);
        var outp = new System.Collections.Generic.List<object?>();
        for (int k = 0; k < s.Len; k++) if (!(bool)GoRuntime.InvokeArgs(del, At(s, k))!) outp.Add(At(s, k));
        return FromList(outp);
    }
    // Repeat(s, count) -> s concatenated count times.
    public static object Repeat(object slice, long count)
    {
        var s = S(slice);
        var outp = new System.Collections.Generic.List<object?>(s.Len * (int)count);
        for (long c = 0; c < count; c++) for (int k = 0; k < s.Len; k++) outp.Add(At(s, k));
        return FromList(outp);
    }
    // Compare(s1, s2): lexicographic by element; the shorter slice is smaller if it is a prefix.
    public static long Compare(object a, object b)
    {
        var x = S(a); var y = S(b);
        int n = x.Len < y.Len ? x.Len : y.Len;
        for (int i = 0; i < n; i++) { int c = OrderedCompare(At(x, i), At(y, i)); if (c != 0) return c < 0 ? -1 : 1; }
        return x.Len == y.Len ? 0 : (x.Len < y.Len ? -1 : 1);
    }
    public static long CompareFunc(object a, object b, GoClosure cmp)
    {
        var x = S(a); var y = S(b);
        int n = x.Len < y.Len ? x.Len : y.Len;
        for (int i = 0; i < n; i++) { long c = System.Convert.ToInt64(GoRuntime.InvokeArgs(cmp, At(x, i), At(y, i))); if (c != 0) return c < 0 ? -1 : 1; }
        return x.Len == y.Len ? 0 : (x.Len < y.Len ? -1 : 1);
    }
}
