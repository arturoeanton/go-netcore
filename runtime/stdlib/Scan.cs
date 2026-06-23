namespace GoCLR.Stdlib;

using System.Text;
using GoCLR.Runtime;

/// <summary>Shim for fmt's string scanners (Sscan / Sscanf / Sscanln): a pragmatic
/// reverse of Sprintf covering the common verbs (%d %s %f/%g/%e %t %x %c %q %v) plus
/// literal/whitespace matching, assigning each scanned value through its argument
/// pointer. The pointee's runtime kind picks the stored type (int32 vs int64, float32
/// vs float64, …), so a scan writes back the exact Go type.</summary>
public static class Scan
{
    // fmt.Sscanf(str, format, ...ptrs) (n int, err error)
    public static object?[] Sscanf(GoString str, GoString format, GoSlice args)
    {
        string s = str.ToDotNetString();
        string f = format.ToDotNetString();
        int si = 0, fi = 0, ai = 0, count = 0;
        while (fi < f.Length)
        {
            char fc = f[fi];
            if (char.IsWhiteSpace(fc))
            {
                fi++;
                while (si < s.Length && char.IsWhiteSpace(s[si])) si++;
                continue;
            }
            if (fc == '%' && fi + 1 < f.Length)
            {
                fi++;
                while (fi < f.Length && char.IsDigit(f[fi])) fi++; // skip an optional width
                char verb = f[fi];
                fi++;
                if (verb == '%')
                {
                    if (si < s.Length && s[si] == '%') { si++; continue; }
                    return Fail(count, "input does not match format");
                }
                if (ai >= args.Len) return Fail(count, "too few operands for format");
                var ptr = args.Data![args.Off + ai] as GoPtr;
                ai++;
                // An unsupported verb (e.g. the scanset %[...], which Go's fmt does not
                // implement either) is reported exactly as Go does: bad verb '%X' for <type>.
                if ("dsfgetxcqv".IndexOf(verb) < 0)
                    return Fail(count, "bad verb '%" + verb + "' for " + Fmt.TypeName(SafeGet(ptr)));
                if (verb != 'c') while (si < s.Length && char.IsWhiteSpace(s[si])) si++;
                if (!ScanOne(verb, s, ref si, ptr)) return Fail(count, "expected " + VerbDesc(verb));
                count++;
                continue;
            }
            if (si < s.Length && s[si] == fc) { si++; fi++; continue; }
            return Fail(count, "input does not match format");
        }
        return new object?[] { (long)count, null };
    }

    // fmt.Sscan(str, ...ptrs): space-separated values; the pointee type drives parsing.
    public static object?[] Sscan(GoString str, GoSlice args) => ScanSpace(str.ToDotNetString(), args, false);
    // fmt.Sscanln(str, ...ptrs): like Sscan but stops at a newline.
    public static object?[] Sscanln(GoString str, GoSlice args) => ScanSpace(str.ToDotNetString(), args, true);

    static object?[] ScanSpace(string s, GoSlice args, bool line)
    {
        int si = 0, count = 0;
        for (int ai = 0; ai < args.Len; ai++)
        {
            while (si < s.Length && char.IsWhiteSpace(s[si]) && !(line && s[si] == '\n')) si++;
            if (si >= s.Length || (line && s[si] == '\n')) return Fail(count, "unexpected EOF");
            var ptr = args.Data![args.Off + ai] as GoPtr;
            if (!ScanOne('v', s, ref si, ptr)) return Fail(count, "scan error");
            count++;
        }
        return new object?[] { (long)count, null };
    }

    // Scans one value of the given verb from s at si (advancing si), storing it through ptr.
    static bool ScanOne(char verb, string s, ref int si, GoPtr? ptr)
    {
        if (ptr == null) return false;
        long kind = GoPtrs.PointeeKind(ptr);
        if (verb == 'v') verb = kind switch { 1 => 's', 5 => 't', 6 => 'f', 9 => 'f', _ => 'd' };
        if (verb == 'g' || verb == 'e') verb = 'f';
        switch (verb)
        {
            case 's':
            case 'q':
            {
                if (verb == 'q' && si < s.Length && s[si] == '"')
                {
                    int j = si + 1;
                    var sb = new StringBuilder();
                    while (j < s.Length && s[j] != '"') { if (s[j] == '\\' && j + 1 < s.Length) j++; sb.Append(s[j]); j++; }
                    if (j >= s.Length) return false;
                    si = j + 1;
                    GoPtrs.Set(ptr, GoString.FromDotNetString(sb.ToString()));
                    return true;
                }
                int start = si;
                while (si < s.Length && !char.IsWhiteSpace(s[si])) si++;
                if (si == start) return false;
                GoPtrs.Set(ptr, GoString.FromDotNetString(s.Substring(start, si - start)));
                return true;
            }
            case 'd':
            {
                int start = si;
                if (si < s.Length && (s[si] == '+' || s[si] == '-')) si++;
                int digits = si;
                while (si < s.Length && char.IsDigit(s[si])) si++;
                if (si == digits) { si = start; return false; }
                if (!long.TryParse(s.Substring(start, si - start), out long n)) { si = start; return false; }
                StoreInt(ptr, kind, n);
                return true;
            }
            case 'x':
            {
                int start = si;
                while (si < s.Length && IsHex(s[si])) si++;
                if (si == start) return false;
                long n = System.Convert.ToInt64(s.Substring(start, si - start), 16);
                StoreInt(ptr, kind, n);
                return true;
            }
            case 'f':
            {
                int start = si;
                if (si < s.Length && (s[si] == '+' || s[si] == '-')) si++;
                int begin = si;
                while (si < s.Length && (char.IsDigit(s[si]) || s[si] == '.' || s[si] == 'e' || s[si] == 'E' ||
                                         ((s[si] == '+' || s[si] == '-') && si > begin && (s[si - 1] == 'e' || s[si - 1] == 'E')))) si++;
                if (si == begin) { si = start; return false; }
                if (!double.TryParse(s.Substring(start, si - start), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double d)) { si = start; return false; }
                if (kind == 9) GoPtrs.Set(ptr, (float)d); else GoPtrs.Set(ptr, d);
                return true;
            }
            case 't':
            {
                if (Match(s, si, "true")) { si += 4; GoPtrs.Set(ptr, true); return true; }
                if (Match(s, si, "false")) { si += 5; GoPtrs.Set(ptr, false); return true; }
                return false;
            }
            case 'c':
            {
                if (si >= s.Length) return false;
                StoreInt(ptr, kind, s[si]); // a rune target is int32; store per the pointee kind
                si++;
                return true;
            }
            default:
                return false;
        }
    }

    static void StoreInt(GoPtr ptr, long kind, long n)
    {
        switch (kind)
        {
            case 4: GoPtrs.Set(ptr, (int)n); break;
            case 7: GoPtrs.Set(ptr, (ulong)n); break;
            case 8: GoPtrs.Set(ptr, (uint)n); break;
            default: GoPtrs.Set(ptr, n); break;
        }
    }

    static object? SafeGet(GoPtr? p) { try { return p == null ? null : GoPtrs.Get(p); } catch { return null; } }
    static bool IsHex(char c) => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
    static bool Match(string s, int i, string word) => i + word.Length <= s.Length && s.Substring(i, word.Length) == word;
    static string VerbDesc(char v) => v switch { 'd' or 'x' => "integer", 'f' or 'g' or 'e' => "float", 't' => "boolean", _ => "input" };
    static object?[] Fail(int count, string msg) => new object?[] { (long)count, new GoError(GoString.FromDotNetString(msg)) };
}
