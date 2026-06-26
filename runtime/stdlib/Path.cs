namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>Shim for Go's <c>path</c> and (on a slash OS) <c>path/filepath</c>.
/// Operations are slash-based, matching Go on darwin/linux.</summary>
public static class Path
{
    private static string J(GoSlice elems)
    {
        var parts = new System.Collections.Generic.List<string>();
        for (int i = 0; i < elems.Len; i++)
        {
            string e = ((GoString)elems.Data![elems.Off + i]!).ToDotNetString();
            if (e.Length > 0) parts.Add(e);
        }
        return parts.Count == 0 ? "" : Clean(string.Join("/", parts));
    }
    public static GoString Join(GoSlice elems) => GoString.FromDotNetString(J(elems));

    // filepath.Walk(root, fn) error: goclr cannot invoke the Go walk callback from a
    // shim, and the only consumer (universal-translator's translation import/export)
    // is dead code for a service. This returns nil without walking; if a program
    // genuinely needs Walk, it must be lowered from a goclr-safe overlay.
    public static object? Walk(GoString root, GoClosure? fn) => null;

    public static GoString Base(GoString p)
    {
        string s = p.ToDotNetString();
        if (s.Length == 0) return GoString.FromDotNetString(".");
        s = s.TrimEnd('/');
        if (s.Length == 0) return GoString.FromDotNetString("/");
        int i = s.LastIndexOf('/');
        if (i >= 0) s = s.Substring(i + 1);
        return GoString.FromDotNetString(s.Length == 0 ? "/" : s);
    }

    public static GoString Dir(GoString p)
    {
        string s = p.ToDotNetString();
        int i = s.LastIndexOf('/');
        string dir = i < 0 ? "" : s.Substring(0, i + 1);
        return GoString.FromDotNetString(Clean(dir.Length == 0 ? "." : dir));
    }

    public static GoString Ext(GoString p)
    {
        string s = p.ToDotNetString();
        for (int i = s.Length - 1; i >= 0 && s[i] != '/'; i--)
            if (s[i] == '.') return GoString.FromDotNetString(s.Substring(i));
        return GoString.FromDotNetString("");
    }

    public static object?[] Split(GoString p)
    {
        string s = p.ToDotNetString();
        int i = s.LastIndexOf('/');
        return new object?[] { GoString.FromDotNetString(s.Substring(0, i + 1)), GoString.FromDotNetString(s.Substring(i + 1)) };
    }

    public static bool IsAbs(GoString p) { string s = p.ToDotNetString(); return s.Length > 0 && s[0] == '/'; }
    // filepath.Abs(path) (string, error): the cleaned absolute path (joined with cwd).
    public static object?[] Abs(GoString p)
    {
        try
        {
            string s = p.ToDotNetString();
            string abs = IsAbs(p) ? Clean(s) : Clean(System.IO.Directory.GetCurrentDirectory() + "/" + s);
            return new object?[] { GoString.FromDotNetString(abs), null };
        }
        catch (System.Exception e) { return new object?[] { GoString.FromDotNetString(""), new GoError(e.Message) }; }
    }

    // Go's path.Clean algorithm (lexical).
    public static GoString Clean(GoString p) => GoString.FromDotNetString(Clean(p.ToDotNetString()));

    // filepath.VolumeName(path): always "" on the unix-like targets.
    public static GoString VolumeName(GoString p) => GoString.FromDotNetString("");

    // filepath.HasPrefix(p, prefix) (deprecated): a plain string prefix test on unix.
    public static bool HasPrefix(GoString p, GoString prefix) =>
        p.ToDotNetString().StartsWith(prefix.ToDotNetString(), System.StringComparison.Ordinal);

    // filepath.SplitList(path): split on the OS list separator (':' on unix); "" -> [].
    public static GoSlice SplitList(GoString p)
    {
        string s = p.ToDotNetString();
        if (s.Length == 0) return new GoSlice { Data = System.Array.Empty<object?>(), Off = 0, Len = 0, Cap = 0 };
        var parts = s.Split(':');
        var d = new object?[parts.Length];
        for (int i = 0; i < parts.Length; i++) d[i] = GoString.FromDotNetString(parts[i]);
        return new GoSlice { Data = d, Off = 0, Len = d.Length, Cap = d.Length };
    }

