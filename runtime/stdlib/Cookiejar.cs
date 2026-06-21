namespace GoCLR.Stdlib;

using System.Collections.Generic;
using GoCLR.Runtime;

/// <summary>An in-memory net/http/cookiejar.Jar: cookies grouped by effective domain,
/// each keyed by name+path so a re-set overwrites and a negative MaxAge deletes.</summary>
public sealed class GoCookieJar
{
    public readonly Dictionary<string, Dictionary<string, GoCookie>> ByDomain = new();
}

/// <summary>Shim for net/http/cookiejar over an in-memory store with RFC 6265-style
/// domain/path matching (enough for an http.Client that carries session cookies).</summary>
public static class Cookiejar
{
    // cookiejar.New(o *Options) (*Jar, error) — the options only select a PSL, which
    // this store does not need, so they are ignored.
    public static object?[] New(GoPtr? options) => new object?[] { new GoCookieJar(), null };

    private static GoCookie AsCookie(object? o) => o switch
    {
        GoCookie c => c,
        GoPtr p => (GoCookie)GoPtrs.Get(p)!,
        _ => (GoCookie)o!,
    };

    private static GoUrl AsUrl(object? o) => o switch
    {
        GoUrl u => u,
        GoPtr p => (GoUrl)GoPtrs.Get(p)!,
        _ => (GoUrl)o!,
    };

    // The host with any port stripped, lowercased.
    private static string HostOnly(string host)
    {
        int colon = host.LastIndexOf(':');
        if (colon >= 0 && host.IndexOf(']') < colon) host = host.Substring(0, colon);
        return host.ToLowerInvariant();
    }

    private static string Defaulted(string p) => p.Length == 0 ? "/" : p;

    // A request host matches a stored domain if equal or a subdomain of it.
    private static bool DomainMatch(string host, string domain) =>
        host == domain || (host.Length > domain.Length && host.EndsWith("." + domain));

    // RFC 6265 path-match: the cookie path is a prefix of the request path on a
    // boundary ("/a" matches "/a", "/a/b", but not "/ab").
    private static bool PathMatch(string requestPath, string cookiePath)
    {
        cookiePath = Defaulted(cookiePath);
        if (requestPath == cookiePath) return true;
        if (!requestPath.StartsWith(cookiePath)) return false;
        return cookiePath.EndsWith("/") || requestPath[cookiePath.Length] == '/';
    }

    public static void Jar_SetCookies(object jar, object? u, GoSlice cookies)
    {
        var j = (GoCookieJar)jar;
        var url = AsUrl(u);
        string host = HostOnly(url.Host);
        if (host.Length == 0) return;
        for (int i = 0; i < cookies.Len; i++)
        {
            var c = AsCookie(cookies.Data![cookies.Off + i]);
            string domain = c.Domain.Length > 0 ? c.Domain.TrimStart('.').ToLowerInvariant() : host;
            if (!DomainMatch(host, domain) && host != domain) domain = host; // reject cross-domain set
            if (!j.ByDomain.TryGetValue(domain, out var m)) j.ByDomain[domain] = m = new();
            string key = c.Name + "\n" + Defaulted(c.Path);
            if (c.MaxAge < 0) m.Remove(key);
            else m[key] = c;
        }
    }

    public static GoSlice Jar_Cookies(object jar, object? u)
    {
        var j = (GoCookieJar)jar;
        var url = AsUrl(u);
        string host = HostOnly(url.Host);
        string path = Defaulted(url.Path);
        var outList = new List<object?>();
        foreach (var (domain, m) in j.ByDomain)
        {
            if (!DomainMatch(host, domain)) continue;
            foreach (var c in m.Values)
                if (PathMatch(path, c.Path)) outList.Add(c);
        }
        return new GoSlice { Data = outList.ToArray(), Off = 0, Len = outList.Count, Cap = outList.Count };
    }
}
