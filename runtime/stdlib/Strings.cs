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
}
