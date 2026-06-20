namespace GoCLR.Stdlib;

using System;
using GoCLR.Runtime;

/// <summary>Shim for Go's <c>sort</c> package (concrete sorters; in-place over the
/// slice's shared backing array, like Go).</summary>
public static class Sort
{
    public static void Ints(GoSlice a)
    {
        Array.Sort(a.Data, a.Off, a.Len, System.Collections.Generic.Comparer<object?>.Create(
            (x, y) => Convert.ToInt64(x).CompareTo(Convert.ToInt64(y))));
    }
    public static void Float64s(GoSlice a)
    {
        Array.Sort(a.Data, a.Off, a.Len, System.Collections.Generic.Comparer<object?>.Create(
            (x, y) => Convert.ToDouble(x).CompareTo(Convert.ToDouble(y))));
    }
    public static void Strings(GoSlice a)
    {
        Array.Sort(a.Data, a.Off, a.Len, System.Collections.Generic.Comparer<object?>.Create(
            (x, y) => string.CompareOrdinal(((GoString)x!).ToDotNetString(), ((GoString)y!).ToDotNetString())));
    }

    public static bool IntsAreSorted(GoSlice a)
    {
        for (int i = a.Off + 1; i < a.Off + a.Len; i++)
            if (Convert.ToInt64(a.Data[i - 1]) > Convert.ToInt64(a.Data[i])) return false;
        return true;
    }

    public static long SearchInts(GoSlice a, long x)
    {
        long lo = 0, hi = a.Len;
        while (lo < hi)
        {
            long mid = (lo + hi) / 2;
            if (Convert.ToInt64(a.Data[a.Off + (int)mid]) < x) lo = mid + 1; else hi = mid;
        }
        return lo;
    }

    public static bool Float64sAreSorted(GoSlice a)
    {
        for (int i = a.Off + 1; i < a.Off + a.Len; i++)
            if (Convert.ToDouble(a.Data[i - 1]) > Convert.ToDouble(a.Data[i])) return false;
        return true;
    }
    public static bool StringsAreSorted(GoSlice a)
    {
        for (int i = a.Off + 1; i < a.Off + a.Len; i++)
            if (string.CompareOrdinal(((GoString)a.Data[i - 1]!).ToDotNetString(), ((GoString)a.Data[i]!).ToDotNetString()) > 0) return false;
        return true;
    }

    public static long SearchStrings(GoSlice a, GoString x)
    {
        string xs = x.ToDotNetString();
        long lo = 0, hi = a.Len;
        while (lo < hi)
        {
            long mid = (lo + hi) / 2;
            if (string.CompareOrdinal(((GoString)a.Data[a.Off + (int)mid]!).ToDotNetString(), xs) < 0) lo = mid + 1; else hi = mid;
        }
        return lo;
    }
    public static long SearchFloat64s(GoSlice a, double x)
    {
        long lo = 0, hi = a.Len;
        while (lo < hi)
        {
            long mid = (lo + hi) / 2;
            if (Convert.ToDouble(a.Data[a.Off + (int)mid]) < x) lo = mid + 1; else hi = mid;
        }
        return lo;
    }

    // sort.Search(n, f func(int) bool) — generic binary search with a predicate.
    public static long Search(long n, GoClosure f)
    {
        var c = f;
        long lo = 0, hi = n;
        while (lo < hi)
        {
            long mid = (lo + hi) / 2;
            bool ok = (bool)GoRuntime.InvokeArgs(c, mid)!;
            if (!ok) lo = mid + 1; else hi = mid;
        }
        return lo;
    }

    // sort.Slice(slice, less func(i, j int) bool) — in-place sort with a comparator.
    public static void Slice(object slice, GoClosure less)
    {
        var s = (GoSlice)slice;
        var c = less;
        // index-based insertion-style via Array.Sort with a comparator that calls less.
        var idx = new int[s.Len];
        for (int i = 0; i < s.Len; i++) idx[i] = i;
        // Build a stable-ish order by comparing through the closure on original indices.
        System.Array.Sort(idx, (i, j) =>
        {
            bool lij = (bool)GoRuntime.InvokeArgs(c, (long)i, (long)j)!;
            if (lij) return -1;
            bool lji = (bool)GoRuntime.InvokeArgs(c, (long)j, (long)i)!;
            return lji ? 1 : 0;
        });
        var copy = new object?[s.Len];
        for (int i = 0; i < s.Len; i++) copy[i] = s.Data[s.Off + idx[i]];
        for (int i = 0; i < s.Len; i++) s.Data[s.Off + i] = copy[i];
    }
}
