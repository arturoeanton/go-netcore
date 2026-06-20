namespace GoCLR.Stdlib;

using System.Net;
using System.Net.Http;
using GoCLR.Runtime;

/// <summary>An *http.Response handle (status + body snapshot).</summary>
public sealed class GoResponse { public int StatusCode; public string Status = ""; public GoReader Body = new(); public long ContentLength; }

/// <summary>An http.ResponseWriter backed by an HttpListenerResponse.</summary>
public sealed class GoRespWriter { public HttpListenerResponse Resp = null!; public bool WroteHeader; }

/// <summary>An *http.Request handle (server side).</summary>
public sealed class GoRequest { public string Method = ""; public GoUrl Url = new(); public GoReader Body = new(); public string Host = ""; public string RemoteAddr = ""; }

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
        return new GoRequest { Method = r.HttpMethod, Url = u, Body = new GoReader { Data = body }, Host = r.UserHostName ?? "", RemoteAddr = r.RemoteEndPoint?.ToString() ?? "" };
    }

    // http.ResponseWriter methods.
    public static object?[] RW_Write(object w, GoSlice p)
    {
        var rw = (GoRespWriter)w;
        var buf = new byte[p.Len];
        for (int i = 0; i < p.Len; i++) buf[i] = (byte)System.Convert.ToInt64(p.Data![p.Off + i]);
        rw.Resp.OutputStream.Write(buf, 0, buf.Length);
        return new object?[] { (long)p.Len, null };
    }
    public static void RW_WriteHeader(object w, long code) { var rw = (GoRespWriter)w; if (!rw.WroteHeader) { rw.Resp.StatusCode = (int)code; rw.WroteHeader = true; } }

    // *http.Request field getters.
    public static GoString Req_Method(object r) => GoString.FromDotNetString(((GoRequest)r).Method);
    public static object Req_URL(object r) => ((GoRequest)r).Url;
    public static object Req_Body(object r) => ((GoRequest)r).Body;
    public static GoString Req_Host(object r) => GoString.FromDotNetString(((GoRequest)r).Host);
    public static GoString Req_RemoteAddr(object r) => GoString.FromDotNetString(((GoRequest)r).RemoteAddr);
}
