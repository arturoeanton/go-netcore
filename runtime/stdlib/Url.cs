namespace GoCLR.Stdlib;

using System.Text;
using GoCLR.Runtime;

/// <summary>A *url.URL handle (parsed components).</summary>
[GoShim("net/url.URL")]
public sealed class GoUrl { public string Scheme = "", Host = "", Path = "", RawQuery = "", Fragment = "", Opaque = ""; public GoUserinfo? User; }

/// <summary>url.Userinfo: the username (and optional password) of a URL's authority.</summary>
public sealed class GoUserinfo { public string Username = ""; public string Password = ""; public bool HasPassword; }

/// <summary>url.Error (returned by Parse on failure): Op + URL + the wrapped error.</summary>
public sealed class GoUrlError : IGoError
{
    public string Op = "", Url = ""; public object? Err;
    public GoString Error() => GoString.FromDotNetString($"{Op} \"{Url}\": {(Err is IGoError g ? g.Error().ToDotNetString() : Err?.ToString() ?? "")}");
}

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
            if (at >= 0) { u.User = ParseUserinfo(authority.Substring(0, at)); authority = authority.Substring(at + 1); }
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
    public static object? URL_User(object u) => ((GoUrl)u).User;
    public static void URL_SetUser(object u, object? v) => ((GoUrl)u).User = v as GoUserinfo;

    private static GoUserinfo? ParseUserinfo(string s)
    {
        if (s.Length == 0) return null;
        int c = s.IndexOf(':');
        return c < 0
            ? new GoUserinfo { Username = s }
            : new GoUserinfo { Username = s.Substring(0, c), Password = s.Substring(c + 1), HasPassword = true };
    }

    // url.User(username) / url.UserPassword(username, password) -> *Userinfo.
    public static object User(GoString name) => new GoUserinfo { Username = name.ToDotNetString() };
    public static object UserPassword(GoString name, GoString pw) => new GoUserinfo { Username = name.ToDotNetString(), Password = pw.ToDotNetString(), HasPassword = true };
    public static GoString Userinfo_Username(object ui) => GoString.FromDotNetString(((GoUserinfo)ui).Username);
    public static object?[] Userinfo_Password(object ui) { var u = (GoUserinfo)ui; return new object?[] { GoString.FromDotNetString(u.Password), u.HasPassword }; }
    public static GoString Userinfo_String(object ui)
    {
        var u = (GoUserinfo)ui;
        return GoString.FromDotNetString(u.HasPassword ? Escape(u.Username, true) + ":" + Escape(u.Password, true) : Escape(u.Username, true));
    }

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

    // Zero value of url.URL, so a &url.URL{...} composite literal has a real backing object
    // for its field setters to write into instead of a null receiver.
    public static object URL_Zero() => new GoUrl();
    public static void URL_SetPath(object u, GoString v) => ((GoUrl)u).Path = v.ToDotNetString();
    public static void URL_SetScheme(object u, GoString v) => ((GoUrl)u).Scheme = v.ToDotNetString();
    public static void URL_SetHost(object u, GoString v) => ((GoUrl)u).Host = v.ToDotNetString();
    public static void URL_SetRawQuery(object u, GoString v) => ((GoUrl)u).RawQuery = v.ToDotNetString();
    public static void URL_SetFragment(object u, GoString v) => ((GoUrl)u).Fragment = v.ToDotNetString();
    public static void URL_SetOpaque(object u, GoString v) => ((GoUrl)u).Opaque = v.ToDotNetString();

    public static bool URL_IsAbs(object u) => ((GoUrl)u).Scheme.Length > 0;
    // (*URL).Hostname / Port split Host into host and port (Host may be "[ipv6]:port").
    public static GoString URL_Hostname(object u) => GoString.FromDotNetString(SplitHostPort(((GoUrl)u).Host).host);
    public static GoString URL_Port(object u) => GoString.FromDotNetString(SplitHostPort(((GoUrl)u).Host).port);
    private static (string host, string port) SplitHostPort(string h)
    {
        int colon = h.LastIndexOf(':');
        int bracket = h.LastIndexOf(']');
        if (colon > bracket && colon >= 0) return (h.Substring(0, colon), h.Substring(colon + 1));
        return (h, "");
    }
    public static GoString URL_EscapedPath(object u)
    {
        // The whole path keeps its '/' separators; only each segment is escaped.
        var parts = ((GoUrl)u).Path.Split('/');
        for (int i = 0; i < parts.Length; i++) parts[i] = Escape(parts[i], true);
        return GoString.FromDotNetString(string.Join("/", parts));
    }
    public static GoString URL_EscapedFragment(object u) => GoString.FromDotNetString(Escape(((GoUrl)u).Fragment, true));
    // (*URL).Redacted: String() with any password replaced by "xxxxx".
    public static GoString URL_Redacted(object uo)
    {
        var u = (GoUrl)uo;
        if (u.User == null || !u.User.HasPassword) return URL_String(uo);
        var saved = u.User;
        u.User = new GoUserinfo { Username = saved.Username, Password = "xxxxx", HasPassword = true };
        var s = URL_String(uo);
        u.User = saved;
        return s;
    }
    // (*URL).Parse(ref): parse ref relative to the receiver (ResolveReference of the parsed ref).
    public static object?[] URL_Parse(object uo, GoString refStr)
    {
        var r = Parse(refStr);
        if (r[1] != null) return r;
        return new object?[] { URL_ResolveReference(uo, r[0]!), null };
    }
    public static object URL_JoinPath(object uo, GoSlice elems)
    {
        var u = (GoUrl)URL_Clone(uo);
        u.Path = JoinPathStr(PrependNonEmpty(u.Path, elems));
        return u;
    }
    // Binary marshal of a URL is its String() bytes.
    public static object?[] URL_MarshalBinary(object uo) => new object?[] { GoStrings.ToByteSlice(URL_String(uo)), null };
    public static GoSlice URL_AppendBinary(object uo, GoSlice dst)
    {
        var b = GoStrings.ToByteSlice(URL_String(uo));
        return Rt.AppendSlice(dst, b);
    }
    public static object? URL_UnmarshalBinary(object uo, GoSlice data)
    {
        var r = Parse(GoString.FromBytes(SliceBytes(data)));
        if (r[1] != null) return r[1];
        var src = (GoUrl)r[0]!; var u = (GoUrl)uo;
        u.Scheme = src.Scheme; u.Host = src.Host; u.Path = src.Path; u.RawQuery = src.RawQuery; u.Fragment = src.Fragment; u.Opaque = src.Opaque; u.User = src.User;
        return null;
    }
    private static byte[] SliceBytes(GoSlice s) { var b = new byte[s.Len]; for (int i = 0; i < s.Len; i++) b[i] = (byte)System.Convert.ToInt64(s.Data![s.Off + i]); return b; }

    // url.JoinPath(base, elem...) (string, error): clean-join path elements onto base.
    public static object?[] JoinPath(GoString baseStr, GoSlice elems)
    {
        var r = Parse(baseStr);
        if (r[1] != null) return new object?[] { GoString.FromDotNetString(""), r[1] };
        var u = (GoUrl)r[0]!;
        u.Path = JoinPathStr(PrependNonEmpty(u.Path, elems));
        return new object?[] { URL_String(u), null };
    }
    private static string[] PrependNonEmpty(string first, GoSlice elems)
    {
        var list = new System.Collections.Generic.List<string> { first };
        for (int i = 0; i < elems.Len; i++) list.Add(((GoString)elems.Data![elems.Off + i]!).ToDotNetString());
        return list.ToArray();
    }
    private static string JoinPathStr(string[] parts)
    {
        var sb = new StringBuilder();
        foreach (var p in parts) { if (p.Length == 0) continue; if (sb.Length > 0 && sb[sb.Length - 1] != '/' && p[0] != '/') sb.Append('/'); sb.Append(p); }
        return CleanPath(sb.ToString());
    }
    private static string CleanPath(string p)
    {
        if (p.Length == 0) return "";
        bool rooted = p[0] == '/';
        var stack = new System.Collections.Generic.List<string>();
        foreach (var seg in p.Split('/'))
        {
            if (seg == "" || seg == ".") continue;
            if (seg == "..") { if (stack.Count > 0 && stack[stack.Count - 1] != "..") stack.RemoveAt(stack.Count - 1); else if (!rooted) stack.Add(".."); }
            else stack.Add(seg);
        }
        string joined = string.Join("/", stack);
        string res = rooted ? "/" + joined : joined;
        if (p[p.Length - 1] == '/' && !res.EndsWith("/") && res != "/") res += "/";
        return res.Length == 0 ? (rooted ? "/" : "") : res;
    }
    // url.ParseQuery(query) (Values, error): parse "a=1&b=2" into a map[string][]string.
    public static object?[] ParseQuery(GoString query)
    {
        var m = GoMaps.Make();
        ParseQueryInto(m, query.ToDotNetString());
        return new object?[] { m, null };
    }
    private static void ParseQueryInto(GoMap m, string q)
    {
        foreach (var pair in q.Split('&'))
        {
            if (pair.Length == 0) continue;
            int eq = pair.IndexOf('=');
            string k = eq < 0 ? pair : pair.Substring(0, eq);
            string v = eq < 0 ? "" : pair.Substring(eq + 1);
            var ku = Unescape(k, true); var vu = Unescape(v, true);
            if (ku[1] != null || vu[1] != null) continue;
            Values_Add(m, (GoString)ku[0]!, (GoString)vu[0]!);
        }
    }

    public static GoString URL_String(object uo)
    {
        var u = (GoUrl)uo;
        var sb = new System.Text.StringBuilder();
        if (u.Scheme.Length > 0) sb.Append(u.Scheme).Append(':');
        if (u.Opaque.Length > 0) { sb.Append(u.Opaque); }
        else
        {
            if (u.Host.Length > 0 || u.Scheme.Length > 0) sb.Append("//");
            if (u.User != null) sb.Append(Userinfo_String(u.User).ToDotNetString()).Append('@');
            sb.Append(u.Host).Append(u.Path);
        }
        if (u.RawQuery.Length > 0) sb.Append('?').Append(u.RawQuery);
        if (u.Fragment.Length > 0) sb.Append('#').Append(u.Fragment);
        return GoString.FromDotNetString(sb.ToString());
    }

    // url.Error / EscapeError / InvalidHostError methods.
    public static GoString URLError_Error(object e) => ((GoUrlError)e).Error();
    public static object? URLError_Unwrap(object e) => ((GoUrlError)e).Err;
    public static bool URLError_Timeout(object e) { var err = ((GoUrlError)e).Err; return err != null && Bridge.HasMethod(err, "Timeout") && Bridge.CallMethod(err, "Timeout") is bool b && b; }
    public static bool URLError_Temporary(object e) { var err = ((GoUrlError)e).Err; return err != null && Bridge.HasMethod(err, "Temporary") && Bridge.CallMethod(err, "Temporary") is bool b && b; }
    public static GoString EscapeError_Error(GoString s) => GoString.FromDotNetString("invalid URL escape " + Strconv.Quote(s).ToDotNetString());
    public static GoString InvalidHostError_Error(GoString s) => GoString.FromDotNetString("invalid character " + Strconv.Quote(s).ToDotNetString() + " in host name");

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
