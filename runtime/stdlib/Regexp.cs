namespace GoCLR.Stdlib;

using System.Text.RegularExpressions;
using GoCLR.Runtime;

/// <summary>A *regexp.Regexp handle wrapping a .NET Regex. (.NET uses a
/// backtracking engine vs Go's RE2; common patterns match, some edges differ.)</summary>
[GoShim("regexp.Regexp")]
public sealed class GoRegexp
{
    public Regex Re = null!;
    public string Orig = "";
    private int[]? _map;     // Go group index -> .NET group number
    private string[]? _names; // Go group index -> name ("" when unnamed); index 0 = ""
    // Go numbers capturing groups left-to-right by opening paren; .NET numbers unnamed
    // groups first then named. For patterns mixing the two the orders differ, so map
    // every Go index onto the matching .NET group. Built lazily from the original pattern.
    public int[] Map { get { Build(); return _map!; } }
    public string[] Names { get { Build(); return _names!; } }
    private void Build()
    {
        if (_map != null) return;
        var names = new System.Collections.Generic.List<string>();
        string p = Orig; bool inClass = false;
        for (int i = 0; i < p.Length; i++)
        {
            char c = p[i];
            if (c == '\\') { i++; continue; }
            if (inClass) { if (c == ']') inClass = false; continue; }
            if (c == '[') { inClass = true; continue; }
            if (c != '(') continue;
            if (i + 1 < p.Length && p[i + 1] == '?')
            {
                int k = i + 2;
                if (k + 1 < p.Length && p[k] == 'P' && p[k + 1] == '<')
                { int s = k + 2, e = p.IndexOf('>', s); if (e > 0) names.Add(p.Substring(s, e - s)); }
                else if (k < p.Length && p[k] == '<' && k + 1 < p.Length && p[k + 1] != '=' && p[k + 1] != '!')
                { int s = k + 1, e = p.IndexOf('>', s); if (e > 0) names.Add(p.Substring(s, e - s)); }
                else if (k < p.Length && p[k] == '\'')
                { int s = k + 1, e = p.IndexOf('\'', s); if (e > 0) names.Add(p.Substring(s, e - s)); }
                // else (?:  (?=  (?!  (?i)  (?i:  (?<=  (?<!  (?P=name)  -> non-capturing
            }
            else names.Add(""); // plain unnamed capturing group
        }
        var map = new int[names.Count + 1];
        var nm = new string[names.Count + 1];
        map[0] = 0; nm[0] = "";
        int unnamed = 0;
        for (int gi = 1; gi <= names.Count; gi++)
        {
            string name = names[gi - 1];
            nm[gi] = name;
            map[gi] = name.Length == 0 ? ++unnamed : Re.GroupNumberFromName(name);
        }
        _names = nm; _map = map;
    }
    public Group G(Match m, int goIndex) => m.Groups[Map[goIndex]];
    public int Count => Map.Length;
    public int GoIndexOfName(string name) { var ns = Names; for (int i = 0; i < ns.Length; i++) if (ns[i] == name) return i; return -1; }
}

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
    private static string Translate(string p) => TranslatePosix(p.Replace("(?P<", "(?<").Replace("(?P=", "\\k<"));

    // POSIX character classes ([[:digit:]], [[:alpha:]], …) are valid in RE2 but unknown to
    // .NET's engine; expand each to the equivalent ASCII ranges inside the bracket. Negated
    // forms ([:^digit:]) are rare and left untranslated.
    private static readonly System.Collections.Generic.Dictionary<string, string> _posixClasses = new()
    {
        ["[:alpha:]"] = "a-zA-Z", ["[:digit:]"] = "0-9", ["[:alnum:]"] = "a-zA-Z0-9",
        ["[:upper:]"] = "A-Z", ["[:lower:]"] = "a-z", ["[:space:]"] = @" \t\n\r\f\x0B",
        ["[:blank:]"] = @" \t", ["[:xdigit:]"] = "0-9A-Fa-f", ["[:word:]"] = "0-9A-Za-z_",
        ["[:punct:]"] = @"!-/:-@[-`{-~",
        ["[:cntrl:]"] = @"\x00-\x1f\x7f", ["[:graph:]"] = @"\x21-\x7e", ["[:print:]"] = @"\x20-\x7e",
    };
    private static string TranslatePosix(string p)
    {
        if (!p.Contains("[:")) return p;
        foreach (var kv in _posixClasses) p = p.Replace(kv.Key, kv.Value);
        return p;
    }

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
        var gx = (GoRegexp)r;
        var rows = new System.Collections.Generic.List<object?>();
        foreach (Match m in GoMatches(gx.Re, str, n))
        {
            if (n >= 0 && rows.Count >= n) break;
            var idxs = new System.Collections.Generic.List<object?>();
            for (int g = 0; g < gx.Count; g++)
            {
                var grp = gx.G(m, g);
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
        var gx = (GoRegexp)r;
        var m = gx.Re.Match(str);
        if (!m.Success) return new GoSlice { Data = null, Off = 0, Len = 0, Cap = 0 };
        var idxs = new System.Collections.Generic.List<object?>();
        for (int g = 0; g < gx.Count; g++)
        {
            var grp = gx.G(m, g);
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
        var gx = (GoRegexp)r;
        var src = gx.Names; // Go order, index 0 = "" (whole match)
        var names = new object?[src.Length];
        for (int i = 0; i < src.Length; i++) names[i] = GoString.FromDotNetString(src[i]);
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
        var gx = (GoRegexp)r;
        var m = gx.Re.Match(str);
        if (!m.Success) return new GoSlice { Data = null, Off = 0, Len = 0, Cap = 0 };
        var idxs = new System.Collections.Generic.List<object?>();
        for (int g = 0; g < gx.Count; g++)
        {
            var grp = gx.G(m, g);
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
        var gx = (GoRegexp)r;
        var m = gx.Re.Match(s.ToDotNetString());
        if (!m.Success) return default;
        var items = new System.Collections.Generic.List<string>();
        for (int i = 0; i < gx.Count; i++) { var grp = gx.G(m, i); items.Add(grp.Success ? grp.Value : ""); }
        return StrSlice(items);
    }
    public static GoSlice Re_FindAllString(object r, GoString s, long n)
    {
        var items = new System.Collections.Generic.List<string>();
        foreach (Match m in GoMatches(((GoRegexp)r).Re, s.ToDotNetString(), n)) { if (n >= 0 && items.Count >= n) break; items.Add(m.Value); }
        return items.Count == 0 ? default : StrSlice(items);
    }
    // FindAllStringSubmatch(s, n) [][]string: every match's submatches (group 0 = the
    // whole match), up to n matches (n < 0 = all). nil if there are no matches.
    public static GoSlice Re_FindAllStringSubmatch(object r, GoString s, long n)
    {
        var gx = (GoRegexp)r;
        var rows = new System.Collections.Generic.List<object?>();
        foreach (Match m in GoMatches(gx.Re, s.ToDotNetString(), n))
        {
            if (n >= 0 && rows.Count >= n) break;
            var groups = new System.Collections.Generic.List<string>();
            for (int i = 0; i < gx.Count; i++) { var grp = gx.G(m, i); groups.Add(grp.Success ? grp.Value : ""); }
            rows.Add(StrSlice(groups));
        }
        return rows.Count == 0 ? default : new GoSlice { Data = rows.ToArray(), Off = 0, Len = rows.Count, Cap = rows.Count };
    }
    public static GoString Re_ReplaceAllString(object r, GoString s, GoString repl)
    {
        var gx = (GoRegexp)r; string template = repl.ToDotNetString();
        return GoString.FromDotNetString(ReplaceMatches(gx, s.ToDotNetString(), m => ExpandRepl(gx, m, template)));
    }
    // Go's replacement $-expansion: $name / ${name} / $N, $$ -> $. Group references use Go's
    // left-to-right numbering and named groups; an unknown/failed group expands to empty.
    private static string ExpandRepl(GoRegexp gx, Match m, string template)
    {
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
            if (name.Length == 0) continue; // lone '$' with no valid name: Go drops it
            Group? grp = null;
            if (int.TryParse(name, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var num))
            { if (num >= 0 && num < gx.Map.Length) grp = gx.G(m, num); }
            else { int gi = gx.GoIndexOfName(name); if (gi >= 0) grp = gx.G(m, gi); }
            if (grp != null && grp.Success) sb.Append(grp.Value);
        }
        return sb.ToString();
    }
    // ReplaceAllStringFunc(s, repl): replace each match with the result of calling repl on
    // the matched text (repl is a func(string) string -> a GoClosure).
    public static GoString Re_ReplaceAllStringFunc(object r, GoString s, GoClosure repl)
    {
        return GoString.FromDotNetString(ReplaceMatches((GoRegexp)r, s.ToDotNetString(),
            m => ((GoString)GoRuntime.InvokeArgs(repl, GoString.FromDotNetString(m.Value))!).ToDotNetString()));
    }
    // ReplaceAllLiteralString(s, repl): the replacement is used verbatim — no $-expansion.
    public static GoString Re_ReplaceAllLiteralString(object r, GoString s, GoString repl)
    {
        string lit = repl.ToDotNetString();
        return GoString.FromDotNetString(ReplaceMatches((GoRegexp)r, s.ToDotNetString(), m => lit));
    }
    // Go's (*Regexp).Split — NOT .NET Regex.Split (whose n-limit and empty-match handling
    // differ). With limit n the final element is the unsplit remainder, and an empty pattern
    // (e.g. `\s*`) does not produce spurious empty fields.
    public static GoSlice Re_Split(object r, GoString gs, long n)
    {
        if (n == 0) return default; // nil
        string s = gs.ToDotNetString();
        if (s.Length == 0) return StrSlice(new[] { "" }); // a non-empty pattern on "" -> [""]
        var matches = GoMatchSpans(Re(r), s, n);
        var outp = new System.Collections.Generic.List<string>();
        int beg = 0, end = 0;
        foreach (var (mstart, mlen) in matches)
        {
            if (n > 0 && outp.Count >= n - 1) break;
            end = mstart;
            if (mstart + mlen != 0) outp.Add(s.Substring(beg, end - beg)); // skip the leading empty match
            beg = mstart + mlen;
        }
        if (end != s.Length) outp.Add(s.Substring(beg));
        return StrSlice(outp.ToArray());
    }

    // Enumerate matches the way Go's regexp does: an empty match sitting exactly at the
    // previous match's end is skipped, and search advances one position past an empty match.
    private static System.Collections.Generic.List<(int start, int len)> GoMatchSpans(Regex re, string s, long n)
    {
        var outp = new System.Collections.Generic.List<(int, int)>();
        int pos = 0, prevEnd = -1;
        while (pos <= s.Length)
        {
            if (n >= 0 && outp.Count >= n) break;
            var m = re.Match(s, pos);
            if (!m.Success) break;
            bool accept = !(m.Length == 0 && m.Index == prevEnd);
            if (accept) outp.Add((m.Index, m.Length));
            prevEnd = m.Index + m.Length;
            pos = m.Length > 0 ? m.Index + m.Length : m.Index + 1;
        }
        return outp;
    }

    // Like GoMatchSpans but returns the Match objects (for submatch groups), following Go's
    // empty-match rule so FindAll*/ReplaceAll don't emit a spurious empty match adjacent to a
    // previous match's end. .NET's Matches/Replace use a different empty-match convention.
    private static System.Collections.Generic.List<Match> GoMatches(Regex re, string s, long n)
    {
        var outp = new System.Collections.Generic.List<Match>();
        int pos = 0, prevEnd = -1;
        while (pos <= s.Length)
        {
            if (n >= 0 && outp.Count >= n) break;
            var m = re.Match(s, pos);
            if (!m.Success) break;
            if (!(m.Length == 0 && m.Index == prevEnd)) outp.Add(m);
            prevEnd = m.Index + m.Length;
            pos = m.Length > 0 ? m.Index + m.Length : m.Index + 1;
        }
        return outp;
    }

    // Replace each Go-faithful match with repl(m) (used by ReplaceAll*; .NET Regex.Replace has
    // the wrong empty-match handling).
    private static string ReplaceMatches(GoRegexp gx, string s, System.Func<Match, string> repl)
    {
        var sb = new System.Text.StringBuilder();
        int last = 0;
        foreach (var m in GoMatches(gx.Re, s, -1))
        {
            sb.Append(s, last, m.Index - last).Append(repl(m));
            last = m.Index + m.Length;
        }
        sb.Append(s, last, s.Length - last);
        return sb.ToString();
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
    private static GoSlice SubmatchBytes(GoRegexp gx, Match m, string s) { var g = new System.Collections.Generic.List<GoSlice>(); for (int i = 0; i < gx.Count; i++) { var grp = gx.G(m, i); g.Add(grp.Success ? ByteSliceOf(grp.Value) : default); } return SlicesOf(g); }
    private static GoSlice SubmatchIndex(GoRegexp gx, Match m, string s) { var idx = new System.Collections.Generic.List<long>(); for (int i = 0; i < gx.Count; i++) { var grp = gx.G(m, i); if (grp.Success) { idx.Add(BOff(s, grp.Index)); idx.Add(BOff(s, grp.Index + grp.Length)); } else { idx.Add(-1); idx.Add(-1); } } return IntSlice(idx); }

    public static GoSlice Re_Find(object r, GoSlice b) { string s = Str(b); var m = Re(r).Match(s); return m.Success ? ByteSliceOf(m.Value) : default; }
    public static GoSlice Re_FindIndex(object r, GoSlice b) { string s = Str(b); var m = Re(r).Match(s); return m.Success ? MatchIndex(m, s) : default; }
    public static GoSlice Re_FindStringIndexAll(object r, GoString gs, long n) { string s = gs.ToDotNetString(); var outp = new System.Collections.Generic.List<GoSlice>(); foreach (Match m in GoMatches(Re(r), s, n)) { if (n >= 0 && outp.Count >= n) break; outp.Add(MatchIndex(m, s)); } return outp.Count == 0 ? default : SlicesOf(outp); }
    public static GoSlice Re_FindAll(object r, GoSlice b, long n) { string s = Str(b); var outp = new System.Collections.Generic.List<GoSlice>(); foreach (Match m in GoMatches(Re(r), s, n)) { if (n >= 0 && outp.Count >= n) break; outp.Add(ByteSliceOf(m.Value)); } return outp.Count == 0 ? default : SlicesOf(outp); }
    public static GoSlice Re_FindAllIndex(object r, GoSlice b, long n) { string s = Str(b); var outp = new System.Collections.Generic.List<GoSlice>(); foreach (Match m in GoMatches(Re(r), s, n)) { if (n >= 0 && outp.Count >= n) break; outp.Add(MatchIndex(m, s)); } return outp.Count == 0 ? default : SlicesOf(outp); }
    public static GoSlice Re_FindSubmatch(object r, GoSlice b) { var gx = (GoRegexp)r; string s = Str(b); var m = gx.Re.Match(s); return m.Success ? SubmatchBytes(gx, m, s) : default; }
    public static GoSlice Re_FindSubmatchIndex(object r, GoSlice b) { var gx = (GoRegexp)r; string s = Str(b); var m = gx.Re.Match(s); return m.Success ? SubmatchIndex(gx, m, s) : default; }
    public static GoSlice Re_FindAllSubmatch(object r, GoSlice b, long n) { var gx = (GoRegexp)r; string s = Str(b); var outp = new System.Collections.Generic.List<GoSlice>(); foreach (Match m in GoMatches(gx.Re, s, n)) { if (n >= 0 && outp.Count >= n) break; outp.Add(SubmatchBytes(gx, m, s)); } return outp.Count == 0 ? default : SlicesOf(outp); }
    public static GoSlice Re_FindAllSubmatchIndex(object r, GoSlice b, long n) { var gx = (GoRegexp)r; string s = Str(b); var outp = new System.Collections.Generic.List<GoSlice>(); foreach (Match m in GoMatches(gx.Re, s, n)) { if (n >= 0 && outp.Count >= n) break; outp.Add(SubmatchIndex(gx, m, s)); } return outp.Count == 0 ? default : SlicesOf(outp); }

    public static GoSlice Re_ReplaceAll(object r, GoSlice src, GoSlice repl) => ByteSliceOf(Re_ReplaceAllString(r, GoString.FromBytes(Bytes(src)), GoString.FromBytes(Bytes(repl))).ToDotNetString());
    public static GoSlice Re_ReplaceAllLiteral(object r, GoSlice src, GoSlice repl) => ByteSliceOf(Re_ReplaceAllLiteralString(r, GoString.FromBytes(Bytes(src)), GoString.FromBytes(Bytes(repl))).ToDotNetString());
    public static GoSlice Re_ReplaceAllFunc(object r, GoSlice src, GoClosure f)
        => ByteSliceOf(Re(r).Replace(Str(src), (Match m) => GoString.FromBytes(Bytes((GoSlice)GoRuntime.InvokeArgs(f, ByteSliceOf(m.Value))!)).ToDotNetString()));

    public static long Re_SubexpIndex(object r, GoString name) => ((GoRegexp)r).GoIndexOfName(name.ToDotNetString());
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
            if (name.Length == 0) continue;
            int gi = int.TryParse(name, out var num) ? num : ((GoRegexp)r).GoIndexOfName(name);
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
