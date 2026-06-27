namespace GoCLR.Stdlib;

using System;
using GoCLR.Runtime;

/// <summary>Shim for Go's <c>strings</c> package. Structural operations use .NET
/// strings; byte-offset operations (Index/Count) operate on UTF-8 bytes to match
/// Go's byte semantics.</summary>
public static class Strings
{
    private static GoSlice Slice(string[] parts)
    {
        var data = new object?[parts.Length];
        for (int i = 0; i < parts.Length; i++) data[i] = GoString.FromDotNetString(parts[i]);
        return new GoSlice { Data = data, Off = 0, Len = parts.Length, Cap = parts.Length };
    }

    private static long IndexBytes(byte[] s, byte[] sub)
    {
        if (sub.Length == 0) return 0;
        for (int i = 0; i + sub.Length <= s.Length; i++)
        {
            bool m = true;
            for (int j = 0; j < sub.Length; j++) if (s[i + j] != sub[j]) { m = false; break; }
            if (m) return i;
        }
        return -1;
    }

    // Go applies unicode.ToUpper/ToLower per rune (simple, locale-independent mapping):
    // ToLower("İ") is "i" and ToUpper("ß") stays "ß". .NET's string-level
    // ToUpperInvariant/ToLowerInvariant differ for a few chars (İ stays İ), so map per rune.
    public static GoString ToUpper(GoString s) => MapRunes(s, GoUpper);
    public static GoString ToLower(GoString s) => MapRunes(s, GoLower);

    // strings.ToUpper/Lower/TitleSpecial(c unicode.SpecialCase, s string): locale-aware casing
    // (Turkish/Azeri). unicode is compiled, so the SpecialCase ([]CaseRange) arrives as a real
    // slice of lowered CaseRange structs; apply Go's unicode.to() per rune (case index
    // 0=Upper, 1=Lower, 2=Title), falling back to the default mapping when no range matches.
    public static GoString ToUpperSpecial(GoSlice special, GoString s) => MapSpecial(special, 0, s, GoUpper);
    public static GoString ToLowerSpecial(GoSlice special, GoString s) => MapSpecial(special, 1, s, GoLower);
    public static GoString ToTitleSpecial(GoSlice special, GoString s) => MapSpecial(special, 2, s, GoTitle);

