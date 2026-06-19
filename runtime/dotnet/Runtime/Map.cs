namespace GoCLR.Runtime;

/// <summary>
/// GoMap models a Go map. It is a reference type (like Go's map header) so that
/// copies share state. The nil map (null Data) supports reads and len but panics
/// on assignment, matching Go.
/// </summary>
public sealed class GoMap<TK, TV> where TK : notnull
{
    public Dictionary<TK, TV>? Data;

    public GoMap() { }
    private GoMap(Dictionary<TK, TV> data) { Data = data; }

    /// <summary>make(map[TK]TV).</summary>
    public static GoMap<TK, TV> Make(int hint = 0) =>
        new(new Dictionary<TK, TV>(hint > 0 ? hint : 0));

    /// <summary>The nil map literal.</summary>
    public static GoMap<TK, TV> Nil => new();

    public bool IsNil => Data == null;

    public int Len => Data?.Count ?? 0;

    /// <summary>m[k] — returns the zero value when absent (Go semantics).</summary>
    public TV Get(TK key)
    {
        if (Data != null && Data.TryGetValue(key, out var v)) return v;
        return default!;
    }

    /// <summary>v, ok := m[k].</summary>
    public (TV value, bool ok) Get2(TK key)
    {
        if (Data != null && Data.TryGetValue(key, out var v)) return (v, true);
        return (default!, false);
    }

    /// <summary>m[k] = v. Panics on a nil map, like Go.</summary>
    public void Set(TK key, TV value)
    {
        if (Data == null)
            throw new GoPanicException(GoString.FromDotNetString("assignment to entry in nil map"));
        Data[key] = value;
    }

    /// <summary>delete(m, k).</summary>
    public void Delete(TK key) => Data?.Remove(key);

    /// <summary>Keys for range iteration (order is intentionally unspecified, as in Go).</summary>
    public IEnumerable<TK> Keys => Data?.Keys ?? Enumerable.Empty<TK>();

    public IEnumerable<KeyValuePair<TK, TV>> Entries => Data ?? Enumerable.Empty<KeyValuePair<TK, TV>>();
}
