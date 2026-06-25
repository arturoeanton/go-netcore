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

/// <summary>Map-key equality with Go value semantics. The default Dictionary comparer
/// compares an array key (a GoSlice) and a struct key holding array fields by reference,
/// so equal-by-value keys miss; ValueType.GetHashCode also can't recurse into array
/// fields reliably. This comparer hashes/compares arrays element-wise, named values by
/// (id, value), and structs field-by-field, while scalars/strings/pointers fall back to
/// their own equality (pointers keep Go's identity semantics).</summary>
public sealed class GoKeyComparer : IEqualityComparer<object>
{
    public static readonly GoKeyComparer Instance = new();

    public new bool Equals(object? a, object? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        switch (a)
        {
            case GoSlice sa when b is GoSlice sb:
                if (sa.Len != sb.Len) return false;
                for (int i = 0; i < sa.Len; i++)
                    if (!Equals(sa.Data?[sa.Off + i], sb.Data?[sb.Off + i])) return false;
                return true;
            case GoNamed na when b is GoNamed nb:
                return na.TypeId == nb.TypeId && Equals(na.Value, nb.Value);
            case GoComplex ca when b is GoComplex cb:
                return ca.Re == cb.Re && ca.Im == cb.Im;
        }
        var t = a.GetType();
        if (t != b.GetType()) return false;
        if (IsGoStruct(t))
        {
            foreach (var f in t.GetFields(FieldFlags))
                if (!Equals(f.GetValue(a), f.GetValue(b))) return false;
            return true;
        }
        return a.Equals(b);
    }

    public int GetHashCode(object o)
    {
        switch (o)
        {
            case GoSlice s:
            {
                var h = new System.HashCode();
                h.Add(s.Len);
                for (int i = 0; i < s.Len; i++) h.Add(GetHashCode(s.Data?[s.Off + i] ?? NilKey));
                return h.ToHashCode();
            }
            case GoNamed n:
                return System.HashCode.Combine(n.TypeId, GetHashCode(n.Value ?? NilKey));
            case GoComplex c:
                return System.HashCode.Combine(c.Re, c.Im);
        }
        var t = o.GetType();
        if (IsGoStruct(t))
        {
            var h = new System.HashCode();
            foreach (var f in t.GetFields(FieldFlags)) h.Add(GetHashCode(f.GetValue(o) ?? NilKey));
            return h.ToHashCode();
        }
        return o.GetHashCode();
    }

    private static readonly object NilKey = new();
    private const System.Reflection.BindingFlags FieldFlags =
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

    // A goclr struct value (compared field-by-field): a value type that is not a primitive,
    // enum, or one of the runtime's own scalar/array carriers (handled above or by default).
    private static bool IsGoStruct(System.Type t) =>
        t.IsValueType && !t.IsPrimitive && !t.IsEnum && t != typeof(GoString) && t != typeof(GoSlice);
}

/// <summary>GoMap operations the compiler calls into (null-safe for nil maps).</summary>
public static class GoMaps
{
    /// <summary>make(map[K]V).</summary>
    public static GoMap Make() => new() { Data = new Dictionary<object, object?>(GoKeyComparer.Instance) };

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
