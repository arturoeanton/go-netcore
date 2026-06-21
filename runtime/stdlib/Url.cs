namespace GoCLR.Stdlib;

using System.Text;
using GoCLR.Runtime;

/// <summary>A *url.URL handle (parsed components).</summary>
[GoShim("net/url.URL")]
public sealed class GoUrl { public string Scheme = "", Host = "", Path = "", RawQuery = "", Fragment = "", Opaque = "", User = ""; }

/// <summary>Shim for Go's <c>net/url</c> (escapes + Parse with field getters).</summary>
public static class Url
{
    public static object?[] Parse(GoString raw)
    {
        string s = raw.ToDotNetString();
        var u = new GoUrl();
        int hash = s.IndexOf('#');
        if (hash >= 0) { u.Fragment = s.Substring(hash + 1); s = s.Substring(0, hash); }
        int q = s.IndexOf('?');
        if (q >= 0) { u.RawQuery = s.Substring(q + 1); s = s.Substring(0, q); }
        int scheme = s.IndexOf("://", System.StringComparison.Ordinal);
        if (scheme >= 0)
        {
            u.Scheme = s.Substring(0, scheme);
            string rest = s.Substring(scheme + 3);
            int slash = rest.IndexOf('/');
            string authority = slash < 0 ? rest : rest.Substring(0, slash);
            u.Path = slash < 0 ? "" : rest.Substring(slash);
            int at = authority.IndexOf('@');
            if (at >= 0) { u.User = authority.Substring(0, at); authority = authority.Substring(at + 1); }
            u.Host = authority;
        }
        else if (s.Contains(":") && !s.StartsWith("/"))
        {
            int c = s.IndexOf(':');
            u.Scheme = s.Substring(0, c); u.Opaque = s.Substring(c + 1);
        }
        else u.Path = s;
        return new object?[] { u, null };
    }

    // url.ParseRequestURI: like Parse but the URL must be absolute (have a scheme).
    public static object?[] ParseRequestURI(GoString raw)
    {
        var r = Parse(raw);
        var u = (GoUrl)r[0]!;
        if (u.Scheme.Length == 0)
            return new object?[] { u, new GoError("parse " + raw.ToDotNetString() + ": invalid URI for request") };
        return r;
    }

    // (*url.URL).Query() url.Values: parse RawQuery into a map[string][]string.
    public static GoMap URL_Query(object u)
    {
        var m = GoMaps.Make();
        string q = ((GoUrl)u).RawQuery;
        if (!string.IsNullOrEmpty(q))
            foreach (var pair in q.Split('&'))
            {
                if (pair.Length == 0) continue;
                int eq = pair.IndexOf('=');
                string k = System.Uri.UnescapeDataString((eq < 0 ? pair : pair.Substring(0, eq)).Replace('+', ' '));
                string v = eq < 0 ? "" : System.Uri.UnescapeDataString(pair.Substring(eq + 1).Replace('+', ' '));
                var key = GoString.FromDotNetString(k);
                GoSlice vals = m.Data!.TryGetValue(key, out var ex) && ex is GoSlice sl ? sl : new GoSlice { Data = new object?[0], Off = 0, Len = 0, Cap = 0 };
                m.Data[key] = GoSlices.AppendOne(vals, GoString.FromDotNetString(v));
            }
        return m;
    }

