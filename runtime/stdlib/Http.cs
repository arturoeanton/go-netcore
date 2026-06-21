namespace GoCLR.Stdlib;

using System.Net;
using System.Net.Http;
using GoCLR.Runtime;

/// <summary>An *http.Response handle (status + body snapshot).</summary>
public sealed class GoResponse { public int StatusCode; public string Status = ""; public GoReader Body = new(); public long ContentLength; }

/// <summary>An http.ResponseWriter backed by an HttpListenerResponse.</summary>
public sealed class GoRespWriter { public HttpListenerResponse Resp = null!; public bool WroteHeader; public GoMap? Headers; }

/// <summary>An *http.Request handle (server side).</summary>
public sealed class GoRequest { public string Method = ""; public GoUrl Url = new(); public GoReader Body = new(); public string Host = ""; public string RemoteAddr = ""; public GoMap? Form; public GoMap? PostForm; public GoMap? Header; public byte[] RawBody = System.Array.Empty<byte>(); }

/// <summary>Shim for Go's <c>net/http</c> client (over a pooled HttpClient).
/// Server-side and streaming bodies are out of scope; the body is read eagerly.</summary>
public static class Http
{
    private static readonly HttpClient Client = new();

    private static GoResponse Make(HttpResponseMessage m)
    {
        byte[] body = m.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
        return new GoResponse
        {
            StatusCode = (int)m.StatusCode,
            Status = (int)m.StatusCode + " " + m.ReasonPhrase,
            Body = new GoReader { Data = body },
            ContentLength = body.Length,
        };
    }

    public static object?[] Get(GoString url)
    {
        try { return new object?[] { Make(Client.GetAsync(url.ToDotNetString()).GetAwaiter().GetResult()), null }; }
        catch (System.Exception e) { return new object?[] { null, new GoError(GoString.FromDotNetString("Get " + url.ToDotNetString() + ": " + e.Message)) }; }
    }

    public static object?[] Post(GoString url, GoString contentType, object? body)
    {
        try
        {
            var content = new ByteArrayContent(Readers.Drain(body));
            content.Headers.TryAddWithoutValidation("Content-Type", contentType.ToDotNetString());
            return new object?[] { Make(Client.PostAsync(url.ToDotNetString(), content).GetAwaiter().GetResult()), null };
        }
        catch (System.Exception e) { return new object?[] { null, new GoError(GoString.FromDotNetString("Post " + url.ToDotNetString() + ": " + e.Message)) }; }
    }

    // *Response field getters.
    public static long Resp_StatusCode(object r) => ((GoResponse)r).StatusCode;
    public static GoString Resp_Status(object r) => GoString.FromDotNetString(((GoResponse)r).Status);
    public static object Resp_Body(object r) => ((GoResponse)r).Body;
    public static long Resp_ContentLength(object r) => ((GoResponse)r).ContentLength;

    // io.ReadCloser.Close on a response body is a no-op (body already buffered).
    public static object? Body_Close(object r) => null;

    // ---- server (over System.Net.HttpListener) ----------------------------

    private static readonly System.Collections.Generic.List<(string pat, GoClosure h)> Mux = new();

    public static void HandleFunc(GoString pattern, GoClosure handler)
    {
        lock (Mux) Mux.Add((pattern.ToDotNetString(), handler));
    }

    public static object? ListenAndServe(GoString addr, object? handler)
    {
        try
        {
            string a = addr.ToDotNetString();
            string host = a.StartsWith(":") ? "localhost" + a : a;
            var listener = new HttpListener();
            listener.Prefixes.Add("http://" + host + "/");
            listener.Start();
            while (true)
            {
                var ctx = listener.GetContext();
                var w = new GoRespWriter { Resp = ctx.Response };
                var req = MakeRequest(ctx.Request);
                var h = handler as GoClosure ?? Match(ctx.Request.Url?.AbsolutePath ?? "/");
                try { if (h != null) GoRuntime.InvokeArgs(h, w, req); }
                catch (System.Exception) { }
                try { ctx.Response.OutputStream.Close(); } catch { }
            }
        }
        catch (System.Exception e) { return new GoError(GoString.FromDotNetString("http: " + e.Message)); }
    }

