namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>Shim for Go's <c>cmp</c> package (Ordered comparison helpers). The constraint
/// erases to a boxed value, so these reduce to Slices.OrderedCompare.</summary>
public static class Cmp
{
    // Go's cmp.Compare returns exactly -1, 0, or +1 (not the raw comparator value, which for
    // strings can be ±2); normalize to the sign.
    public static long Compare(object? x, object? y) => System.Math.Sign(Slices.OrderedCompare(x, y));
    public static bool Less(object? x, object? y) => Slices.OrderedCompare(x, y) < 0;
    // cmp.Or(vals...): the first non-zero argument, else the zero value (the last one).
    public static object? Or(GoSlice vals)
    {
        object? zero = null;
        for (int i = 0; i < vals.Len; i++)
        {
            var v = vals.Data![vals.Off + i];
            zero = v;
            if (!IsZero(v)) return v;
        }
        return zero;
    }
    private static bool IsZero(object? v) => v switch
    {
        null => true,
        GoString g => g.Len == 0,
        long l => l == 0,
        ulong u => u == 0,
        int i => i == 0,
        double d => d == 0,
        float f => f == 0,
        bool b => !b,
        _ => false,
    };
}
