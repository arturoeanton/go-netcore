namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>Shim for Go's <c>cmp</c> package (Ordered comparison helpers). The constraint
/// erases to a boxed value, so these reduce to Slices.OrderedCompare.</summary>
public static class Cmp
{
    public static long Compare(object? x, object? y) => Slices.OrderedCompare(x, y);
    public static bool Less(object? x, object? y) => Slices.OrderedCompare(x, y) < 0;
}