    private static GoClosure? Match(string path)
    {
        GoClosure? best = null; int bestLen = -1;
        lock (Mux)
            foreach (var (pat, h) in Mux)
                if ((pat == "/" || path == pat || path.StartsWith(pat)) && pat.Length > bestLen) { best = h; bestLen = pat.Length; }
        return best;
    }

    private static GoRequest MakeRequest(HttpListenerRequest r)
    {
        var u = new GoUrl { Path = r.Url?.AbsolutePath ?? "/", RawQuery = (r.Url?.Query ?? "").TrimStart('?'), Host = r.Url?.Authority ?? "" };
        byte[] body;
        using (var ms = new System.IO.MemoryStream()) { r.InputStream.CopyTo(ms); body = ms.ToArray(); }
        var hdr = GoMaps.Make();
        foreach (string? key in r.Headers.AllKeys)
        {
            if (key == null) continue;
            var vals = new GoSlice { Data = new object?[] { GoString.FromDotNetString(r.Headers[key] ?? "") }, Off = 0, Len = 1, Cap = 1 };
            hdr.Data![GoString.FromDotNetString(key)] = vals;
        }
        return new GoRequest { Method = r.HttpMethod, Url = u, Body = new GoReader { Data = body }, Host = r.UserHostName ?? "", RemoteAddr = r.RemoteEndPoint?.ToString() ?? "", RawBody = body, Header = hdr };
    }

    public static object Req_Header(object r)
    {
        var rq = (GoRequest)r;
        return rq.Header ??= GoMaps.Make();
    }

    /// <summary>http.ErrNotMultipart — the sentinel ParseMultipartForm returns for a
    /// non-multipart request (gin checks errors.Is(err, http.ErrNotMultipart)).</summary>
    public static readonly GoError ErrNotMultipartSentinel = new(GoString.FromDotNetString("request Content-Type isn't multipart/form-data"));
    public static object ErrNotMultipart() => ErrNotMultipartSentinel;

    // (*http.Request).ParseForm(): parse the URL query and a urlencoded body into Form
    // (url.Values = map[string][]string).
    public static object? Req_ParseForm(object ro)
    {
        var r = (GoRequest)ro;
        var m = GoMaps.Make();
        var post = GoMaps.Make();
        AddQuery(m, r.Url.RawQuery);
        if (r.Method is "POST" or "PUT" or "PATCH")
        {
            string b = System.Text.Encoding.UTF8.GetString(r.RawBody);
            AddQuery(m, b);
            AddQuery(post, b);
        }
        r.Form = m;
        r.PostForm = post;
        return null;
    }
    public static object? Req_ParseMultipartForm(object ro, long maxMemory) => ErrNotMultipartSentinel;
    public static object Req_Form(object r)
    {
        var rq = (GoRequest)r;
        if (rq.Form == null) Req_ParseForm(r);
        return rq.Form!;
    }
    public static object Req_PostForm(object r)
    {
        var rq = (GoRequest)r;
        if (rq.PostForm == null) Req_ParseForm(r);
        return rq.PostForm!;
    }

    private static void AddQuery(GoMap m, string q)
    {
        if (string.IsNullOrEmpty(q)) return;
        foreach (var pair in q.Split('&'))
        {
            if (pair.Length == 0) continue;
            int eq = pair.IndexOf('=');
            string k = System.Uri.UnescapeDataString((eq < 0 ? pair : pair.Substring(0, eq)).Replace('+', ' '));
            string v = eq < 0 ? "" : System.Uri.UnescapeDataString(pair.Substring(eq + 1).Replace('+', ' '));
            var key = GoString.FromDotNetString(k);
            GoSlice vals = m.Data!.TryGetValue(key, out var ex) && ex is GoSlice s ? s : new GoSlice { Data = new object?[0], Off = 0, Len = 0, Cap = 0 };
            vals = GoSlices.AppendOne(vals, GoString.FromDotNetString(v));
            m.Data[key] = vals;
        }
    }

