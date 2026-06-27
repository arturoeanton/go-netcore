namespace GoCLR.Stdlib;

using System;
using System.Collections.Generic;
using System.Reflection;

/// <summary>Marks a CLR class as the runtime representation of an opaque Go shim type
/// (e.g. <c>[GoShim("syscall.Signal")]</c> on GoSignal). The lowering keeps shim values
/// boxed as System.Object, so this self-declared map is how a type switch or an
/// interface dispatch recovers a shim value's concrete Go type — without goclr hardcoding
/// any specific type. A class may back several Go types (a net address class). </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class GoShimAttribute : Attribute
{
    public string GoName { get; }
    public GoShimAttribute(string goName) => GoName = goName;
}

/// <summary>The shim type ↔ CLR class registry, built once by reflecting the stdlib
/// assembly for [GoShim] annotations.</summary>
public static class ShimTypes
{
    private static readonly Dictionary<Type, HashSet<string>> _clrToGo = new();
    private static readonly HashSet<string> _goNames = new();
    private static bool _scanned;
    private static readonly object _lock = new();

    // Normalize a Go type name to its short "pkgbase.Type" form, dropping any import-path
    // prefix: "encoding/xml.StartElement" -> "xml.StartElement". [GoShim] annotations are
    // written inconsistently (some short, some full import path) and the lowering always
    // queries with the full path, so all comparisons normalize through this — which is also
    // exactly the form Go's %T prints.
    private static string Short(string n)
    {
        int dot = n.LastIndexOf('.');
        if (dot < 0) return n;
        string pkg = n.Substring(0, dot);
        int slash = pkg.LastIndexOf('/');
        if (slash >= 0) pkg = pkg.Substring(slash + 1);
        return pkg + n.Substring(dot);
    }

    private static bool Has(HashSet<string> names, string goName)
    {
        string q = Short(goName);
        foreach (var n in names) if (Short(n) == q) return true;
        return false;
    }

    private static void EnsureScanned()
    {
        if (_scanned) return;
        lock (_lock)
        {
            if (_scanned) return;
            foreach (var t in typeof(GoShimAttribute).Assembly.GetTypes())
            {
                var attrs = t.GetCustomAttributes<GoShimAttribute>(false);
                foreach (var a in attrs)
                {
                    if (!_clrToGo.TryGetValue(t, out var set)) _clrToGo[t] = set = new();
                    set.Add(a.GoName);
                    _goNames.Add(Short(a.GoName));
                }
            }
            _scanned = true;
        }
    }

    /// <summary>Whether v is the shim value of exactly goName. For an annotated shim the
    /// answer is precise (its CLR class is registered); for any other object it is false
    /// when goName itself names a known shim (v is a different type) and otherwise true,
    /// preserving the "any non-primitive" heuristic for shim types not yet annotated.</summary>
    public static bool Is(object v, string goName)
    {
        EnsureScanned();
        if (_clrToGo.TryGetValue(v.GetType(), out var names)) return Has(names, goName);
        return !_goNames.Contains(Short(goName));
    }

    /// <summary>Whether v is *precisely* the shim value of goName: its CLR class must be
    /// a registered [GoShim] for goName. Unlike Is, an unannotated object never matches —
    /// required where a false positive would mis-dispatch (interface method routing).</summary>
    public static bool IsStrict(object v, string goName)
    {
        EnsureScanned();
        return _clrToGo.TryGetValue(v.GetType(), out var names) && Has(names, goName);
    }

    /// <summary>Whether v's CLR class is a registered shim type at all.</summary>
    public static bool IsAnyShim(object v)
    {
        EnsureScanned();
        return _clrToGo.ContainsKey(v.GetType());
    }

    /// <summary>The Go type name of a shim value (e.g. GoXmlName -> "xml.Name"), or null if
    /// its CLR class is not a registered shim. Used by fmt's %T / %#v so a shim struct names
    /// its Go type rather than the internal CLR class. The first registered name is returned
    /// (types backing several Go names are rare and ambiguous under %T anyway).</summary>
    public static string? GoNameOf(object v)
    {
        EnsureScanned();
        if (_clrToGo.TryGetValue(v.GetType(), out var names))
            foreach (var n in names) return n;
        return null;
    }
}
