namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>Shim for a subset of Go's <c>mime</c> (extension -> content type).</summary>
public static class Mime
{
    private static readonly System.Collections.Generic.Dictionary<string, string> Types = new()
    {
        [".html"] = "text/html; charset=utf-8", [".htm"] = "text/html; charset=utf-8",
        [".css"] = "text/css; charset=utf-8", [".js"] = "text/javascript; charset=utf-8",
        [".json"] = "application/json", [".xml"] = "text/xml; charset=utf-8",
        [".txt"] = "text/plain; charset=utf-8", [".png"] = "image/png", [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg", [".gif"] = "image/gif", [".svg"] = "image/svg+xml",
        [".pdf"] = "application/pdf", [".zip"] = "application/zip", [".csv"] = "text/csv; charset=utf-8",
        [".wasm"] = "application/wasm", [".webp"] = "image/webp", [".ico"] = "image/x-icon",
    };
    public static GoString TypeByExtension(GoString ext) =>
        GoString.FromDotNetString(Types.TryGetValue(ext.ToDotNetString().ToLowerInvariant(), out var t) ? t : "");

    // ParseMediaType(v) (mediatype string, params map[string]string, err error):
    // split on ';', lowercase the media type, and collect key=value parameters
    // (unquoting double-quoted values). Returns an error for an empty media type.
    public static object?[] ParseMediaType(GoString v)
    {
        string s = v.ToDotNetString();
        int semi = s.IndexOf(';');
        string mt = (semi < 0 ? s : s.Substring(0, semi)).Trim().ToLowerInvariant();
        if (mt.Length == 0)
            return new object?[] { GoString.FromDotNetString(""), new GoMap { Data = null },
                new GoError("mime: no media type") };

        var data = new System.Collections.Generic.Dictionary<object, object?>();
        int i = semi;
        while (i >= 0 && i < s.Length)
        {
            int next = s.IndexOf(';', i + 1);
            string part = (next < 0 ? s.Substring(i + 1) : s.Substring(i + 1, next - i - 1)).Trim();
            int eq = part.IndexOf('=');
            if (eq > 0)
            {
                string key = part.Substring(0, eq).Trim().ToLowerInvariant();
                string val = part.Substring(eq + 1).Trim();
                if (val.Length >= 2 && val[0] == '"' && val[^1] == '"') val = val.Substring(1, val.Length - 2);
                if (key.Length > 0) data[GoString.FromDotNetString(key)] = GoString.FromDotNetString(val);
            }
            i = next;
        }
        return new object?[] { GoString.FromDotNetString(mt), new GoMap { Data = data }, null };
    }
}