    // filepath.IsLocal(path): reports whether path, lexically, stays within its directory
    // (not absolute, not escaping via "..", not empty). Ported from internal/filepathlite.
    public static bool IsLocal(GoString p)
    {
        string path = p.ToDotNetString();
        if (path.Length == 0 || path[0] == '/') return false;
        bool hasDots = false;
        for (string rest = path; rest.Length > 0;)
        {
            int slash = rest.IndexOf('/');
            string part = slash < 0 ? rest : rest.Substring(0, slash);
            rest = slash < 0 ? "" : rest.Substring(slash + 1);
            if (part == "." || part == "..") { hasDots = true; break; }
        }
        if (hasDots) path = Clean(path);
        if (path == ".." || path.StartsWith("../", System.StringComparison.Ordinal)) return false;
        return true;
    }

    // filepath.Rel(basepath, targpath) (string, error): a relative path from base to targ.
    // Unix-simplified port (VolumeName is always ""): pure lexical, no filesystem access.
    public static object?[] Rel(GoString basePathG, GoString targPathG)
    {
        string basePath = basePathG.ToDotNetString(), targPath = targPathG.ToDotNetString();
        string bas = Clean(basePath), targ = Clean(targPath);
        if (bas == targ) return new object?[] { GoString.FromDotNetString("."), null };
        if (bas == ".") bas = "";
        bool baseSlashed = bas.Length > 0 && bas[0] == '/';
        bool targSlashed = targ.Length > 0 && targ[0] == '/';
        if (baseSlashed != targSlashed)
            return new object?[] { GoString.FromDotNetString(""), new GoError(GoString.FromDotNetString("Rel: can't make " + targPath + " relative to " + basePath)) };
        int bl = bas.Length, tl = targ.Length, b0 = 0, bi = 0, t0 = 0, ti = 0;
        while (true)
        {
            while (bi < bl && bas[bi] != '/') bi++;
            while (ti < tl && targ[ti] != '/') ti++;
            if (Span(targ, t0, ti) != Span(bas, b0, bi)) break;
            if (bi < bl) bi++;
            if (ti < tl) ti++;
            b0 = bi; t0 = ti;
        }
        if (Span(bas, b0, bi) == "..")
            return new object?[] { GoString.FromDotNetString(""), new GoError(GoString.FromDotNetString("Rel: can't make " + targPath + " relative to " + basePath)) };
        if (b0 != bl)
        {
            int seps = 0;
            for (int i = b0; i < bl; i++) if (bas[i] == '/') seps++;
            var sb = new System.Text.StringBuilder("..");
            for (int i = 0; i < seps; i++) sb.Append("/..");
            if (t0 != tl) { sb.Append('/'); sb.Append(targ.Substring(t0)); }
            return new object?[] { GoString.FromDotNetString(Clean(sb.ToString())), null };
        }
        return new object?[] { GoString.FromDotNetString(targ.Substring(t0)), null };
    }
    private static string Span(string s, int a, int b) => s.Substring(a, b - a);

    private static string Clean(string path)
    {
        if (path.Length == 0) return ".";
        bool rooted = path[0] == '/';
        var outp = new System.Text.StringBuilder();
        int r = 0, dotdot = 0;
        if (rooted) { outp.Append('/'); r = 1; dotdot = 1; }
        while (r < path.Length)
        {
            if (path[r] == '/') { r++; }
            else if (path[r] == '.' && (r + 1 == path.Length || path[r + 1] == '/')) { r++; }
            else if (path[r] == '.' && path[r + 1] == '.' && (r + 2 == path.Length || path[r + 2] == '/'))
            {
                r += 2;
                if (outp.Length > dotdot)
                {
                    int w = outp.Length - 1;
                    while (w > dotdot && outp[w] != '/') w--;
                    outp.Length = w;
                }
                else if (!rooted)
                {
                    if (outp.Length > 0) outp.Append('/');
                    outp.Append(".."); dotdot = outp.Length;
                }
            }
            else
            {
                if ((rooted && outp.Length != 1) || (!rooted && outp.Length != 0)) outp.Append('/');
                while (r < path.Length && path[r] != '/') outp.Append(path[r++]);
            }
        }
        return outp.Length == 0 ? "." : outp.ToString();
    }

