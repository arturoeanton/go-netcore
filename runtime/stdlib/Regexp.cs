namespace GoCLR.Stdlib;

using System.Text.RegularExpressions;
using GoCLR.Runtime;

/// <summary>A *regexp.Regexp handle wrapping a .NET Regex. (.NET uses a
/// backtracking engine vs Go's RE2; common patterns match, some edges differ.)</summary>
[GoShim("regexp.Regexp")]
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
    public static GoSlice Re_FindAllStringSubmatchIndex(object r, GoString gs, long n)
    {
        var str = gs.ToDotNetString();
        var rows = new System.Collections.Generic.List<object?>();
        foreach (Match m in ((GoRegexp)r).Re.Matches(str))
        {
            if (n >= 0 && rows.Count >= n) break;
            var idxs = new System.Collections.Generic.List<object?>();
            for (int g = 0; g < m.Groups.Count; g++)
            {
                var grp = m.Groups[g];
                if (grp.Success)
                {
                    int start = System.Text.Encoding.UTF8.GetByteCount(str.Substring(0, grp.Index));
                    int end = start + System.Text.Encoding.UTF8.GetByteCount(grp.Value);
                    idxs.Add((long)start); idxs.Add((long)end);
                }
                else { idxs.Add(-1L); idxs.Add(-1L); }
            }
            rows.Add(new GoSlice { Data = idxs.ToArray(), Off = 0, Len = idxs.Count, Cap = idxs.Count });
        }
        if (rows.Count == 0) return new GoSlice { Data = null, Off = 0, Len = 0, Cap = 0 }; // nil
        return new GoSlice { Data = rows.ToArray(), Off = 0, Len = rows.Count, Cap = rows.Count };
    }
    // FindStringSubmatchIndex: the first match's group byte-offset pairs as a flat
    // []int (g0.start,g0.end, g1.start,g1.end, ...), or nil if no match.
    public static GoSlice Re_FindStringSubmatchIndex(object r, GoString gs)
    {
        var str = gs.ToDotNetString();
        var m = ((GoRegexp)r).Re.Match(str);
        if (!m.Success) return new GoSlice { Data = null, Off = 0, Len = 0, Cap = 0 };
        var idxs = new System.Collections.Generic.List<object?>();
        for (int g = 0; g < m.Groups.Count; g++)
        {
            var grp = m.Groups[g];
            if (grp.Success)
            {
                int start = System.Text.Encoding.UTF8.GetByteCount(str.Substring(0, grp.Index));
                int end = start + System.Text.Encoding.UTF8.GetByteCount(grp.Value);
                idxs.Add((long)start); idxs.Add((long)end);
            }
            else { idxs.Add(-1L); idxs.Add(-1L); }
        }
        return new GoSlice { Data = idxs.ToArray(), Off = 0, Len = idxs.Count, Cap = idxs.Count };
    }

    // SubexpNames: index 0 is "" (the whole match); each i is group i's name, or ""
    // when the group is unnamed (matching Go's ordering for purely numbered groups).
    public static GoSlice Re_SubexpNames(object r)
    {
        var re = ((GoRegexp)r).Re;
        var nums = re.GetGroupNumbers();
        var names = new object?[nums.Length];
        for (int i = 0; i < nums.Length; i++)
        {
            string nm = re.GroupNameFromNumber(nums[i]);
            // .NET names an unnamed group with its number; Go reports "".
            names[i] = GoString.FromDotNetString(nm == nums[i].ToString(System.Globalization.CultureInfo.InvariantCulture) ? "" : nm);
        }
        return new GoSlice { Data = names, Off = 0, Len = names.Length, Cap = names.Length };
    }

    public static long Re_NumSubexp(object r) => ((GoRegexp)r).Re.GetGroupNumbers().Length - 1;

    // FindReaderSubmatchIndex: match against an io.RuneReader and return rune-offset
    // group index pairs. The reader is drained through the runtime's reader protocol;
    // a custom RuneReader the runtime can't drain yields no match (see LIMITATIONS.md
    // — this only affects full-Unicode RE2 matching over non-standard readers).
    public static GoSlice Re_FindReaderSubmatchIndex(object r, object? reader)
    {
        var bytes = Readers.Drain(reader);
        string str = System.Text.Encoding.UTF8.GetString(bytes);
        var m = ((GoRegexp)r).Re.Match(str);
        if (!m.Success) return new GoSlice { Data = null, Off = 0, Len = 0, Cap = 0 };
        var idxs = new System.Collections.Generic.List<object?>();
        for (int g = 0; g < m.Groups.Count; g++)
        {
            var grp = m.Groups[g];
            // Rune offsets: count code points up to the UTF-16 index.
            if (grp.Success)
            {
                long start = RuneIndex(str, grp.Index);
                long end = start + RuneIndex(grp.Value, grp.Value.Length);
                idxs.Add(start); idxs.Add(end);
            }
            else { idxs.Add(-1L); idxs.Add(-1L); }
        }
        return new GoSlice { Data = idxs.ToArray(), Off = 0, Len = idxs.Count, Cap = idxs.Count };
    }

    private static long RuneIndex(string s, int utf16Index)
    {
        long runes = 0;
        for (int i = 0; i < utf16Index && i < s.Length; i++)
        {
            if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1])) i++;
            runes++;
        }
        return runes;
    }

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
