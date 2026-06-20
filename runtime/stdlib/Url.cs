namespace GoCLR.Stdlib;

using System.Text;
using GoCLR.Runtime;

/// <summary>A *url.URL handle (parsed components).</summary>
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

    public static GoString URL_Scheme(object u) => GoString.FromDotNetString(((GoUrl)u).Scheme);
    public static GoString URL_Host(object u) => GoString.FromDotNetString(((GoUrl)u).Host);
    public static GoString URL_Path(object u) => GoString.FromDotNetString(((GoUrl)u).Path);
    public static GoString URL_RawQuery(object u) => GoString.FromDotNetString(((GoUrl)u).RawQuery);
    public static GoString URL_Fragment(object u) => GoString.FromDotNetString(((GoUrl)u).Fragment);
    public static GoString URL_Opaque(object u) => GoString.FromDotNetString(((GoUrl)u).Opaque);
    public static object? URL_User(object u) => ((GoUrl)u).User.Length == 0 ? null : GoString.FromDotNetString(((GoUrl)u).User);

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
