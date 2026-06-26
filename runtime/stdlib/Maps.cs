namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>Shim for the non-iterator functions of Go's <c>maps</c> package. The iterator-based
/// Keys/Values/All (returning iter.Seq) are not provided. Keys/values erase to boxed values
/// inside the GoMap, so these reduce to operations over the backing dictionary.</summary>
public static class Maps
{
    private static GoMap M(object o) => (GoMap)(o is GoNamed n ? n.Value : o)!;

    // maps.Clone(m): a shallow copy (nil stays nil).
    public static object? Clone(object m)
    {
        var s = M(m);
        if (s.Data == null) return null; // a nil map clones to nil
        var d = GoMaps.Make();
        foreach (var kv in s.Data) d.Data![kv.Key] = kv.Value;
        return d;
    }
    // maps.Copy(dst, src): copy every src entry into dst (overwriting).
    public static void Copy(object dst, object src)
    {
        var d = M(dst); var s = M(src);
        if (s.Data == null) return;
        d.Data ??= new System.Collections.Generic.Dictionary<object, object?>(GoKeyComparer.Instance);
        foreach (var kv in s.Data) d.Data[kv.Key] = kv.Value;
    }
    public static bool Equal(object a, object b)
    {
        var x = M(a); var y = M(b);
        int xn = x.Data?.Count ?? 0, yn = y.Data?.Count ?? 0;
        if (xn != yn) return false;
        if (xn == 0) return true;
        foreach (var kv in x.Data!)
        {
            if (!y.Data!.TryGetValue(kv.Key, out var yv)) return false;
            if (!GoKeyComparer.Instance.Equals(kv.Value!, yv!)) return false;
        }
        return true;
    }
    public static bool EqualFunc(object a, object b, GoClosure eq)
    {
        var x = M(a); var y = M(b);
        int xn = x.Data?.Count ?? 0, yn = y.Data?.Count ?? 0;
        if (xn != yn) return false;
        if (xn == 0) return true;
        foreach (var kv in x.Data!)
        {
            if (!y.Data!.TryGetValue(kv.Key, out var yv)) return false;
            if (!(bool)GoRuntime.InvokeArgs(eq, kv.Value, yv)!) return false;
        }
        return true;
    }
    // --- iterators (iter.Seq) ---
    // maps.Keys(m): an iter.Seq[K] over the keys (Go's order is unspecified).
    public static GoClosure Keys(object m)
    {
        var s = M(m);
        return NativeClosures.Make(a =>
        {
            var yield = (GoClosure)a![0]!;
            if (s.Data != null) foreach (var kv in s.Data) if (!(bool)GoRuntime.InvokeArgs(yield, kv.Key)!) break;
            return null;
        });
    }
    // maps.Values(m): an iter.Seq[V] over the values.
    public static GoClosure Values(object m)
    {
        var s = M(m);
        return NativeClosures.Make(a =>
        {
            var yield = (GoClosure)a![0]!;
            if (s.Data != null) foreach (var kv in s.Data) if (!(bool)GoRuntime.InvokeArgs(yield, kv.Value)!) break;
            return null;
        });
    }
    // maps.All(m): an iter.Seq2[K, V] over the entries.
    public static GoClosure All(object m)
    {
        var s = M(m);
        return NativeClosures.Make(a =>
        {
            var yield = (GoClosure)a![0]!;
            if (s.Data != null) foreach (var kv in s.Data) if (!(bool)GoRuntime.InvokeArgs(yield, kv.Key, kv.Value)!) break;
            return null;
        });
    }

    // maps.DeleteFunc(m, del): remove every entry for which del(k, v) is true.
    public static void DeleteFunc(object m, GoClosure del)
    {
        var s = M(m);
        if (s.Data == null) return;
        var toRemove = new System.Collections.Generic.List<object>();
        foreach (var kv in s.Data) if ((bool)GoRuntime.InvokeArgs(del, kv.Key, kv.Value)!) toRemove.Add(kv.Key);
        foreach (var k in toRemove) s.Data.Remove(k);
    }
}
