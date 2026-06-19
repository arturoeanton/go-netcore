namespace GoCLR.Runtime;

/// <summary>
/// GoMap is the non-generic map the M1 backend emits: a reference type (Go maps
/// are reference types) wrapping a Dictionary keyed/valued by boxed elements, so
/// one representation serves every key/value type without .NET generics. A nil
/// map is a null GoMap reference; the <see cref="GoMaps"/> helpers tolerate it.
/// </summary>
public sealed class GoMap
{
    public Dictionary<object, object?>? Data;
}

/// <summary>GoMap operations the compiler calls into (null-safe for nil maps).</summary>
public static class GoMaps
{
    /// <summary>make(map[K]V).</summary>
    public static GoMap Make() => new() { Data = new Dictionary<object, object?>() };

    /// <summary>len(m).</summary>
    public static long Len(GoMap? m) => (m?.Data)?.Count ?? 0;

    /// <summary>m[k] — returns the boxed value, or the boxed zero when absent (Go semantics).</summary>
    public static object? Get(GoMap? m, object key, object? zero) =>
        m?.Data != null && m.Data.TryGetValue(key, out var v) ? v : zero;

    /// <summary>The ok of v, ok := m[k].</summary>
    public static bool Contains(GoMap? m, object key) => m?.Data != null && m.Data.ContainsKey(key);

    /// <summary>m[k] = v. Panics on a nil map, like Go.</summary>
    public static void Set(GoMap? m, object key, object? val)
    {
        if (m?.Data == null)
            throw new GoPanicException(GoString.FromDotNetString("assignment to entry in nil map"));
        m.Data[key] = val;
    }

    /// <summary>delete(m, k).</summary>
    public static void Delete(GoMap? m, object key)
    {
        if (m?.Data != null) m.Data.Remove(key);
    }

    /// <summary>The keys of m as a slice (for range-over-map). Order is unspecified, as in Go.</summary>
    public static GoSlice Keys(GoMap? m)
    {
        if (m?.Data == null)
            return new GoSlice { Data = System.Array.Empty<object?>(), Off = 0, Len = 0, Cap = 0 };
        var keys = new object?[m.Data.Count];
        int i = 0;
        foreach (var k in m.Data.Keys) keys[i++] = k;
        return new GoSlice { Data = keys, Off = 0, Len = keys.Length, Cap = keys.Length };
    }
}
