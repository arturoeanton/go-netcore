namespace GoCLR.Stdlib;

using System.Text.RegularExpressions;
using GoCLR.Runtime;

/// <summary>A *regexp.Regexp handle wrapping a .NET Regex. (.NET uses a
/// backtracking engine vs Go's RE2; common patterns match, some edges differ.)</summary>
public sealed class GoRegexp { public Regex Re = null!; }

/// <summary>Shim for a subset of Go's <c>regexp</c> package.</summary>
public static class Regexp
{
    private static GoSlice StrSlice(System.Collections.Generic.IEnumerable<string> items)
    {
        var list = new System.Collections.Generic.List<object?>();
        foreach (var s in items) list.Add(GoString.FromDotNetString(s));
        var d = list.ToArray();
        return new GoSlice { Data = d, Off = 0, Len = d.Length, Cap = d.Length };
    }

    public static object?[] Compile(GoString pattern)
    {
        try { return new object?[] { new GoRegexp { Re = new Regex(pattern.ToDotNetString()) }, null }; }
        catch (System.Exception e) { return new object?[] { null, new GoError(GoString.FromDotNetString("error parsing regexp: " + e.Message)) }; }
    }
    public static object MustCompile(GoString pattern) => new GoRegexp { Re = new Regex(pattern.ToDotNetString()) };
    public static object?[] MatchString(GoString pattern, GoString s)
    {
        try { return new object?[] { Regex.IsMatch(s.ToDotNetString(), pattern.ToDotNetString()), null }; }
        catch (System.Exception e) { return new object?[] { false, new GoError(GoString.FromDotNetString(e.Message)) }; }
    }
    public static GoString QuoteMeta(GoString s) => GoString.FromDotNetString(Regex.Escape(s.ToDotNetString()));

    // *Regexp methods.
    public static bool Re_MatchString(object r, GoString s) => ((GoRegexp)r).Re.IsMatch(s.ToDotNetString());
    public static GoString Re_FindString(object r, GoString s) { var m = ((GoRegexp)r).Re.Match(s.ToDotNetString()); return GoString.FromDotNetString(m.Success ? m.Value : ""); }
    public static GoString Re_String(object r) => GoString.FromDotNetString(((GoRegexp)r).Re.ToString());
    public static GoSlice Re_FindStringIndex(object r, GoString gs)
    {
        var str = gs.ToDotNetString();
        var m = ((GoRegexp)r).Re.Match(str);
        if (!m.Success) return new GoSlice { Data = null, Off = 0, Len = 0, Cap = 0 }; // nil
        int start = System.Text.Encoding.UTF8.GetByteCount(str.Substring(0, m.Index));
        int end = start + System.Text.Encoding.UTF8.GetByteCount(m.Value);
        return new GoSlice { Data = new object?[] { (long)start, (long)end }, Off = 0, Len = 2, Cap = 2 };
    }
    public static GoSlice Re_FindStringSubmatch(object r, GoString s)
    {
        var m = ((GoRegexp)r).Re.Match(s.ToDotNetString());
        if (!m.Success) return default;
        var items = new System.Collections.Generic.List<string>();
        for (int i = 0; i < m.Groups.Count; i++) items.Add(m.Groups[i].Success ? m.Groups[i].Value : "");
        return StrSlice(items);
    }
    public static GoSlice Re_FindAllString(object r, GoString s, long n)
    {
        var items = new System.Collections.Generic.List<string>();
        foreach (Match m in ((GoRegexp)r).Re.Matches(s.ToDotNetString())) { if (n >= 0 && items.Count >= n) break; items.Add(m.Value); }
        return items.Count == 0 ? default : StrSlice(items);
    }
    public static GoString Re_ReplaceAllString(object r, GoString s, GoString repl)
    {
        // Go uses $1 / ${name}; .NET uses the same — translate Go's $name to ${name} loosely.
        return GoString.FromDotNetString(((GoRegexp)r).Re.Replace(s.ToDotNetString(), repl.ToDotNetString()));
    }
    public static GoSlice Re_Split(object r, GoString s, long n)
    {
        var parts = ((GoRegexp)r).Re.Split(s.ToDotNetString());
        if (n >= 0 && parts.Length > n) { var trimmed = new string[n]; System.Array.Copy(parts, trimmed, (int)n); parts = trimmed; }
        return StrSlice(parts);
    }
}
