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
}
