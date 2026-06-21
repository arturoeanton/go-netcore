namespace GoCLR.Stdlib;

using System.Text;
using GoCLR.Runtime;

/// <summary>Shim for the subset of net/http/httputil used by gin (request dumping for
/// panic logs).</summary>
public static class Httputil
{
    // httputil.DumpRequest(req, body) ([]byte, error): a textual dump of the request line,
    // headers, and (optionally) the buffered body.
    public static object?[] DumpRequest(object? req, bool body)
    {
        var sb = new StringBuilder();
        if (req is GoRequest r)
        {
            string uri = r.Url != null ? Url.URL_RequestURI(r.Url).ToDotNetString() : "/";
            sb.Append(r.Method).Append(' ').Append(uri).Append(" HTTP/1.1\r\n");
            if (r.Header?.Data != null)
                foreach (var kv in r.Header.Data)
                {
                    string k = kv.Key is GoString gk ? gk.ToDotNetString() : "";
                    if (kv.Value is GoSlice s && s.Data != null)
                        for (int i = 0; i < s.Len; i++)
                            sb.Append(k).Append(": ").Append(s.Data[s.Off + i] is GoString gv ? gv.ToDotNetString() : "").Append("\r\n");
                }
            sb.Append("\r\n");
            if (body && r.RawBody.Length > 0) sb.Append(Encoding.UTF8.GetString(r.RawBody));
        }
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var d = new object?[bytes.Length];
        for (int i = 0; i < bytes.Length; i++) d[i] = (int)bytes[i];
        return new object?[] { new GoSlice { Data = d, Off = 0, Len = bytes.Length, Cap = bytes.Length }, null };
    }
}
