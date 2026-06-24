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

    // mime.ErrInvalidMediaParameter.
    public static readonly GoError ErrInvalidMediaParameterSentinel = new(GoString.FromDotNetString("mime: invalid media parameter"));
    public static object ErrInvalidMediaParameter() => ErrInvalidMediaParameterSentinel;

    private const string TSpecials = "()<>@,;:\\\"/[]?=";
    private static bool IsTSpecial(char r) => TSpecials.IndexOf(r) >= 0;
    private static bool IsTokenChar(char r) => r > 0x20 && r < 0x7f && !IsTSpecial(r);
    private static bool IsToken(string s)
    {
        if (s.Length == 0) return false;
        foreach (char c in s) if (!IsTokenChar(c)) return false;
        return true;
    }
    private static bool NeedsEncoding(string s)
    {
        foreach (char b in s) if ((b < ' ' || b > '~') && b != '\t') return true;
        return false;
    }
    private const string UpperHex = "0123456789ABCDEF";

    // mime.FormatMediaType(t, param) — faithful port of Go's format (sorts on the ORIGINAL
    // attribute keys, lowercases the type and attribute names but not values, RFC 2231
    // percent-encodes a non-ASCII value with the *=utf-8'' form).
    public static GoString FormatMediaType(GoString tS, GoMap? param)
    {
        var sb = new System.Text.StringBuilder();
        string t = tS.ToDotNetString();
        int slash = t.IndexOf('/');
        if (slash < 0)
        {
            if (!IsToken(t)) return GoString.FromDotNetString("");
            sb.Append(t.ToLowerInvariant());
        }
        else
        {
            string major = t.Substring(0, slash), sub = t.Substring(slash + 1);
            if (!IsToken(major) || !IsToken(sub)) return GoString.FromDotNetString("");
            sb.Append(major.ToLowerInvariant()).Append('/').Append(sub.ToLowerInvariant());
        }
        var pm = ParamPairs(param);
        var attrs = new System.Collections.Generic.List<string>(pm.Keys);
        attrs.Sort(System.StringComparer.Ordinal);
        foreach (var attribute in attrs)
        {
            string value = pm[attribute];
            sb.Append(';').Append(' ');
            if (!IsToken(attribute)) return GoString.FromDotNetString("");
            sb.Append(attribute.ToLowerInvariant());
            bool needEnc = NeedsEncoding(value);
            if (needEnc) sb.Append('*');
            sb.Append('=');
            if (needEnc)
            {
                sb.Append("utf-8''");
                foreach (byte ch in System.Text.Encoding.UTF8.GetBytes(value))
                {
                    if (ch <= ' ' || ch >= 0x7F || ch == '*' || ch == '\'' || ch == '%' || IsTSpecial((char)ch))
                    { sb.Append('%').Append(UpperHex[ch >> 4]).Append(UpperHex[ch & 0x0F]); }
                    else sb.Append((char)ch);
                }
            }
            else if (IsToken(value)) sb.Append(value);
            else
            {
                sb.Append('"');
                foreach (char c in value) { if (c == '"' || c == '\\') sb.Append('\\'); sb.Append(c); }
                sb.Append('"');
            }
        }
        return GoString.FromDotNetString(sb.ToString());
    }

    private static System.Collections.Generic.Dictionary<string, string> ParamPairs(object? param)
    {
        var d = new System.Collections.Generic.Dictionary<string, string>();
        if (param is GoMap m && m.Data != null)
            foreach (var kv in m.Data)
            {
                string k = kv.Key is GoString gk ? gk.ToDotNetString() : kv.Key.ToString() ?? "";
                string v = kv.Value is GoString gv ? gv.ToDotNetString() : kv.Value?.ToString() ?? "";
                d[k] = v;
            }
        return d;
    }

    // mime.AddExtensionType(ext, typ) error.
    public static object? AddExtensionType(GoString extS, GoString typS)
    {
        string ext = extS.ToDotNetString();
        if (ext.Length == 0 || ext[0] != '.')
            return new GoError(GoString.FromDotNetString($"mime: extension {GoQuote(ext)} missing leading dot"));
        string typ = typS.ToDotNetString();
        string head = (typ.IndexOf(';') is var sc && sc >= 0 ? typ.Substring(0, sc) : typ).Trim();
        int sl = head.IndexOf('/');
        if (sl < 0)
            return new GoError(GoString.FromDotNetString("mime: expected slash after first token"));
        var pr = ParseMediaType(typS);
        string justType = ((GoString)pr[0]!).ToDotNetString();
        var pmap = ParamPairs(pr[1]);
        string stored = typ;
        if (justType.StartsWith("text/", System.StringComparison.Ordinal) && !pmap.ContainsKey("charset"))
        {
            pmap["charset"] = "utf-8";
            var gm = new GoMap { Data = new System.Collections.Generic.Dictionary<object, object?>() };
            foreach (var kv in pmap) gm.Data![GoString.FromDotNetString(kv.Key)] = GoString.FromDotNetString(kv.Value);
            stored = FormatMediaType(GoString.FromDotNetString(justType), gm).ToDotNetString();
        }
        Types[ext.ToLowerInvariant()] = stored;
        return null;
    }
    private static string GoQuote(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