    // http.ResponseWriter methods.
    public static object?[] RW_Write(object w, GoSlice p)
    {
        var rw = (GoRespWriter)w;
        FlushHeaders(rw);
        var buf = new byte[p.Len];
        for (int i = 0; i < p.Len; i++) buf[i] = (byte)System.Convert.ToInt64(p.Data![p.Off + i]);
        rw.Resp.OutputStream.Write(buf, 0, buf.Length);
        return new object?[] { (long)p.Len, null };
    }
    public static void RW_WriteHeader(object w, long code) { var rw = (GoRespWriter)w; FlushHeaders(rw); if (!rw.WroteHeader) { rw.Resp.StatusCode = (int)code; rw.WroteHeader = true; } }

    // http.Redirect(w, r, url, code): set Location and write the status code.
    public static void Redirect(object w, object? r, GoString url, long code)
    {
        var rw = (GoRespWriter)w;
        try { rw.Resp.Headers["Location"] = url.ToDotNetString(); } catch { }
        RW_WriteHeader(w, code);
    }

    // http.ResponseWriter.Header() http.Header: a live map[string][]string; entries
    // set on it are flushed to the response on the first Write/WriteHeader.
    public static object RW_Header(object w)
    {
        var rw = (GoRespWriter)w;
        return rw.Headers ??= GoMaps.Make();
    }
    private static void FlushHeaders(GoRespWriter rw)
    {
        if (rw.WroteHeader || rw.Headers?.Data == null) return;
        foreach (var kv in rw.Headers.Data)
        {
            string k = ((GoString)kv.Key).ToDotNetString();
            if (kv.Value is GoSlice vs && vs.Data != null)
                for (int i = 0; i < vs.Len; i++)
                {
                    string v = ((GoString)vs.Data[vs.Off + i]!).ToDotNetString();
                    try { rw.Resp.Headers.Add(k, v); } catch { /* restricted header (Content-Length, etc.) */ }
                }
        }
    }

    // http.Header methods (receiver is the map[string][]string).
    private static GoString Canon(GoString k) => Textproto.CanonicalMIMEHeaderKey(k);
    public static GoString Header_Get(object h, GoString key)
    {
        var m = (GoMap)h;
        if (m.Data != null && m.Data.TryGetValue(Canon(key), out var v) && v is GoSlice s && s.Data != null && s.Len > 0)
            return (GoString)s.Data[s.Off]!;
        return GoString.FromDotNetString("");
    }
    public static void Header_Set(object h, GoString key, GoString val)
    {
        var m = (GoMap)h;
        m.Data![Canon(key)] = new GoSlice { Data = new object?[] { val }, Off = 0, Len = 1, Cap = 1 };
    }
    public static void Header_Add(object h, GoString key, GoString val)
    {
        var m = (GoMap)h;
        var ck = Canon(key);
        GoSlice s = m.Data!.TryGetValue(ck, out var ex) && ex is GoSlice gs ? gs : new GoSlice { Data = new object?[0], Off = 0, Len = 0, Cap = 0 };
        m.Data[ck] = GoSlices.AppendOne(s, val);
    }
    public static void Header_Del(object h, GoString key) => ((GoMap)h).Data?.Remove(Canon(key));
    public static GoString Header_Values(object h, GoString key) => Header_Get(h, key);

    // *http.Request field getters.
    public static GoString Req_Method(object r) => GoString.FromDotNetString(((GoRequest)r).Method);
    public static object Req_URL(object r) => ((GoRequest)r).Url;
    public static object Req_Body(object r) => ((GoRequest)r).Body;
    public static GoString Req_Host(object r) => GoString.FromDotNetString(((GoRequest)r).Host);
    public static GoString Req_RemoteAddr(object r) => GoString.FromDotNetString(((GoRequest)r).RemoteAddr);
}