    // url.Values (map[string][]string) methods. The receiver is the GoMap produced by
    // URL_Query / Req_Form; values are GoSlices of GoString.
    public static GoString Values_Get(GoMap m, GoString key)
    {
        if (m.Data is var d && d != null && d.TryGetValue(key, out var v) && v is GoSlice s && s.Len > 0
            && s.Data![s.Off] is GoString gs)
            return gs;
        return GoString.FromDotNetString("");
    }
    public static void Values_Set(GoMap m, GoString key, GoString val)
    {
        m.Data![key] = new GoSlice { Data = new object?[] { val }, Off = 0, Len = 1, Cap = 1 };
    }
    public static void Values_Add(GoMap m, GoString key, GoString val)
    {
        var d = m.Data!;
        GoSlice vals = d.TryGetValue(key, out var ex) && ex is GoSlice sl ? sl : new GoSlice { Data = new object?[0], Off = 0, Len = 0, Cap = 0 };
        d[key] = GoSlices.AppendOne(vals, val);
    }
    public static void Values_Del(GoMap m, GoString key) => m.Data!.Remove(key);
    public static bool Values_Has(GoMap m, GoString key) => m.Data!.ContainsKey(key);
    public static GoString Values_Encode(GoMap m)
    {
        var d = m.Data!;
        var keys = new System.Collections.Generic.List<GoString>();
        foreach (var k in d.Keys) keys.Add((GoString)k!);
        keys.Sort((a, b) => string.CompareOrdinal(a.ToDotNetString(), b.ToDotNetString()));
        var sb = new System.Text.StringBuilder();
        foreach (var k in keys)
        {
            string ek = Escape(k.ToDotNetString(), false);
            if (d[k] is GoSlice s)
                for (int i = 0; i < s.Len; i++)
                {
                    if (sb.Length > 0) sb.Append('&');
                    sb.Append(ek).Append('=').Append(Escape(((GoString)s.Data![s.Off + i]!).ToDotNetString(), false));
                }
        }
        return GoString.FromDotNetString(sb.ToString());
    }

    // (*url.URL).RequestURI(): the path (or opaque) plus the raw query.
    public static GoString URL_RequestURI(object uo)
    {
        var u = (GoUrl)uo;
        string r = u.Opaque.Length > 0 ? u.Opaque : (u.Path.Length > 0 ? u.Path : "/");
        if (u.RawQuery.Length > 0) r += "?" + u.RawQuery;
        return GoString.FromDotNetString(r);
    }

    public static GoString URL_Scheme(object u) => GoString.FromDotNetString(((GoUrl)u).Scheme);
    public static GoString URL_Host(object u) => GoString.FromDotNetString(((GoUrl)u).Host);
    public static GoString URL_Path(object u) => GoString.FromDotNetString(((GoUrl)u).Path);
    public static GoString URL_RawQuery(object u) => GoString.FromDotNetString(((GoUrl)u).RawQuery);
    public static GoString URL_Fragment(object u) => GoString.FromDotNetString(((GoUrl)u).Fragment);
    public static GoString URL_Opaque(object u) => GoString.FromDotNetString(((GoUrl)u).Opaque);
    public static object? URL_User(object u) => ((GoUrl)u).User.Length == 0 ? null : GoString.FromDotNetString(((GoUrl)u).User);

    // ResolveReference resolves a URI reference relative to a base URL (RFC 3986,
    // practical subset — scheme/host/path merge with dot-segment removal).
    public static object URL_ResolveReference(object baseO, object refO)
    {
        var b = (GoUrl)baseO; var r = (GoUrl)refO;
        var u = new GoUrl
        {
            Scheme = r.Scheme,
            Host = r.Host,
            Path = r.Path,
            RawQuery = r.RawQuery,
            Fragment = r.Fragment,
            Opaque = r.Opaque,
            User = r.User,
        };
        if (r.Scheme == "") u.Scheme = b.Scheme;
        if (r.Scheme != "" || r.Host != "")
        {
            u.Path = RemoveDotSegments(r.Path);
            return u;
        }
        if (r.Opaque != "") { u.Host = ""; u.Path = ""; return u; }
        if (r.Path == "" && r.RawQuery == "")
        {
            u.RawQuery = b.RawQuery;
            if (r.Fragment == "") u.Fragment = b.Fragment;
        }
        u.Host = b.Host;
        u.User = b.User;
        u.Path = RemoveDotSegments(ResolvePath(b.Path, r.Path));
        return u;
    }

    private static string ResolvePath(string basePath, string refPath)
    {
        if (refPath == "") return basePath;
        if (refPath.StartsWith("/")) return refPath;
        int slash = basePath.LastIndexOf('/');
        if (slash < 0) return refPath;
        return basePath.Substring(0, slash + 1) + refPath;
    }

