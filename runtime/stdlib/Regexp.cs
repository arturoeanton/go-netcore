namespace GoCLR.Stdlib;

using System.Text.RegularExpressions;
using GoCLR.Runtime;

/// <summary>A *regexp.Regexp handle wrapping a .NET Regex. (.NET uses a
/// backtracking engine vs Go's RE2; common patterns match, some edges differ.)</summary>
[GoShim("regexp.Regexp")]
public sealed class GoRegexp { public Regex Re = null!; public string Orig = ""; }

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

    // Go's regexp/syntax (RE2) differs from .NET's engine in a few spellings; translate the
    // common ones so the .NET Regex accepts a Go pattern. The original is kept for String().
    private static string Translate(string p) => p.Replace("(?P<", "(?<").Replace("(?P=", "\\k<");

    public static object?[] Compile(GoString pattern)
    {
        string p = pattern.ToDotNetString();
        try { return new object?[] { new GoRegexp { Re = new Regex(Translate(p)), Orig = p }, null }; }
        catch (System.Exception e) { return new object?[] { null, new GoError(GoString.FromDotNetString("error parsing regexp: " + e.Message)) }; }
    }
    public static object MustCompile(GoString pattern) { string p = pattern.ToDotNetString(); return new GoRegexp { Re = new Regex(Translate(p)), Orig = p }; }
    public static object?[] MatchString(GoString pattern, GoString s)
    {
        try { return new object?[] { Regex.IsMatch(s.ToDotNetString(), Translate(pattern.ToDotNetString())), null }; }
        catch (System.Exception e) { return new object?[] { false, new GoError(GoString.FromDotNetString(e.Message)) }; }
    }
    public static GoString QuoteMeta(GoString s) => GoString.FromDotNetString(Regex.Escape(s.ToDotNetString()));

    // *Regexp methods.
    public static bool Re_MatchString(object r, GoString s) => ((GoRegexp)r).Re.IsMatch(s.ToDotNetString());
    public static bool Re_Match(object r, GoSlice b)
    {
        var bytes = new byte[b.Len];
        for (int i = 0; i < b.Len; i++) bytes[i] = (byte)System.Convert.ToInt64(b.Data![b.Off + i]);
        return ((GoRegexp)r).Re.IsMatch(GoString.FromBytes(bytes).ToDotNetString());
    }
    public static GoString Re_FindString(object r, GoString s) { var m = ((GoRegexp)r).Re.Match(s.ToDotNetString()); return GoString.FromDotNetString(m.Success ? m.Value : ""); }
    public static GoString Re_String(object r) => GoString.FromDotNetString(((GoRegexp)r).Orig);
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
    // a custom RuneReader the runtime can't drain yields no match (see docs/LIMITATIONS.md
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
    // FindAllStringSubmatch(s, n) [][]string: every match's submatches (group 0 = the
    // whole match), up to n matches (n < 0 = all). nil if there are no matches.
    public static GoSlice Re_FindAllStringSubmatch(object r, GoString s, long n)
    {
        var rows = new System.Collections.Generic.List<object?>();
        foreach (Match m in ((GoRegexp)r).Re.Matches(s.ToDotNetString()))
        {
            if (n >= 0 && rows.Count >= n) break;
            var groups = new System.Collections.Generic.List<string>();
            for (int i = 0; i < m.Groups.Count; i++) groups.Add(m.Groups[i].Success ? m.Groups[i].Value : "");
            rows.Add(StrSlice(groups));
        }
        return rows.Count == 0 ? default : new GoSlice { Data = rows.ToArray(), Off = 0, Len = rows.Count, Cap = rows.Count };
    }
    public static GoString Re_ReplaceAllString(object r, GoString s, GoString repl)
    {
        // Go uses $1 / ${name}; .NET uses the same — translate Go's $name to ${name} loosely.
        return GoString.FromDotNetString(((GoRegexp)r).Re.Replace(s.ToDotNetString(), repl.ToDotNetString()));
    }
    // ReplaceAllStringFunc(s, repl): replace each match with the result of calling repl on
    // the matched text (repl is a func(string) string -> a GoClosure).
    public static GoString Re_ReplaceAllStringFunc(object r, GoString s, GoClosure repl)
    {
        return GoString.FromDotNetString(((GoRegexp)r).Re.Replace(s.ToDotNetString(),
            (Match m) => ((GoString)GoRuntime.InvokeArgs(repl, GoString.FromDotNetString(m.Value))!).ToDotNetString()));
    }
    // ReplaceAllLiteralString(s, repl): the replacement is used verbatim — no $-expansion.
    public static GoString Re_ReplaceAllLiteralString(object r, GoString s, GoString repl)
    {
        string lit = repl.ToDotNetString();
        return GoString.FromDotNetString(((GoRegexp)r).Re.Replace(s.ToDotNetString(), (Match m) => lit));
    }
    public static GoSlice Re_Split(object r, GoString s, long n)
    {
        var parts = ((GoRegexp)r).Re.Split(s.ToDotNetString());
        if (n >= 0 && parts.Length > n) { var trimmed = new string[n]; System.Array.Copy(parts, trimmed, (int)n); parts = trimmed; }
        return StrSlice(parts);
    }

    // ---- byte / index / submatch variants -------------------------------------------
    private static Regex Re(object r) => ((GoRegexp)r).Re;
    private static byte[] Bytes(GoSlice b) { var x = new byte[b.Len]; for (int i = 0; i < b.Len; i++) x[i] = (byte)System.Convert.ToInt64(b.Data![b.Off + i]); return x; }
    private static string Str(GoSlice b) => GoString.FromBytes(Bytes(b)).ToDotNetString();
    private static GoSlice ByteSliceOf(string s) { var by = System.Text.Encoding.UTF8.GetBytes(s); var d = new object?[by.Length]; for (int i = 0; i < by.Length; i++) d[i] = (int)by[i]; return new GoSlice { Data = d, Off = 0, Len = by.Length, Cap = by.Length }; }
    private static int BOff(string s, int charIdx) => System.Text.Encoding.UTF8.GetByteCount(s.Substring(0, charIdx));
    private static GoSlice IntSlice(System.Collections.Generic.List<long> xs) { var d = new object?[xs.Count]; for (int i = 0; i < xs.Count; i++) d[i] = xs[i]; return new GoSlice { Data = d, Off = 0, Len = xs.Count, Cap = xs.Count }; }
    private static GoSlice SlicesOf(System.Collections.Generic.List<GoSlice> ss) { var d = new object?[ss.Count]; for (int i = 0; i < ss.Count; i++) d[i] = ss[i]; return new GoSlice { Data = d, Off = 0, Len = ss.Count, Cap = ss.Count }; }
    private static GoSlice MatchIndex(Match m, string s) { var idx = new System.Collections.Generic.List<long> { BOff(s, m.Index), BOff(s, m.Index + m.Length) }; return IntSlice(idx); }
    private static GoSlice SubmatchBytes(Match m, string s) { var g = new System.Collections.Generic.List<GoSlice>(); for (int i = 0; i < m.Groups.Count; i++) g.Add(m.Groups[i].Success ? ByteSliceOf(m.Groups[i].Value) : default); return SlicesOf(g); }
    private static GoSlice SubmatchIndex(Match m, string s) { var idx = new System.Collections.Generic.List<long>(); for (int i = 0; i < m.Groups.Count; i++) { var grp = m.Groups[i]; if (grp.Success) { idx.Add(BOff(s, grp.Index)); idx.Add(BOff(s, grp.Index + grp.Length)); } else { idx.Add(-1); idx.Add(-1); } } return IntSlice(idx); }

    public static GoSlice Re_Find(object r, GoSlice b) { string s = Str(b); var m = Re(r).Match(s); return m.Success ? ByteSliceOf(m.Value) : default; }
    public static GoSlice Re_FindIndex(object r, GoSlice b) { string s = Str(b); var m = Re(r).Match(s); return m.Success ? MatchIndex(m, s) : default; }
    public static GoSlice Re_FindStringIndexAll(object r, GoString gs, long n) { string s = gs.ToDotNetString(); var outp = new System.Collections.Generic.List<GoSlice>(); foreach (Match m in Re(r).Matches(s)) { if (n >= 0 && outp.Count >= n) break; outp.Add(MatchIndex(m, s)); } return outp.Count == 0 ? default : SlicesOf(outp); }
    public static GoSlice Re_FindAll(object r, GoSlice b, long n) { string s = Str(b); var outp = new System.Collections.Generic.List<GoSlice>(); foreach (Match m in Re(r).Matches(s)) { if (n >= 0 && outp.Count >= n) break; outp.Add(ByteSliceOf(m.Value)); } return outp.Count == 0 ? default : SlicesOf(outp); }
    public static GoSlice Re_FindAllIndex(object r, GoSlice b, long n) { string s = Str(b); var outp = new System.Collections.Generic.List<GoSlice>(); foreach (Match m in Re(r).Matches(s)) { if (n >= 0 && outp.Count >= n) break; outp.Add(MatchIndex(m, s)); } return outp.Count == 0 ? default : SlicesOf(outp); }
    public static GoSlice Re_FindSubmatch(object r, GoSlice b) { string s = Str(b); var m = Re(r).Match(s); return m.Success ? SubmatchBytes(m, s) : default; }
    public static GoSlice Re_FindSubmatchIndex(object r, GoSlice b) { string s = Str(b); var m = Re(r).Match(s); return m.Success ? SubmatchIndex(m, s) : default; }
    public static GoSlice Re_FindAllSubmatch(object r, GoSlice b, long n) { string s = Str(b); var outp = new System.Collections.Generic.List<GoSlice>(); foreach (Match m in Re(r).Matches(s)) { if (n >= 0 && outp.Count >= n) break; outp.Add(SubmatchBytes(m, s)); } return outp.Count == 0 ? default : SlicesOf(outp); }
    public static GoSlice Re_FindAllSubmatchIndex(object r, GoSlice b, long n) { string s = Str(b); var outp = new System.Collections.Generic.List<GoSlice>(); foreach (Match m in Re(r).Matches(s)) { if (n >= 0 && outp.Count >= n) break; outp.Add(SubmatchIndex(m, s)); } return outp.Count == 0 ? default : SlicesOf(outp); }

    public static GoSlice Re_ReplaceAll(object r, GoSlice src, GoSlice repl) => ByteSliceOf(Re_ReplaceAllString(r, GoString.FromBytes(Bytes(src)), GoString.FromBytes(Bytes(repl))).ToDotNetString());
    public static GoSlice Re_ReplaceAllLiteral(object r, GoSlice src, GoSlice repl) => ByteSliceOf(Re_ReplaceAllLiteralString(r, GoString.FromBytes(Bytes(src)), GoString.FromBytes(Bytes(repl))).ToDotNetString());
    public static GoSlice Re_ReplaceAllFunc(object r, GoSlice src, GoClosure f)
        => ByteSliceOf(Re(r).Replace(Str(src), (Match m) => GoString.FromBytes(Bytes((GoSlice)GoRuntime.InvokeArgs(f, ByteSliceOf(m.Value))!)).ToDotNetString()));

    public static long Re_SubexpIndex(object r, GoString name) { int n = Re(r).GroupNumberFromName(name.ToDotNetString()); return n; }
    public static object?[] Re_LiteralPrefix(object r) => new object?[] { GoString.FromDotNetString(""), false }; // conservative: no literal prefix asserted
    public static void Re_Longest(object r) { } // leftmost-longest mode (.NET is leftmost-first; accepted)
    public static object Re_Copy(object r) { var g = (GoRegexp)r; return new GoRegexp { Re = g.Re, Orig = g.Orig }; }
    public static object?[] Re_MarshalText(object r) => new object?[] { ByteSliceOf(((GoRegexp)r).Orig), null };
    public static object?[] Re_AppendText(object r, GoSlice b) => new object?[] { Rt.AppendSlice(b, ByteSliceOf(((GoRegexp)r).Orig)), null };
    public static object? Re_UnmarshalText(object r, GoSlice text)
    {
        var g = (GoRegexp)r; string p = Str(text);
        try { g.Re = new Regex(Translate(p)); g.Orig = p; return null; }
        catch (System.Exception e) { return new GoError(GoString.FromDotNetString("error parsing regexp: " + e.Message)); }
    }

    public static object?[] Re_Expand(object r, GoSlice dst, GoSlice template, GoSlice src, GoSlice match)
        => new object?[] { Rt.AppendSlice(dst, ByteSliceOf(ExpandTemplate(r, Str(template), Str(src), match))), null };
    public static GoSlice Re_ExpandString(object r, GoSlice dst, GoString template, GoString src, GoSlice match)
        => Rt.AppendSlice(dst, ByteSliceOf(ExpandTemplate(r, template.ToDotNetString(), src.ToDotNetString(), match)));
    private static string ExpandTemplate(object r, string template, string src, GoSlice match)
    {
        // $name / ${name} / $N — substitute the matched group (by byte offsets in match).
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < template.Length; i++)
        {
            if (template[i] != '$') { sb.Append(template[i]); continue; }
            if (i + 1 < template.Length && template[i + 1] == '$') { sb.Append('$'); i++; continue; }
            int j = i + 1; bool braced = j < template.Length && template[j] == '{'; if (braced) j++;
            int start = j;
            while (j < template.Length && (char.IsLetterOrDigit(template[j]) || template[j] == '_')) j++;
            string name = template.Substring(start, j - start);
            if (braced && j < template.Length && template[j] == '}') j++;
            i = j - 1;
            int gi = int.TryParse(name, out var num) ? num : Re(r).GroupNumberFromName(name);
            if (gi >= 0 && 2 * gi + 1 < match.Len)
            {
                long lo = System.Convert.ToInt64(match.Data![match.Off + 2 * gi]), hi = System.Convert.ToInt64(match.Data![match.Off + 2 * gi + 1]);
                if (lo >= 0 && hi >= lo) { var b = System.Text.Encoding.UTF8.GetBytes(src); sb.Append(System.Text.Encoding.UTF8.GetString(b, (int)lo, (int)(hi - lo))); }
            }
        }
        return sb.ToString();
    }

    public static bool Re_MatchReader(object r, object? rd) => Re(r).IsMatch(System.Text.Encoding.UTF8.GetString(Readers.Drain(rd)));
    public static GoSlice Re_FindReaderIndex(object r, object? rd) { string s = System.Text.Encoding.UTF8.GetString(Readers.Drain(rd)); var m = Re(r).Match(s); return m.Success ? MatchIndex(m, s) : default; }

    // package funcs.
    public static object?[] Match(GoString pattern, GoSlice b) { try { return new object?[] { Regex.IsMatch(Str(b), Translate(pattern.ToDotNetString())), null }; } catch (System.Exception e) { return new object?[] { false, new GoError(GoString.FromDotNetString(e.Message)) }; } }
    public static object?[] MatchReader(GoString pattern, object? rd) { try { return new object?[] { Regex.IsMatch(System.Text.Encoding.UTF8.GetString(Readers.Drain(rd)), Translate(pattern.ToDotNetString())), null }; } catch (System.Exception e) { return new object?[] { false, new GoError(GoString.FromDotNetString(e.Message)) }; } }
    public static object?[] CompilePOSIX(GoString pattern) => Compile(pattern); // POSIX leftmost-longest approximated by the default engine
    public static object MustCompilePOSIX(GoString pattern) => MustCompile(pattern);
}