    // filepath aliases (slash OS).
    public static GoString ToSlash(GoString p) => p;
    public static GoString FromSlash(GoString p) => p;

    // ---- path.Match / filepath.Match (faithful port of Go's path/match.go over UTF-8 bytes) ----
    public static readonly GoError ErrBadPatternSentinel = new(GoString.FromDotNetString("syntax error in pattern"));
    public static object ErrBadPattern() => ErrBadPatternSentinel;

    public static object?[] Match(GoString pattern, GoString name)
    {
        byte[] pat = pattern.Bytes, nm = name.Bytes;
        while (pat.Length > 0)
        {
            var (star, chunk, rest) = ScanChunk(pat);
            pat = rest;
            if (star && chunk.Length == 0) return new object?[] { !ContainsSlash(nm), null };
            var (t, ok, err) = MatchChunk(chunk, nm);
            if (ok && (t.Length == 0 || pat.Length > 0)) { nm = t; continue; }
            if (err != null) return new object?[] { false, err };
            if (star)
            {
                bool advanced = false;
                for (int i = 0; i < nm.Length && nm[i] != (byte)'/'; i++)
                {
                    var (t2, ok2, err2) = MatchChunk(chunk, Sub(nm, i + 1));
                    if (ok2)
                    {
                        if (pat.Length == 0 && t2.Length > 0) continue;
                        nm = t2; advanced = true; break;
                    }
                    if (err2 != null) return new object?[] { false, err2 };
                }
                if (advanced) continue;
            }
            return new object?[] { false, null };
        }
        return new object?[] { nm.Length == 0, null };
    }

    // filepath.Glob(pattern) ([]string, error): the names of all files matching pattern
    // (filepath.Match syntax). File-system errors are ignored (a non-existent or unreadable
    // directory yields no matches); only a malformed pattern returns ErrBadPattern. Results
    // within a directory are sorted, matching Go's use of a sorted Readdirnames.
    public static object?[] Glob(GoString pattern)
    {
        var (matches, err) = GlobImpl(pattern.ToDotNetString(), 0);
        if (err != null) return new object?[] { NilStrSlice(), err };
        if (matches == null || matches.Count == 0) return new object?[] { NilStrSlice(), null };
        return new object?[] { StrSlice(matches), null };
    }

    private static GoSlice NilStrSlice() => new GoSlice { Data = null, Off = 0, Len = 0, Cap = 0 };
    private static GoSlice StrSlice(System.Collections.Generic.List<string> xs)
    {
        var d = new object?[xs.Count];
        for (int i = 0; i < xs.Count; i++) d[i] = GoString.FromDotNetString(xs[i]);
        return new GoSlice { Data = d, Off = 0, Len = d.Length, Cap = d.Length };
    }

    private static bool HasMeta(string path)
    {
        foreach (char c in path) if (c == '*' || c == '?' || c == '[' || c == '\\') return true;
        return false;
    }