    private static string RemoveDotSegments(string p)
    {
        if (p == "") return "";
        bool rooted = p.StartsWith("/");
        var parts = p.Split('/');
        var outp = new System.Collections.Generic.List<string>();
        foreach (var seg in parts)
        {
            if (seg == "." ) continue;
            if (seg == "..")
            {
                if (outp.Count > 0 && outp[outp.Count - 1] != "") outp.RemoveAt(outp.Count - 1);
                continue;
            }
            outp.Add(seg);
        }
        var res = string.Join("/", outp);
        if (rooted && !res.StartsWith("/")) res = "/" + res.TrimStart('/');
        return res;
    }

    public static object URL_Clone(object uo)
    {
        var u = (GoUrl)uo;
        return new GoUrl { Scheme = u.Scheme, Host = u.Host, Path = u.Path, RawQuery = u.RawQuery, Fragment = u.Fragment, Opaque = u.Opaque, User = u.User };
    }

    public static void URL_SetPath(object u, GoString v) => ((GoUrl)u).Path = v.ToDotNetString();
    public static void URL_SetScheme(object u, GoString v) => ((GoUrl)u).Scheme = v.ToDotNetString();
    public static void URL_SetHost(object u, GoString v) => ((GoUrl)u).Host = v.ToDotNetString();
    public static void URL_SetRawQuery(object u, GoString v) => ((GoUrl)u).RawQuery = v.ToDotNetString();
    public static void URL_SetFragment(object u, GoString v) => ((GoUrl)u).Fragment = v.ToDotNetString();

    public static bool URL_IsAbs(object u) => ((GoUrl)u).Scheme.Length > 0;
    public static GoString URL_String(object uo)
    {
        var u = (GoUrl)uo;
        var sb = new System.Text.StringBuilder();
        if (u.Scheme.Length > 0) sb.Append(u.Scheme).Append(':');
        if (u.Opaque.Length > 0) { sb.Append(u.Opaque); }
        else
        {
            if (u.Host.Length > 0 || u.Scheme.Length > 0) sb.Append("//");
            if (u.User.Length > 0) sb.Append(u.User).Append('@');
            sb.Append(u.Host).Append(u.Path);
        }
        if (u.RawQuery.Length > 0) sb.Append('?').Append(u.RawQuery);
        if (u.Fragment.Length > 0) sb.Append('#').Append(u.Fragment);
        return GoString.FromDotNetString(sb.ToString());
    }

    public static GoString QueryEscape(GoString s) => GoString.FromDotNetString(Escape(s.ToDotNetString(), false));
    public static GoString PathEscape(GoString s) => GoString.FromDotNetString(Escape(s.ToDotNetString(), true));

    public static object?[] QueryUnescape(GoString s) => Unescape(s.ToDotNetString(), true);
    public static object?[] PathUnescape(GoString s) => Unescape(s.ToDotNetString(), false);

    private static bool Unreserved(char c) =>
        (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') ||
        c == '-' || c == '_' || c == '.' || c == '~';

    private static string Escape(string s, bool path)
    {
        var sb = new StringBuilder();
        foreach (byte b in Encoding.UTF8.GetBytes(s))
        {
            char c = (char)b;
            if (Unreserved(c)) sb.Append(c);
            else if (!path && c == ' ') sb.Append('+');
            else if (path && (c == '$' || c == '&' || c == '+' || c == ',' || c == '/' || c == ':' || c == ';' || c == '=' || c == '?' || c == '@')) sb.Append('%').Append(b.ToString("X2"));
            else sb.Append('%').Append(b.ToString("X2"));
        }
        return sb.ToString();
    }

    private static object?[] Unescape(string s, bool query)
    {
        var bytes = new System.Collections.Generic.List<byte>();
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '%')
            {
                if (i + 2 >= s.Length) return new object?[] { GoString.FromDotNetString(""), new GoError(GoString.FromDotNetString("invalid URL escape")) };
                try { bytes.Add(System.Convert.ToByte(s.Substring(i + 1, 2), 16)); i += 2; }
                catch { return new object?[] { GoString.FromDotNetString(""), new GoError(GoString.FromDotNetString("invalid URL escape")) }; }
            }
            else if (query && c == '+') bytes.Add((byte)' ');
            else bytes.Add((byte)c);
        }
        return new object?[] { GoString.FromBytes(bytes.ToArray()), null };
    }
}
