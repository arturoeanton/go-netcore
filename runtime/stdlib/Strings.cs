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

    public static GoString ToUpper(GoString s) => GoString.FromDotNetString(s.ToDotNetString().ToUpperInvariant());
    public static GoString ToLower(GoString s) => GoString.FromDotNetString(s.ToDotNetString().ToLowerInvariant());
    public static GoString Title(GoString s) => GoString.FromDotNetString(System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToDotNetString()));

    public static bool Contains(GoString s, GoString sub) => IndexBytes(s.Bytes, sub.Bytes) >= 0;
    public static bool HasPrefix(GoString s, GoString p) => s.ToDotNetString().StartsWith(p.ToDotNetString(), StringComparison.Ordinal);
    public static bool HasSuffix(GoString s, GoString p) => s.ToDotNetString().EndsWith(p.ToDotNetString(), StringComparison.Ordinal);
    public static bool EqualFold(GoString a, GoString b) => string.Equals(a.ToDotNetString(), b.ToDotNetString(), StringComparison.OrdinalIgnoreCase);

    public static long Index(GoString s, GoString sub) => IndexBytes(s.Bytes, sub.Bytes);
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
        if (n < 0) return GoString.FromDotNetString(str.Replace(o, nw));
        if (o.Length == 0) return GoString.FromDotNetString(str); // simplification
        var sb = new System.Text.StringBuilder();
        int i = 0; long done = 0;
        while (done < n)
        {
            int idx = str.IndexOf(o, i, StringComparison.Ordinal);
            if (idx < 0) break;
            sb.Append(str, i, idx - i).Append(nw);
            i = idx + o.Length; done++;
        }
        sb.Append(str, i, str.Length - i);
        return GoString.FromDotNetString(sb.ToString());
    }
    public static GoString ReplaceAll(GoString s, GoString old, GoString neu) =>
        GoString.FromDotNetString(s.ToDotNetString().Replace(old.ToDotNetString(), neu.ToDotNetString()));

    public static GoString TrimSpace(GoString s) => GoString.FromDotNetString(s.ToDotNetString().Trim());
    public static GoString Trim(GoString s, GoString cut) => GoString.FromDotNetString(s.ToDotNetString().Trim(cut.ToDotNetString().ToCharArray()));
    public static GoString TrimLeft(GoString s, GoString cut) => GoString.FromDotNetString(s.ToDotNetString().TrimStart(cut.ToDotNetString().ToCharArray()));
    public static GoString TrimRight(GoString s, GoString cut) => GoString.FromDotNetString(s.ToDotNetString().TrimEnd(cut.ToDotNetString().ToCharArray()));
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

    public static GoString ToTitle(GoString s) => GoString.FromDotNetString(s.ToDotNetString().ToUpperInvariant());

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

    // strings.NewReplacer(oldnew ...string) *Replacer — pairs of old, new.
    public static object NewReplacer(GoSlice pairs)
    {
        int n = pairs.Len / 2;
        var rep = new GoReplacer { Old = new string[n], New = new string[n] };
        for (int i = 0; i < n; i++)
        {
            rep.Old[i] = ((GoString)pairs.Data![pairs.Off + 2 * i]!).ToDotNetString();
            rep.New[i] = ((GoString)pairs.Data![pairs.Off + 2 * i + 1]!).ToDotNetString();
        }
        return rep;
    }

    // (*Replacer).Replace — single non-overlapping pass; at each position the old
    // strings are tried in argument order and the first match wins (Go semantics).
    public static GoString Replacer_Replace(object r, GoString s)
    {
        var rep = (GoReplacer)r;
        string str = s.ToDotNetString();
        var sb = new System.Text.StringBuilder();
        int i = 0;
        while (i < str.Length)
        {
            int match = -1;
            for (int k = 0; k < rep.Old.Length; k++)
            {
                int len = rep.Old[k].Length;
                if (len > 0 && i + len <= str.Length &&
                    string.CompareOrdinal(str, i, rep.Old[k], 0, len) == 0)
                {
                    match = k;
                    break;
                }
            }
            if (match >= 0) { sb.Append(rep.New[match]); i += rep.Old[match].Length; }
            else { sb.Append(str[i]); i++; }
        }
        return GoString.FromDotNetString(sb.ToString());
    }
}

/// <summary>A strings.Replacer: ordered old→new string pairs.</summary>
public sealed class GoReplacer
{
    public string[] Old = System.Array.Empty<string>();
    public string[] New = System.Array.Empty<string>();
}