    private static (System.Collections.Generic.List<string>?, object?) GlobImpl(string pattern, int depth)
    {
        if (depth >= 10000) return (null, ErrBadPatternSentinel);
        if (!HasMeta(pattern))
        {
            if (!(System.IO.File.Exists(pattern) || System.IO.Directory.Exists(pattern)))
                return (new System.Collections.Generic.List<string>(), null);
            return (new System.Collections.Generic.List<string> { pattern }, null);
        }
        var sp = Split(GoString.FromDotNetString(pattern));
        string dir = ((GoString)sp[0]!).ToDotNetString();
        string file = ((GoString)sp[1]!).ToDotNetString();
        dir = CleanGlobPath(dir);
        if (!HasMeta(dir))
            return GlobDir(dir, file, new System.Collections.Generic.List<string>());
        if (dir == pattern) return (null, ErrBadPatternSentinel);
        var (m, e) = GlobImpl(dir, depth + 1);
        if (e != null) return (null, e);
        var result = new System.Collections.Generic.List<string>();
        foreach (var d in m!)
        {
            var (r, e2) = GlobDir(d, file, result);
            if (e2 != null) return (null, e2);
            result = r!;
        }
        return (result, null);
    }

    // Go's cleanGlobPath: "" -> ".", "/" stays, otherwise drop the trailing separator.
    private static string CleanGlobPath(string path)
    {
        if (path.Length == 0) return ".";
        if (path == "/") return path;
        return path.TrimEnd('/');
    }

    private static (System.Collections.Generic.List<string>?, object?) GlobDir(string dir, string pattern, System.Collections.Generic.List<string> matches)
    {
        string statDir = dir.Length == 0 ? "." : dir;
        if (!System.IO.Directory.Exists(statDir)) return (matches, null); // not a dir / missing -> ignored
        string[] names;
        try { names = System.IO.Directory.GetFileSystemEntries(statDir); }
        catch (System.Exception) { return (matches, null); }
        var bases = new System.Collections.Generic.List<string>(names.Length);
        foreach (var n in names) bases.Add(System.IO.Path.GetFileName(n));
        bases.Sort(System.StringComparer.Ordinal);
        foreach (var n in bases)
        {
            var mr = Match(GoString.FromDotNetString(pattern), GoString.FromDotNetString(n));
            if (mr[1] != null) return (matches, mr[1]);
            if ((bool)mr[0]!) matches.Add(JoinTwo(dir, n));
        }
        return (matches, null);
    }

    private static string JoinTwo(string a, string b)
    {
        if (a.Length == 0) return Clean(b);
        return Clean(a + "/" + b);
    }

    private static byte[] Sub(byte[] b, int from) => Sub(b, from, b.Length);
    private static byte[] Sub(byte[] b, int from, int to)
    {
        var r = new byte[to - from];
        System.Array.Copy(b, from, r, 0, r.Length);
        return r;
    }
    private static bool ContainsSlash(byte[] b) { foreach (var x in b) if (x == (byte)'/') return true; return false; }

    private static (bool star, byte[] chunk, byte[] rest) ScanChunk(byte[] pattern)
    {
        bool star = false;
        int p = 0;
        while (p < pattern.Length && pattern[p] == (byte)'*') { p++; star = true; }
        bool inrange = false;
        int i;
        for (i = p; i < pattern.Length; i++)
        {
            byte c = pattern[i];
            if (c == (byte)'\\') { if (i + 1 < pattern.Length) i++; }
            else if (c == (byte)'[') inrange = true;
            else if (c == (byte)']') inrange = false;
            else if (c == (byte)'*') { if (!inrange) break; }
        }
        return (star, Sub(pattern, p, i), Sub(pattern, i));
    }