    private static GoString MapSpecial(GoSlice special, int caseIdx, GoString s, System.Func<int, int> fallback)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var rune in s.ToDotNetString().EnumerateRunes())
        {
            int r = rune.Value;
            var (m, found) = SpecialTo(special, caseIdx, r);
            if (!found) m = fallback(r); // Go: fall back to default mapping only when no range matched
            sb.Append(System.Char.ConvertFromUtf32(m));
        }
        return GoString.FromDotNetString(sb.ToString());
    }

    // unicode.to(_case, r, caseRange): find r's CaseRange and apply its delta. Reflects the
    // lowered CaseRange struct (fields Lo, Hi uint32 and Delta [3]rune).
    private static (int, bool) SpecialTo(GoSlice special, int caseIdx, int r)
    {
        if (special.Data == null) return (r, false);
        const int MaxRune = 0x10FFFF;
        for (int i = 0; i < special.Len; i++)
        {
            var cr = special.Data[special.Off + i];
            if (cr == null) continue;
            var t = cr.GetType();
            long lo = ReadField(t, cr, "Lo"), hi = ReadField(t, cr, "Hi");
            if (lo <= r && r <= hi)
            {
                int delta = ReadDelta(t, cr, caseIdx);
                if (delta > MaxRune)
                    // Upper-Lower sequence: even offsets are upper, odd are lower; the low bit of
                    // _case selects (Upper/Title even, Lower odd).
                    return ((int)(lo + (((r - lo) & ~1) | (caseIdx & 1))), true);
                return (r + delta, true);
            }
        }
        return (r, false);
    }

    private static long ReadField(System.Type t, object cr, string name)
    {
        var f = t.GetField(name);
        return f != null ? System.Convert.ToInt64(f.GetValue(cr) ?? 0L) : 0;
    }
    private static int ReadDelta(System.Type t, object cr, int idx)
    {
        var arr = t.GetField("Delta")?.GetValue(cr);
        // goclr represents a fixed [N]T array as a GoSlice over its backing object[].
        if (arr is GoSlice gs && gs.Data != null) return (int)System.Convert.ToInt64(gs.Data[gs.Off + idx] ?? 0L);
        if (arr is object?[] oa) return (int)System.Convert.ToInt64(oa[idx] ?? 0L);
        if (arr is System.Array sa) return (int)System.Convert.ToInt64(sa.GetValue(idx) ?? 0L);
        return 0;
    }

    private static GoString MapRunes(GoString s, System.Func<int, int> f)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var rune in s.ToDotNetString().EnumerateRunes()) sb.Append(System.Char.ConvertFromUtf32(f(rune.Value)));
        return GoString.FromDotNetString(sb.ToString());
    }

    // .NET's invariant case mapping lacks a few entries that Go's simple mapping has
    // (the Turkish dotted/dotless I, long s, Kelvin/Angstrom signs); override those.
    private static int GoLower(int r) => r switch { 0x130 => 0x69, 0x212A => 0x6B, 0x212B => 0xE5, _ => Unicode.ToLower(r) };
    private static int GoUpper(int r) => r switch { 0x131 => 0x49, 0x17F => 0x53, _ => Unicode.ToUpper(r) };
    // Title case differs from upper for the Dž/Lj/Nj/Dz digraphs (their middle, titlecase
    // form); everything else titlecases like uppercase.
    private static int GoTitle(int r) => r switch
    {
        0x1C4 or 0x1C5 or 0x1C6 => 0x1C5,
        0x1C7 or 0x1C8 or 0x1C9 => 0x1C8,
        0x1CA or 0x1CB or 0x1CC => 0x1CB,
        0x1F1 or 0x1F2 or 0x1F3 => 0x1F2,
        _ => GoUpper(r),
    };
    // strings.Title: titlecase the first rune of each word, where a word starts after a
    // separator. NOT .NET ToTitleCase (which treats '_' as a separator and lowercases the
    // tail of all-caps words). Go's isSeparator: ASCII letters/digits/'_' are never
    // separators; otherwise non-letter/non-digit runes that are spaces are separators.
    private static bool IsSeparator(int r)
    {
        if (r <= 0x7F)
        {
            if (r >= '0' && r <= '9') return false;
            if (r >= 'a' && r <= 'z') return false;
            if (r >= 'A' && r <= 'Z') return false;
            if (r == '_') return false;
            return true;
        }
        if (Unicode.IsLetter(r) || Unicode.IsDigit(r)) return false;
        return Unicode.IsSpace(r);
    }
    public static GoString Title(GoString s)
    {
        var sb = new System.Text.StringBuilder();
        int prev = ' '; // a separator, so the first rune titlecases
        foreach (var rune in s.ToDotNetString().EnumerateRunes())
        {
            int r = rune.Value;
            sb.Append(System.Char.ConvertFromUtf32(IsSeparator(prev) ? GoTitle(r) : r));
            prev = r;
        }
        return GoString.FromDotNetString(sb.ToString());
    }

    public static bool Contains(GoString s, GoString sub) => IndexBytes(s.Bytes, sub.Bytes) >= 0;
    public static bool HasPrefix(GoString s, GoString p) => s.ToDotNetString().StartsWith(p.ToDotNetString(), StringComparison.Ordinal);
    public static bool HasSuffix(GoString s, GoString p) => s.ToDotNetString().EndsWith(p.ToDotNetString(), StringComparison.Ordinal);
    public static bool EqualFold(GoString a, GoString b) => string.Equals(a.ToDotNetString(), b.ToDotNetString(), StringComparison.OrdinalIgnoreCase);

    public static long Index(GoString s, GoString sub) => IndexBytes(s.Bytes, sub.Bytes);
    // strings.Compare: lexicographic byte comparison, returning -1/0/+1.
    public static long Compare(GoString a, GoString b)
    {
        byte[] x = a.Bytes, y = b.Bytes;
        int n = System.Math.Min(x.Length, y.Length);
        for (int i = 0; i < n; i++) if (x[i] != y[i]) return x[i] < y[i] ? -1 : 1;
        return x.Length == y.Length ? 0 : (x.Length < y.Length ? -1 : 1);
    }
    public static long LastIndex(GoString s, GoString sub)
    {
        byte[] b = s.Bytes, sb = sub.Bytes;
        if (sb.Length == 0) return b.Length;
        for (int i = b.Length - sb.Length; i >= 0; i--)
        {
            bool m = true;
            for (int j = 0; j < sb.Length; j++) if (b[i + j] != sb[j]) { m = false; break; }
            if (m) return i;
        }
        return -1;
    }
    public static long IndexByte(GoString s, int c)
    {
        byte[] b = s.Bytes;
        for (int i = 0; i < b.Length; i++) if (b[i] == (byte)c) return i;
        return -1;
    }
    public static long Count(GoString s, GoString sub)
    {
        string str = s.ToDotNetString(), sb = sub.ToDotNetString();
        if (sb.Length == 0) return str.Length + 1;
        long n = 0; int idx = 0;
        while ((idx = str.IndexOf(sb, idx, StringComparison.Ordinal)) >= 0) { n++; idx += sb.Length; }
        return n;
    }

    public static GoString Repeat(GoString s, long count) =>
        GoString.FromDotNetString(count <= 0 ? "" : string.Concat(System.Linq.Enumerable.Repeat(s.ToDotNetString(), (int)count)));
    public static GoString Replace(GoString s, GoString old, GoString neu, long n)
    {
        string str = s.ToDotNetString(), o = old.ToDotNetString(), nw = neu.ToDotNetString();
        // Go: a no-op when old == new or n == 0; an EMPTY old inserts new before each rune and
        // at the end (Count("abc","") == runes+1), so .NET String.Replace (which rejects an
        // empty old) can't be used.
        if (o == nw || n == 0) return s;
        int m = o.Length == 0 ? RuneCount(str) + 1 : CountSub(str, o);
        if (m == 0) return s;
        if (n < 0 || m < n) n = m;
        var sb = new System.Text.StringBuilder();
        int start = 0;
        for (long i = 0; i < n; i++)
        {
            int j;
            if (o.Length == 0)
            {
                j = start;
                if (i > 0) j += (start < str.Length && char.IsSurrogatePair(str, start)) ? 2 : 1; // one rune
            }
            else j = str.IndexOf(o, start, StringComparison.Ordinal);
            sb.Append(str, start, j - start).Append(nw);
            start = j + o.Length;
        }
        sb.Append(str, start, str.Length - start);
        return GoString.FromDotNetString(sb.ToString());
    }
    public static GoString ReplaceAll(GoString s, GoString old, GoString neu) => Replace(s, old, neu, -1);

    // non-overlapping occurrences of a non-empty substring; rune count for the rest.
    private static int CountSub(string s, string sub)
    {
        int c = 0, i = 0;
        while ((i = s.IndexOf(sub, i, StringComparison.Ordinal)) >= 0) { c++; i += sub.Length; }
        return c;
    }
    private static int RuneCount(string s)
    {
        int c = 0;
        for (int i = 0; i < s.Length; i += char.IsSurrogatePair(s, i) ? 2 : 1) c++;
        return c;
    }

    public static GoString TrimSpace(GoString s) => GoString.FromDotNetString(s.ToDotNetString().Trim());
    // An empty cutset trims nothing in Go; .NET's Trim(emptyArray) would default to trimming
    // whitespace, so guard against it.
    public static GoString Trim(GoString s, GoString cut) => cut.Len == 0 ? s : GoString.FromDotNetString(s.ToDotNetString().Trim(cut.ToDotNetString().ToCharArray()));
    public static GoString TrimLeft(GoString s, GoString cut) => cut.Len == 0 ? s : GoString.FromDotNetString(s.ToDotNetString().TrimStart(cut.ToDotNetString().ToCharArray()));
    public static GoString TrimRight(GoString s, GoString cut) => cut.Len == 0 ? s : GoString.FromDotNetString(s.ToDotNetString().TrimEnd(cut.ToDotNetString().ToCharArray()));
    public static GoString TrimPrefix(GoString s, GoString p)
    {
        string str = s.ToDotNetString(), pr = p.ToDotNetString();
        return GoString.FromDotNetString(str.StartsWith(pr, StringComparison.Ordinal) ? str.Substring(pr.Length) : str);
    }
    public static GoString TrimSuffix(GoString s, GoString p)
    {
        string str = s.ToDotNetString(), pr = p.ToDotNetString();
        return GoString.FromDotNetString(pr.Length > 0 && str.EndsWith(pr, StringComparison.Ordinal) ? str.Substring(0, str.Length - pr.Length) : str);
    }

    public static GoSlice Split(GoString s, GoString sep)
    {
        string str = s.ToDotNetString(), sp = sep.ToDotNetString();
        if (sp.Length == 0)
        {
            // Go splits into runes; approximate by chars.
            var chars = new string[str.Length];
            for (int i = 0; i < str.Length; i++) chars[i] = str[i].ToString();
            return Slice(chars);
        }
        return Slice(str.Split(new[] { sp }, StringSplitOptions.None));
    }
    public static GoSlice SplitN(GoString s, GoString sep, long n)
    {
        if (n == 0) return new GoSlice { Data = Array.Empty<object?>(), Len = 0, Cap = 0 };
        string str = s.ToDotNetString(), sp = sep.ToDotNetString();
        if (n < 0 || sp.Length == 0) return Split(s, sep);
        return Slice(str.Split(new[] { sp }, (int)n, StringSplitOptions.None));
    }
    public static GoSlice Fields(GoString s) =>
        Slice(s.ToDotNetString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static GoString Join(GoSlice elems, GoString sep)
    {
        var sb = new System.Text.StringBuilder();
        string sp = sep.ToDotNetString();
        for (int i = 0; i < elems.Len; i++)
        {
            if (i > 0) sb.Append(sp);
            sb.Append(((GoString)elems.Data[elems.Off + i]!).ToDotNetString());
        }
        return GoString.FromDotNetString(sb.ToString());
    }

    // strings.Cut(s, sep) -> (before, after, found).
    public static object?[] Cut(GoString s, GoString sep)
    {
        string str = s.ToDotNetString(), sp = sep.ToDotNetString();
        int idx = str.IndexOf(sp, StringComparison.Ordinal);
        if (idx < 0) return new object?[] { s, GoString.FromDotNetString(""), false };
        return new object?[] { GoString.FromDotNetString(str.Substring(0, idx)), GoString.FromDotNetString(str.Substring(idx + sp.Length)), true };
    }

    public static long IndexRune(GoString s, int r) => IndexBytes(s.Bytes, GoString.FromDotNetString(char.ConvertFromUtf32(r)).Bytes);
    public static bool ContainsRune(GoString s, int r) => IndexRune(s, r) >= 0;
    public static bool ContainsAny(GoString s, GoString chars) => IndexAny(s, chars) >= 0;
    public static long IndexAny(GoString s, GoString chars)
    {
        string str = s.ToDotNetString(), set = chars.ToDotNetString();
        for (int i = 0; i < str.Length;)
        {
            int cp = char.ConvertToUtf32(str, i);
            string ch = char.ConvertFromUtf32(cp);
            if (set.Contains(ch, StringComparison.Ordinal)) return System.Text.Encoding.UTF8.GetByteCount(str.Substring(0, i));
            i += ch.Length;
        }
        return -1;
    }
    public static long LastIndexByte(GoString s, int c)
    {
        byte[] b = s.Bytes;
        for (int i = b.Length - 1; i >= 0; i--) if (b[i] == (byte)c) return i;
        return -1;
    }

    public static GoString ToTitle(GoString s) => MapRunes(s, GoTitle);

    public static GoSlice SplitAfter(GoString s, GoString sep)
    {
        string str = s.ToDotNetString(), sp = sep.ToDotNetString();
        var parts = new System.Collections.Generic.List<string>();
        if (sp.Length == 0) { foreach (var r in str.EnumerateRunes()) parts.Add(r.ToString()); return Slice(parts.ToArray()); }
        int start = 0, idx;
        while ((idx = str.IndexOf(sp, start, StringComparison.Ordinal)) >= 0)
        { parts.Add(str.Substring(start, idx + sp.Length - start)); start = idx + sp.Length; }
        parts.Add(str.Substring(start));
        return Slice(parts.ToArray());
    }

    public static GoSlice SplitAfterN(GoString s, GoString sep, long n)
    {
        if (n == 0) return new GoSlice { Data = Array.Empty<object?>(), Len = 0, Cap = 0 };
        if (n < 0) return SplitAfter(s, sep);
        string str = s.ToDotNetString(), sp = sep.ToDotNetString();
        var parts = new System.Collections.Generic.List<string>();
        int start = 0, idx;
        while (parts.Count < n - 1 && sp.Length > 0 && (idx = str.IndexOf(sp, start, StringComparison.Ordinal)) >= 0)
        { parts.Add(str.Substring(start, idx + sp.Length - start)); start = idx + sp.Length; }
        parts.Add(str.Substring(start));
        return Slice(parts.ToArray());
    }

    private static bool RunePred(GoClosure f, int r) => (bool)GoRuntime.InvokeArgs(f, r)!;

    public static GoString TrimFunc(GoString s, GoClosure f) => TrimRightFunc(TrimLeftFunc(s, f), f);
    public static GoString TrimLeftFunc(GoString s, GoClosure f)
    {
        string str = s.ToDotNetString(); int i = 0;
        while (i < str.Length) { int cp = char.ConvertToUtf32(str, i); if (!RunePred(f, cp)) break; i += char.ConvertFromUtf32(cp).Length; }
        return GoString.FromDotNetString(str.Substring(i));
    }
    public static GoString TrimRightFunc(GoString s, GoClosure f)
    {
        var runes = new System.Collections.Generic.List<int>();
        foreach (var r in s.ToDotNetString().EnumerateRunes()) runes.Add(r.Value);
        int end = runes.Count;
        while (end > 0 && RunePred(f, runes[end - 1])) end--;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < end; i++) sb.Append(char.ConvertFromUtf32(runes[i]));
        return GoString.FromDotNetString(sb.ToString());
    }
    public static long IndexFunc(GoString s, GoClosure f)
    {
        string str = s.ToDotNetString();
        for (int i = 0; i < str.Length;)
        {
            int cp = char.ConvertToUtf32(str, i);
            if (RunePred(f, cp)) return System.Text.Encoding.UTF8.GetByteCount(str.Substring(0, i));
            i += char.ConvertFromUtf32(cp).Length;
        }
        return -1;
    }
    public static GoSlice FieldsFunc(GoString s, GoClosure f)
    {
        var parts = new System.Collections.Generic.List<string>();
        var cur = new System.Text.StringBuilder();
        foreach (var r in s.ToDotNetString().EnumerateRunes())
        {
            if (RunePred(f, r.Value)) { if (cur.Length > 0) { parts.Add(cur.ToString()); cur.Clear(); } }
            else cur.Append(r.ToString());
        }
        if (cur.Length > 0) parts.Add(cur.ToString());
        return Slice(parts.ToArray());
    }

    // strings.Map(mapping func(rune) rune, s).
    public static GoString Map(GoClosure mapping, GoString s)
    {
        var c = mapping;
        var sb = new System.Text.StringBuilder();
        foreach (var r in s.ToDotNetString().EnumerateRunes())
        {
            long nr = System.Convert.ToInt64(GoRuntime.InvokeArgs(c, r.Value));
            if (nr >= 0) sb.Append(char.ConvertFromUtf32((int)nr));
        }
        return GoString.FromDotNetString(sb.ToString());
    }

    // strings.Clone returns a fresh copy of s's bytes (a distinct backing array).
    public static GoString Clone(GoString s)
    {
        if (s.Bytes.Length == 0) return GoString.FromDotNetString("");
        var b = new byte[s.Bytes.Length];
        System.Array.Copy(s.Bytes, b, b.Length);
        return GoString.FromBytes(b);
    }
    public static bool ContainsFunc(GoString s, GoClosure f) => IndexFunc(s, f) >= 0;
    // strings.CutPrefix(s, prefix) -> (after, found).
    public static object?[] CutPrefix(GoString s, GoString prefix)
    {
        string str = s.ToDotNetString(), p = prefix.ToDotNetString();
        return str.StartsWith(p, StringComparison.Ordinal)
            ? new object?[] { GoString.FromDotNetString(str.Substring(p.Length)), true }
            : new object?[] { s, false };
    }
    // strings.CutSuffix(s, suffix) -> (before, found).
    public static object?[] CutSuffix(GoString s, GoString suffix)
    {
        string str = s.ToDotNetString(), p = suffix.ToDotNetString();
        return p.Length > 0 && str.EndsWith(p, StringComparison.Ordinal)
            ? new object?[] { GoString.FromDotNetString(str.Substring(0, str.Length - p.Length)), true }
            : new object?[] { s, false };
    }
    public static long LastIndexAny(GoString s, GoString chars)
    {
        string str = s.ToDotNetString(), set = chars.ToDotNetString();
        long last = -1;
        for (int i = 0; i < str.Length;)
        {
            int cp = char.ConvertToUtf32(str, i);
            string ch = char.ConvertFromUtf32(cp);
            if (set.Contains(ch, StringComparison.Ordinal)) last = System.Text.Encoding.UTF8.GetByteCount(str.Substring(0, i));
            i += ch.Length;
        }
        return last;
    }
    public static long LastIndexFunc(GoString s, GoClosure f)
    {
        string str = s.ToDotNetString();
        long last = -1;
        for (int i = 0; i < str.Length;)
        {
            int cp = char.ConvertToUtf32(str, i);
            if (RunePred(f, cp)) last = System.Text.Encoding.UTF8.GetByteCount(str.Substring(0, i));
            i += char.ConvertFromUtf32(cp).Length;
        }
        return last;
    }
    // strings.ToValidUTF8 replaces each run of invalid UTF-8 bytes with the replacement.
    public static GoString ToValidUTF8(GoString s, GoString replacement)
    {
        byte[] b = s.Bytes, repl = replacement.Bytes;
        var outb = new System.Collections.Generic.List<byte>(b.Length);
        int i = 0;
        bool prevInvalid = false;
        while (i < b.Length)
        {
            var status = System.Text.Rune.DecodeFromUtf8(new System.ReadOnlySpan<byte>(b, i, b.Length - i), out _, out int consumed);
            if (status == System.Buffers.OperationStatus.Done)
            {
                for (int k = 0; k < consumed; k++) outb.Add(b[i + k]);
                i += consumed;
                prevInvalid = false;
            }
            else
            {
                if (!prevInvalid) { outb.AddRange(repl); prevInvalid = true; }
                i++;
            }
        }
        return GoString.FromBytes(outb.ToArray());
    }

    // ---- iter.Seq[string] producers (Go 1.24): return a func(yield func(string) bool) ----
    private static GoClosure SeqOf(System.Collections.Generic.List<string> parts) =>
        NativeClosures.Make(args =>
        {
            if (args.Length == 0 || args[0] is not GoClosure yield) return null;
            foreach (var p in parts)
                if (GoRuntime.InvokeArgs(yield, GoString.FromDotNetString(p)) is not true) break;
            return null;
        });
    private static System.Collections.Generic.List<string> SliceStrings(GoSlice sl)
    {
        var list = new System.Collections.Generic.List<string>(sl.Len);
        for (int i = 0; i < sl.Len; i++) list.Add(((GoString)sl.Data![sl.Off + i]!).ToDotNetString());
        return list;
    }
    public static GoClosure SplitSeq(GoString s, GoString sep) => SeqOf(SliceStrings(Split(s, sep)));
    public static GoClosure SplitAfterSeq(GoString s, GoString sep) => SeqOf(SliceStrings(SplitAfter(s, sep)));
    public static GoClosure FieldsSeq(GoString s) => SeqOf(SliceStrings(Fields(s)));
    public static GoClosure FieldsFuncSeq(GoString s, GoClosure f) => SeqOf(SliceStrings(FieldsFunc(s, f)));
    // strings.Lines yields each line including its trailing "\n" (the last without one if absent).
    public static GoClosure Lines(GoString s)
    {
        string str = s.ToDotNetString();
        var parts = new System.Collections.Generic.List<string>();
        int start = 0;
        for (int i = 0; i < str.Length; i++)
            if (str[i] == '\n') { parts.Add(str.Substring(start, i - start + 1)); start = i + 1; }
        if (start < str.Length) parts.Add(str.Substring(start));
        return SeqOf(parts);
    }

    // strings.NewReplacer(oldnew ...string) *Replacer — pairs of old, new.
    public static object NewReplacer(GoSlice pairs)
    {
        int n = pairs.Len / 2;
        var rep = new GoReplacer { Old = new string[n], New = new string[n], OldB = new byte[n][], NewB = new byte[n][] };
        for (int i = 0; i < n; i++)
        {
            var oldS = (GoString)pairs.Data![pairs.Off + 2 * i]!;
            var newS = (GoString)pairs.Data![pairs.Off + 2 * i + 1]!;
            rep.Old[i] = oldS.ToDotNetString();
            rep.New[i] = newS.ToDotNetString();
            rep.OldB[i] = oldS.ToBytes();
            rep.NewB[i] = newS.ToBytes();
        }
        return rep;
    }

    // (*Replacer).Replace — single non-overlapping pass. At each position the old strings
    // are tried in argument order and the first (highest-priority) match wins, exactly like
    // Go's genericReplacer trie (earlier-added keys have higher priority). An empty old
    // string matches at every position with zero width; Go avoids matching it twice in a row
    // via prevMatchEmpty, so NewReplacer("","X","a","b").Replace("aa") == "XbXbX".
    public static GoString Replacer_Replace(object r, GoString s)
    {
        var rep = (GoReplacer)r;
        byte[] str = s.Bytes; // operate on UTF-8 bytes: Go advances/inserts per byte, so an
                              // empty key inserts its replacement between the bytes of a rune.
        var outp = new System.Collections.Generic.List<byte>(str.Length + 8);
        int i = 0, last = 0;
        bool prevMatchEmpty = false;
        while (i <= str.Length)
        {
            int match = -1, keylen = 0;
            for (int k = 0; k < rep.OldB.Length; k++)
            {
                int len = rep.OldB[k].Length;
                if (len == 0)
                {
                    if (prevMatchEmpty) continue; // don't match the empty key twice in a row
                    match = k; keylen = 0; break;
                }
                if (i + len <= str.Length && MatchBytesAt(str, i, rep.OldB[k]))
                {
                    match = k; keylen = len; break;
                }
            }
            prevMatchEmpty = match >= 0 && keylen == 0;
            if (match >= 0)
            {
                for (int b = last; b < i; b++) outp.Add(str[b]);
                outp.AddRange(rep.NewB[match]);
                i += keylen;
                last = i;
                continue;
            }
            i++;
        }
        for (int b = last; b < str.Length; b++) outp.Add(str[b]);
        return GoString.FromBytesOwned(outp.ToArray());
    }
    private static bool MatchBytesAt(byte[] s, int i, byte[] pat)
    {
        for (int j = 0; j < pat.Length; j++) if (s[i + j] != pat[j]) return false;
        return true;
    }

    // (*Replacer).WriteString(w io.Writer, s string) (n int, err error): replace, then
    // write the result to w through the shared writer dispatch.
    public static object?[] Replacer_WriteString(object r, object? w, GoString s)
    {
        var outp = Replacer_Replace(r, s);
        long n = Fmt.WriteTo(w, outp.ToDotNetString());
        return new object?[] { n, null };
    }
}

/// <summary>A strings.Replacer: ordered old→new string pairs.</summary>
public sealed class GoReplacer
{
    public string[] Old = System.Array.Empty<string>();
    public string[] New = System.Array.Empty<string>();
    // UTF-8 byte forms of the pairs, so Replace matches/advances per byte like Go.
    public byte[][] OldB = System.Array.Empty<byte[]>();
    public byte[][] NewB = System.Array.Empty<byte[]>();
}