    private static (byte[] rest, bool ok, object? err) MatchChunk(byte[] chunk, byte[] s)
    {
        bool failed = false;
        int ci = 0, si = 0;
        while (chunk.Length - ci > 0)
        {
            if (!failed && s.Length - si == 0) failed = true;
            byte c0 = chunk[ci];
            if (c0 == (byte)'[')
            {
                int r = 0;
                if (!failed) { var (rr, n) = DecodeRune(s, si); r = rr; si += n; }
                ci++;
                bool negated = false;
                if (chunk.Length - ci > 0 && chunk[ci] == (byte)'^') { negated = true; ci++; }
                bool match = false;
                int nrange = 0;
                while (true)
                {
                    if (chunk.Length - ci > 0 && chunk[ci] == (byte)']' && nrange > 0) { ci++; break; }
                    var (lo, ci1, e1) = GetEsc(chunk, ci);
                    if (e1 != null) return (System.Array.Empty<byte>(), false, e1);
                    ci = ci1;
                    int hi = lo;
                    if (chunk[ci] == (byte)'-')
                    {
                        var (hir, ci2, e2) = GetEsc(chunk, ci + 1);
                        if (e2 != null) return (System.Array.Empty<byte>(), false, e2);
                        hi = hir; ci = ci2;
                    }
                    if (lo <= r && r <= hi) match = true;
                    nrange++;
                }
                if (match == negated) failed = true;
            }
            else if (c0 == (byte)'?')
            {
                if (!failed)
                {
                    if (s[si] == (byte)'/') failed = true;
                    var (_, n) = DecodeRune(s, si); si += n;
                }
                ci++;
            }
            else if (c0 == (byte)'\\')
            {
                ci++;
                if (chunk.Length - ci == 0) return (System.Array.Empty<byte>(), false, ErrBadPatternSentinel);
                if (!failed) { if (chunk[ci] != s[si]) failed = true; si++; }
                ci++;
            }
            else
            {
                if (!failed) { if (chunk[ci] != s[si]) failed = true; si++; }
                ci++;
            }
        }
        if (failed) return (System.Array.Empty<byte>(), false, null);
        return (Sub(s, si), true, null);
    }

    private static (int r, int nchunk, object? err) GetEsc(byte[] chunk, int ci)
    {
        if (chunk.Length - ci == 0 || chunk[ci] == (byte)'-' || chunk[ci] == (byte)']')
            return (0, ci, ErrBadPatternSentinel);
        if (chunk[ci] == (byte)'\\')
        {
            ci++;
            if (chunk.Length - ci == 0) return (0, ci, ErrBadPatternSentinel);
        }
        var (r, n) = DecodeRune(chunk, ci);
        object? err = null;
        if (r == 0xFFFD && n == 1) err = ErrBadPatternSentinel;
        int nci = ci + n;
        if (chunk.Length - nci == 0) err = ErrBadPatternSentinel;
        return (r, nci, err);
    }

    // Minimal utf8.DecodeRune: (rune, size); invalid sequences yield (0xFFFD, 1) like Go.
    private static (int r, int size) DecodeRune(byte[] b, int i)
    {
        if (i >= b.Length) return (0xFFFD, 0);
        byte b0 = b[i];
        if (b0 < 0x80) return (b0, 1);
        if ((b0 & 0xE0) == 0xC0)
        {
            if (i + 1 < b.Length && (b[i + 1] & 0xC0) == 0x80)
            {
                int r = ((b0 & 0x1F) << 6) | (b[i + 1] & 0x3F);
                return r < 0x80 ? (0xFFFD, 1) : (r, 2);
            }
            return (0xFFFD, 1);
        }
        if ((b0 & 0xF0) == 0xE0)
        {
            if (i + 2 < b.Length && (b[i + 1] & 0xC0) == 0x80 && (b[i + 2] & 0xC0) == 0x80)
            {
                int r = ((b0 & 0x0F) << 12) | ((b[i + 1] & 0x3F) << 6) | (b[i + 2] & 0x3F);
                if (r < 0x800 || (r >= 0xD800 && r <= 0xDFFF)) return (0xFFFD, 1);
                return (r, 3);
            }
            return (0xFFFD, 1);
        }
        if ((b0 & 0xF8) == 0xF0)
        {
            if (i + 3 < b.Length && (b[i + 1] & 0xC0) == 0x80 && (b[i + 2] & 0xC0) == 0x80 && (b[i + 3] & 0xC0) == 0x80)
            {
                int r = ((b0 & 0x07) << 18) | ((b[i + 1] & 0x3F) << 12) | ((b[i + 2] & 0x3F) << 6) | (b[i + 3] & 0x3F);
                if (r < 0x10000 || r > 0x10FFFF) return (0xFFFD, 1);
                return (r, 4);
            }
            return (0xFFFD, 1);
        }
        return (0xFFFD, 1);
    }
}
