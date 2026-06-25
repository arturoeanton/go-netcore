namespace GoCLR.Stdlib;

using System.Net;
using System.Net.Http;
using GoCLR.Runtime;

/// <summary>An *http.Response handle (status + body snapshot).</summary>
/// <summary>net/http.ProtocolError: a malformed-protocol error wrapping its message string.</summary>
[GoShim("net/http.ProtocolError")]
public sealed class GoHttpProtocolError : IGoError, IGoErrorIs
{
    public string ErrorString = "";
    public GoString Error() => GoString.FromDotNetString(ErrorString);
    public bool GoIs(object? target) => Http.ProtocolError_Is(this, target);
}

/// <summary>net/http.MaxBytesError: returned by MaxBytesReader past its limit.</summary>
[GoShim("net/http.MaxBytesError")]
public sealed class GoHttpMaxBytesError : IGoError
{
    public long Limit;
    public GoString Error() => GoString.FromDotNetString("http: request body too large");
}

public sealed class GoResponse { public int StatusCode; public string Status = ""; public GoReader Body = new(); public long ContentLength; public readonly GoFieldBag Extra = new(); }

/// <summary>An http.ResponseWriter backed by an HttpListenerResponse. The body is
/// buffered until the handler returns so headers (which a framework may set after the
/// first write, e.g. gin's Content-Type) are committed before the body is flushed.</summary>
[GoShim("net/http.ResponseWriter")]
public sealed class GoRespWriter
{
    public HttpListenerResponse Resp = null!;
    public bool WroteHeader;
    public int Status = 200;
    public GoMap? Headers;
    public readonly System.IO.MemoryStream Body = new();
}

/// <summary>An http.ResponseController over a response writer.</summary>
public sealed class GoResponseController { public object? W; }

/// <summary>An http.FileServer/StripPrefix handler (opaque; ServeHTTP is unsupported).</summary>
public sealed class GoFileServer { public object? Fs; public string StripPrefix = ""; }

/// <summary>An http.Cookie.</summary>
public sealed class GoCookie
{
    public string Name = "";
    public string Value = "";
    public string Path = "";
    public string Domain = "";
    public long MaxAge;
    public bool Secure;
    public bool HttpOnly;
    public long SameSite;
}

/// <summary>An *http.Client (over the pooled static HttpClient).</summary>
[GoShim("net/http.Client")]
public sealed class GoHttpClient { public long TimeoutNanos; }

/// <summary>An *http.Request handle (server side).</summary>
public sealed class GoRequest { public string Method = ""; public GoUrl Url = new(); public GoReader Body = new(); public string Host = ""; public string RemoteAddr = ""; public GoMap? Form; public GoMap? PostForm; public GoMap? Header; public byte[] RawBody = System.Array.Empty<byte>(); public readonly GoFieldBag Extra = new(); public System.Collections.Generic.Dictionary<string, string>? PathValues; }

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

    // http.ReadResponse(r *bufio.Reader, req *Request) (*Response, error): parse a raw HTTP
    // response off a reader. Used by fiber's in-memory Test() helper (dead code when serving);
    // goclr does not parse raw wire responses, so report an error.
    public static object?[] ReadResponse(object? r, object? req) =>
        new object?[] { null, new GoError(GoString.FromDotNetString("http: ReadResponse not supported")) };

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
    public static void Resp_SetStatusCode(object r, long v) => ((GoResponse)r).StatusCode = (int)v;
    public static void Resp_SetStatus(object r, GoString v) => ((GoResponse)r).Status = v.ToDotNetString();
    public static void Resp_SetContentLength(object r, long v) => ((GoResponse)r).ContentLength = v;
    public static void Resp_SetBody(object r, object? v) => ((GoResponse)r).Extra.Set("Body", v);
    public static object? Resp_Request(object r) => ((GoResponse)r).Extra.Get("Request");
    public static void Resp_SetRequest(object r, object? v) => ((GoResponse)r).Extra.Set("Request", v);
    public static object? Resp_TLS(object r) => ((GoResponse)r).Extra.Get("TLS");
    public static void Resp_SetTLS(object r, object? v) => ((GoResponse)r).Extra.Set("TLS", v);
    public static GoMap? Resp_Trailer(object r) => (GoMap?)((GoResponse)r).Extra.Get("Trailer");
    public static void Resp_SetTrailer(object r, GoMap? v) => ((GoResponse)r).Extra.Set("Trailer", v);
    public static bool Resp_Uncompressed(object r) => ((GoResponse)r).Extra.GetB("Uncompressed");
    public static void Resp_SetUncompressed(object r, bool v) => ((GoResponse)r).Extra.Set("Uncompressed", v);
    public static GoMap Resp_Header(object r) => (GoMap)(((GoResponse)r).Extra.Get("Header") ?? GoMaps.Make());
    public static void Resp_SetHeader(object r, GoMap? v) => ((GoResponse)r).Extra.Set("Header", v);
    public static GoString Resp_Proto(object r) => GoString.FromDotNetString("HTTP/2.0");
    public static long Resp_ProtoMajor(object r) => 2;
    public static long Resp_ProtoMinor(object r) => 0;

    // io.ReadCloser.Close on a response body is a no-op (body already buffered).
    public static object? Body_Close(object r) => null;

    // http sentinel errors + well-known vars.
    public static readonly GoError ErrAbortHandlerSentinel = new(GoString.FromDotNetString("net/http: abort Handler"));
    public static object ErrAbortHandler() => ErrAbortHandlerSentinel;
    public static readonly GoError ErrBodyNotAllowedSentinel = new(GoString.FromDotNetString("http: request method or response status code does not allow body"));
    public static object ErrBodyNotAllowed() => ErrBodyNotAllowedSentinel;
    // net/http.ErrNotSupported is a *ProtocolError; its Is matches errors.ErrUnsupported.
    public static readonly GoHttpProtocolError ErrNotSupportedSentinel = new() { ErrorString = "feature not supported" };
    public static object ErrNotSupported() => ErrNotSupportedSentinel;

    // net/http.ProtocolError {ErrorString}: Error() reports the string; Is(target) is true only
    // for ErrNotSupported vs errors.ErrUnsupported.
    public static object ProtocolErrorZero() => new GoHttpProtocolError();
    public static GoString ProtocolError_ErrorString(object e) => GoString.FromDotNetString(((GoHttpProtocolError)e).ErrorString);
    public static void ProtocolError_SetErrorString(object e, GoString v) => ((GoHttpProtocolError)e).ErrorString = v.ToDotNetString();
    public static GoString ProtocolError_Error(object e) => GoString.FromDotNetString(((GoHttpProtocolError)e).ErrorString);
    public static bool ProtocolError_Is(object e, object? target) =>
        ReferenceEquals(e, ErrNotSupportedSentinel) && ReferenceEquals(target, Errors.ErrUnsupportedSentinel);

    // net/http.MaxBytesError {Limit}: Error() is the fixed "request body too large" message.
    public static object MaxBytesErrorZero() => new GoHttpMaxBytesError();
    public static long MaxBytesError_Limit(object e) => ((GoHttpMaxBytesError)e).Limit;
    public static void MaxBytesError_SetLimit(object e, long v) => ((GoHttpMaxBytesError)e).Limit = v;
    public static GoString MaxBytesError_Error(object e) => GoString.FromDotNetString("http: request body too large");
    public static readonly GoError ErrSkipAltProtocolSentinel = new(GoString.FromDotNetString("net/http: skip alternate protocol"));
    public static object ErrSkipAltProtocol() => ErrSkipAltProtocolSentinel;
    public static readonly GoError ErrServerClosedSentinel = new(GoString.FromDotNetString("http: Server closed"));
    public static object ErrServerClosed() => ErrServerClosedSentinel;
    public static readonly GoError ErrHandlerTimeoutSentinel = new(GoString.FromDotNetString("http: Handler timeout"));
    public static object ErrHandlerTimeout() => ErrHandlerTimeoutSentinel;
    // http.ErrNoCookie: the sentinel (*Request).Cookie returns for a missing cookie.
    public static readonly GoError ErrNoCookieSentinel = new(GoString.FromDotNetString("http: named cookie not present"));
    public static object ErrNoCookie() => ErrNoCookieSentinel;
    private static readonly GoReader _noBody = new();
    public static object NoBody() => _noBody;
    private static readonly object _localAddrKey = new();
    public static object LocalAddrContextKey() => _localAddrKey;
    private static readonly object _serverCtxKey = new();
    public static object ServerContextKey() => _serverCtxKey;

    // http.NewResponseController(w): a controller over the writer. Hijack is
    // unsupported on the HttpListener backend (h2c prior-knowledge upgrade path).
    public static object NewResponseController(object w) => new GoResponseController { W = w };
    public static object?[] RC_Hijack(object rc) => new object?[] { null, null, new GoError(GoString.FromDotNetString("feature not supported")) };
    public static object? RC_Flush(object rc) => null;
    public static object? RC_SetReadDeadline(object rc, object t) => null;
    public static object? RC_SetWriteDeadline(object rc, object t) => null;
    public static object? RC_EnableFullDuplex(object rc) => null;

    // http.Error(w, msg, code): write the status code and message.
    public static void Error(object w, GoString msg, long code)
    {
        var h = RW_Header(w);
        Header_Del(h, GoString.FromDotNetString("Content-Length"));
        Header_Set(h, GoString.FromDotNetString("Content-Type"), GoString.FromDotNetString("text/plain; charset=utf-8"));
        Header_Set(h, GoString.FromDotNetString("X-Content-Type-Options"), GoString.FromDotNetString("nosniff"));
        RW_WriteHeader(w, code);
        Fmt.WriteTo(w, msg.ToDotNetString() + "\n");
    }
    // http.NotFound(w, r): reply with a 404 "page not found" plain-text body.
    public static void NotFound(object w, object? r) => Error(w, GoString.FromDotNetString("404 page not found"), 404);
    // http.NotFoundHandler() / RedirectHandler(url, code): the HandlerFunc that replies 404 /
    // redirects. Returned as a native ServeHTTP closure (invoked with w, r).
    public static object NotFoundHandler() =>
        new GoClosure { Id = -1, Native = a => { NotFound(a![0]!, a.Length > 1 ? a[1] : null); return null; } };
    public static object RedirectHandler(GoString url, long code) =>
        new GoClosure { Id = -1, Native = a => { Redirect(a![0]!, a.Length > 1 ? a[1] : null, url, code); return null; } };

    // http.CanonicalHeaderKey(s): canonical MIME header key ("content-type" -> "Content-Type").
    public static GoString CanonicalHeaderKey(GoString s) => Textproto.CanonicalMIMEHeaderKey(s);
    // http.StatusText(code): Go's reason phrase for a status code (e.g. 404 -> "Not Found").
    // The .NET enum names drop the spaces ("NotFound"), so map the common codes explicitly.
    public static GoString StatusText(long code) => GoString.FromDotNetString(code switch
    {
        100 => "Continue", 101 => "Switching Protocols", 102 => "Processing", 103 => "Early Hints",
        200 => "OK", 201 => "Created", 202 => "Accepted", 203 => "Non-Authoritative Information",
        204 => "No Content", 205 => "Reset Content", 206 => "Partial Content",
        300 => "Multiple Choices", 301 => "Moved Permanently", 302 => "Found", 303 => "See Other",
        304 => "Not Modified", 307 => "Temporary Redirect", 308 => "Permanent Redirect",
        400 => "Bad Request", 401 => "Unauthorized", 402 => "Payment Required", 403 => "Forbidden",
        404 => "Not Found", 405 => "Method Not Allowed", 406 => "Not Acceptable",
        407 => "Proxy Authentication Required", 408 => "Request Timeout", 409 => "Conflict",
        410 => "Gone", 411 => "Length Required", 412 => "Precondition Failed",
        413 => "Request Entity Too Large", 414 => "Request URI Too Long",
        415 => "Unsupported Media Type", 416 => "Requested Range Not Satisfiable",
        417 => "Expectation Failed", 418 => "I'm a teapot", 421 => "Misdirected Request",
        422 => "Unprocessable Entity", 423 => "Locked", 424 => "Failed Dependency",
        426 => "Upgrade Required", 428 => "Precondition Required", 429 => "Too Many Requests",
        431 => "Request Header Fields Too Large", 451 => "Unavailable For Legal Reasons",
        500 => "Internal Server Error", 501 => "Not Implemented", 502 => "Bad Gateway",
        503 => "Service Unavailable", 504 => "Gateway Timeout", 505 => "HTTP Version Not Supported",
        506 => "Variant Also Negotiates", 507 => "Insufficient Storage", 508 => "Loop Detected",
        510 => "Not Extended", 511 => "Network Authentication Required",
        _ => "",
    });
    // http.DetectContentType(data): best-effort content sniff.
    public static GoString DetectContentType(GoSlice data) => GoString.FromDotNetString("application/octet-stream");

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
            var listener = new HttpListener();
            // ":8080" -> Go binds every interface and accepts any Host header. HttpListener
            // matches by prefix, so register both loopback names (127.0.0.1 and localhost)
            // — otherwise a request to one is 404'd by the listener before reaching a
            // handler. "host:8080" -> bind that host. (HttpListener mishandles bracketed
            // IPv6 host headers, so an explicit host is used verbatim.)
            if (a.StartsWith(":"))
            {
                listener.Prefixes.Add("http://127.0.0.1" + a + "/");
                listener.Prefixes.Add("http://localhost" + a + "/");
            }
            else
            {
                listener.Prefixes.Add("http://" + a + "/");
            }
            listener.Start();
            while (true)
            {
                var ctx = listener.GetContext();
                var w = new GoRespWriter { Resp = ctx.Response };
                var req = MakeRequest(ctx.Request);
                try { Dispatch(handler, w, req, ctx.Request.Url?.AbsolutePath ?? "/"); }
                catch (System.Exception) { }
                try { Commit(w); } catch { }
                try { ctx.Response.OutputStream.Close(); } catch { }
            }
        }
        catch (System.Exception e) { return new GoError(GoString.FromDotNetString("http: " + e.Message)); }
    }

    // Handlers (http.Handler implementers) are registered at startup by type id; the
    // compiler emits a ServeHTTP adapter closure per implementing type (see
    // collectHandlers). The server loop looks one up from the handler value's type tag.
    private static readonly System.Collections.Generic.Dictionary<long, GoClosure> Handlers = new();
    public static void RegisterHandler(long typeId, GoClosure fn) { lock (Handlers) Handlers[typeId] = fn; }
    private static GoClosure? HandlerFor(long typeId)
    {
        lock (Handlers) return Handlers.TryGetValue(typeId, out var fn) ? fn : null;
    }

    // Dispatch a request to the configured handler: a bare handler func/closure, an
    // http.Handler implementer (e.g. gin's *Engine — a GoPtr carrying its type id,
    // driven through its registered ServeHTTP adapter), an http.Handler interface
    // value, or nil (route through the DefaultServeMux registrations).
    internal static void Dispatch(object? handler, GoRespWriter w, GoRequest req, string path)
    {
        switch (handler)
        {
            case GoClosure c:
                GoRuntime.InvokeArgs(c, w, req);
                return;
            // http.HandlerFunc(f) is a named func type: a GoNamed wrapping the closure,
            // whose ServeHTTP just calls f(w, r) — so invoke the wrapped closure. (Kept
            // before the registered-adapter case so a plain HandlerFunc routes directly.)
            case GoNamed nf when nf.Value is GoClosure fc:
                GoRuntime.InvokeArgs(fc, w, req);
                return;
            case GoInterface gi when gi.Type != null:
                gi.Call("ServeHTTP", w, req);
                return;
            case GoPtr p when p.TypeId != 0 && HandlerFor(p.TypeId) is GoClosure hp:
                GoRuntime.InvokeArgs(hp, handler, w, req);
                return;
            case GoNamed n when HandlerFor(n.TypeId) is GoClosure hn:
                GoRuntime.InvokeArgs(hn, handler, w, req);
                return;
            default:
                var h = Match(path);
                if (h != null) GoRuntime.InvokeArgs(h, w, req);
                return;
        }
    }

    // Fallback for an http.Handler.ServeHTTP interface call whose receiver carries no
    // registered ServeHTTP adapter — a closure-based handler (http.HandlerFunc, and the
    // handlers returned by NotFoundHandler/RedirectHandler): invoke the wrapped closure
    // directly with (w, r). A truly nil handler panics like Go.
    public static void ServeHTTPDyn(object? handler, object? w, object? r)
    {
        switch (handler)
        {
            case GoClosure c: GoRuntime.InvokeArgs(c, w, r); return;
            case GoNamed nf when nf.Value is GoClosure fc: GoRuntime.InvokeArgs(fc, w, r); return;
            case GoInterface gi when gi.Type != null: gi.Call("ServeHTTP", w, r); return;
            default:
                throw new GoPanicException(GoString.FromDotNetString(
                    "runtime error: invalid memory address or nil pointer dereference"));
        }
    }

    private static GoClosure? Match(string path)
    {
        GoClosure? best = null; int bestLen = -1;
        lock (Mux)
            foreach (var (pat, h) in Mux)
                if ((pat == "/" || path == pat || path.StartsWith(pat)) && pat.Length > bestLen) { best = h; bestLen = pat.Length; }
        return best;
    }

    internal static GoRequest MakeRequest(HttpListenerRequest r)
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

    public static GoMap Req_Header(object r)
    {
        var rq = (GoRequest)r;
        return rq.Header ??= GoMaps.Make();
    }

    // (*http.Request).Context(): goclr has no per-request cancellation, so this is a
    // fresh background context.
    public static object Req_Context(object r) => Context.Background();

    // Extra *http.Request fields read/written by x/net/http2 (stored in the field bag).
    public static long Req_ContentLength(object r) => ((GoRequest)r).Extra.GetL("ContentLength");
    public static void Req_SetContentLength(object r, long v) => ((GoRequest)r).Extra.Set("ContentLength", v);
    public static GoMap? Req_Trailer(object r) => (GoMap?)((GoRequest)r).Extra.Get("Trailer");
    public static void Req_SetTrailer(object r, GoMap? v) => ((GoRequest)r).Extra.Set("Trailer", v);
    public static object? Req_TLS(object r) => ((GoRequest)r).Extra.Get("TLS");
    public static void Req_SetTLS(object r, object? v) => ((GoRequest)r).Extra.Set("TLS", v);
    public static object? Req_MultipartForm(object r) => ((GoRequest)r).Extra.Get("MultipartForm");
    public static GoChan? Req_Cancel(object r) => (GoChan?)((GoRequest)r).Extra.Get("Cancel");
    public static GoClosure? Req_GetBody(object r) => (GoClosure?)((GoRequest)r).Extra.Get("GetBody");
    public static bool Req_Close(object r) => ((GoRequest)r).Extra.Get("Close") is bool b && b;
    public static void Req_SetBody(object r, object? v) => ((GoRequest)r).Extra.Set("Body", v);
    public static GoString Req_Proto(object r) => GoString.FromDotNetString("HTTP/1.1");
    public static long Req_ProtoMajor(object r) => 1;
    public static long Req_ProtoMinor(object r) => 1;
    public static GoString Req_RequestURI(object r) => Url.URL_RequestURI(((GoRequest)r).Url);

    // (*Request).PathValue(name): the value captured by a {name} wildcard in the matched
    // ServeMux pattern, or "" if none (Go 1.22 routing).
    public static GoString Req_PathValue(object r, GoString name)
    {
        var pv = ((GoRequest)r).PathValues;
        return GoString.FromDotNetString(pv != null && pv.TryGetValue(name.ToDotNetString(), out var v) ? v : "");
    }
    // (*http.Request).WithContext/Clone: goclr has no request context, so return self.
    public static object Req_WithContext(object r, object? ctx) => r;
    public static object Req_Clone(object r, object? ctx) => r;
    public static GoString Req_UserAgent(object r) => GoString.FromDotNetString(HeaderValue((GoRequest)r, "User-Agent"));
    public static GoString Req_Referer(object r) => GoString.FromDotNetString(HeaderValue((GoRequest)r, "Referer"));

    // First value of a request header (case-insensitive), or "" if absent.
    private static string HeaderValue(GoRequest r, string key)
    {
        if (r.Header?.Data == null) return "";
        foreach (var kv in r.Header.Data)
            if (kv.Key is GoString gk && string.Equals(gk.ToDotNetString(), key, System.StringComparison.OrdinalIgnoreCase))
                return kv.Value is GoSlice s && s.Len > 0 && s.Data![s.Off] is GoString gv ? gv.ToDotNetString() : "";
        return "";
    }

    // Zero value for an http.Cookie composite literal (&http.Cookie{...}); field
    // initializers then assign through Cookie_SetName/Value/... on this instance.
    public static object NewCookie() => new GoCookie();

    // http.NewRequest(method, url, body) (*Request, error) — a client request.
    public static object?[] NewRequest(GoString method, GoString url, object? body)
    {
        try
        {
            var u = new GoUrl();
            string t = url.ToDotNetString();
            if (t.StartsWith("http://") || t.StartsWith("https://"))
            {
                var p = new System.Uri(t);
                u.Scheme = p.Scheme; u.Host = p.Authority; u.Path = p.AbsolutePath; u.RawQuery = p.Query.TrimStart('?');
            }
            else u.Path = t;
            byte[] raw = body == null ? System.Array.Empty<byte>() : Readers.Drain(body);
            return new object?[] { new GoRequest { Method = method.ToDotNetString(), Url = u, Body = new GoReader { Data = raw }, RawBody = raw, Host = u.Host, Header = GoMaps.Make() }, null };
        }
        catch (System.Exception e) { return new object?[] { null, new GoError(GoString.FromDotNetString("http: " + e.Message)) }; }
    }
    public static object?[] NewRequestWithContext(object? ctx, GoString method, GoString url, object? body) => NewRequest(method, url, body);

    // http.Client: zero value, DefaultClient, and the request methods over the pooled
    // static HttpClient (the per-client Timeout/Transport are accepted but not applied).
    public static object NewClient() => new GoHttpClient();
    public static object DefaultClient() => new GoHttpClient();
    public static void Client_SetTimeout(object c, long t) => ((GoHttpClient)c).TimeoutNanos = t;
    public static void Client_SetTransport(object c, object? v) { }
    public static void Client_SetCheckRedirect(object c, object? v) { }
    public static void Client_SetJar(object c, object? v) { }

    private static string ReqUrl(GoRequest r) =>
        (r.Url.Scheme.Length > 0 ? r.Url.Scheme + "://" + r.Url.Host : "") + (r.Url.Path.Length > 0 ? r.Url.Path : "/") +
        (r.Url.RawQuery.Length > 0 ? "?" + r.Url.RawQuery : "");

    public static object?[] Client_Do(object c, object req)
    {
        try
        {
            var r = (GoRequest)req;
            var msg = new HttpRequestMessage(new HttpMethod(r.Method.Length > 0 ? r.Method : "GET"), ReqUrl(r));
            if (r.RawBody.Length > 0) msg.Content = new ByteArrayContent(r.RawBody);
            if (r.Header is GoMap h && h.Data != null)
                foreach (var (k, v) in h.Data)
                    if (k is GoString gk && v is GoSlice gs && gs.Len > 0 && gs.Data![gs.Off] is GoString gv)
                        try { msg.Headers.TryAddWithoutValidation(gk.ToDotNetString(), gv.ToDotNetString()); } catch { }
            return new object?[] { Make(Client.SendAsync(msg).GetAwaiter().GetResult()), null };
        }
        catch (System.Exception e) { return new object?[] { null, new GoError(GoString.FromDotNetString("http: " + e.Message)) }; }
    }
    public static object?[] Client_Get(object c, GoString url) => Get(url);
    public static object?[] Client_Post(object c, GoString url, GoString ct, object? body) => Post(url, ct, body);
    public static object?[] Client_Head(object c, GoString url)
    {
        try { var m = new HttpRequestMessage(HttpMethod.Head, url.ToDotNetString()); return new object?[] { Make(Client.SendAsync(m).GetAwaiter().GetResult()), null }; }
        catch (System.Exception e) { return new object?[] { null, new GoError(GoString.FromDotNetString("Head " + url.ToDotNetString() + ": " + e.Message)) }; }
    }

    // http.ParseTime(text) (time.Time, error) — the three HTTP date formats.
    public static object?[] ParseTime(GoString text)
    {
        string s = text.ToDotNetString();
        var epoch = new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);
        foreach (var fmt in new[] { "ddd, dd MMM yyyy HH:mm:ss 'GMT'", "dddd, dd-MMM-yy HH:mm:ss 'GMT'", "ddd MMM d HH:mm:ss yyyy", "ddd MMM  d HH:mm:ss yyyy" })
            if (System.DateTime.TryParseExact(s, fmt, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var dt))
                return new object?[] { new GoTime { N = (dt.ToUniversalTime() - epoch).Ticks * 100 }, null };
        return new object?[] { new GoTime { IsZero = true }, new GoError(GoString.FromDotNetString("http: invalid time")) };
    }

    // net/http.readCookies(filter): parse the request's Cookie header into cookies whose
    // name is valid and value parses, optionally keeping only the named one (filter). Each
    // part is trimmed, a quoted value is unquoted, and invalid name/value parts are skipped.
    private static System.Collections.Generic.List<object?> ReadCookies(GoRequest r, string filter)
    {
        var list = new System.Collections.Generic.List<object?>();
        string line = HeaderValue(r, "Cookie").Trim(' ', '\t');
        foreach (var raw in line.Split(';'))
        {
            string part = raw.Trim(' ', '\t');
            if (part.Length == 0) continue;
            int eq = part.IndexOf('=');
            string name = eq < 0 ? part : part.Substring(0, eq);
            string val = eq < 0 ? "" : part.Substring(eq + 1);
            name = name.Trim(' ', '\t');
            if (!IsCookieNameValid(name)) continue;
            if (filter.Length != 0 && filter != name) continue;
            var (v, ok) = ParseCookieValue(val, true);
            if (!ok) continue;
            list.Add(new GoCookie { Name = name, Value = v });
        }
        return list;
    }

    // (*http.Request).Cookie(name) (*http.Cookie, error): the first cookie with that name,
    // or ErrNoCookie. An empty name yields ErrNoCookie immediately, like Go.
    public static object?[] Req_Cookie(object r, GoString name)
    {
        string want = name.ToDotNetString();
        if (want.Length == 0) return new object?[] { null, ErrNoCookieSentinel };
        var found = ReadCookies((GoRequest)r, want);
        if (found.Count > 0) return new object?[] { found[0], null };
        return new object?[] { null, ErrNoCookieSentinel };
    }
    public static GoSlice Req_Cookies(object r)
    {
        var list = ReadCookies((GoRequest)r, "");
        return new GoSlice { Data = list.ToArray(), Off = 0, Len = list.Count, Cap = list.Count };
    }

    // (*http.Request).AddCookie(c): append "name=value" to the Cookie header (name's
    // newlines become '-', value sanitized), joining an existing header with "; ".
    public static void Req_AddCookie(object r, object cookie)
    {
        var ck = (GoCookie)cookie;
        string s = SanitizeCookieName(ck.Name) + "=" + SanitizeCookieValue(ck.Value);
        var h = Req_Header(r);
        string existing = Header_Get(h, GoString.FromDotNetString("Cookie")).ToDotNetString();
        string val = existing.Length != 0 ? existing + "; " + s : s;
        Header_Set(h, GoString.FromDotNetString("Cookie"), GoString.FromDotNetString(val));
    }

    // net/http.sanitizeCookieName: newlines in a cookie name become '-'.
    private static string SanitizeCookieName(string n) => n.Replace('\n', '-').Replace('\r', '-');

    // http.ServeFile(w, r, name): write a file's bytes to the response with a sniffed
    // content type (used by gin's StaticFile).
    public static void ServeFile(object w, object r, GoString name)
    {
        string path = name.ToDotNetString();
        try
        {
            byte[] data = System.IO.File.ReadAllBytes(path);
            var hdr = RW_Header(w);
            Header_Set(hdr, GoString.FromDotNetString("Content-Type"), GoString.FromDotNetString(ContentTypeByExt(path)));
            RW_WriteHeader(w, 200);
            RW_Write(w, BytesToSlice(data));
        }
        catch (System.Exception) { RW_WriteHeader(w, 404); }
    }
    // http.ServeContent(w, r, name, modtime, content io.ReadSeeker): write the content's
    // bytes with a Content-Type derived from the name (echo's c.Attachment / fs serving).
    public static void ServeContent(object w, object r, GoString name, object modtime, object? content)
    {
        byte[] data = Readers.Drain(content);
        var hdr = RW_Header(w);
        if (Header_Get((GoMap)hdr, GoString.FromDotNetString("Content-Type")).ToDotNetString() == "")
            Header_Set(hdr, GoString.FromDotNetString("Content-Type"), GoString.FromDotNetString(ContentTypeByExt(name.ToDotNetString())));
        RW_WriteHeader(w, 200);
        RW_Write(w, BytesToSlice(data));
    }
    private static GoSlice BytesToSlice(byte[] b)
    {
        var d = new object?[b.Length];
        for (int i = 0; i < b.Length; i++) d[i] = (int)b[i];
        return new GoSlice { Data = d, Off = 0, Len = b.Length, Cap = b.Length };
    }
    private static string ContentTypeByExt(string path)
    {
        string e = System.IO.Path.GetExtension(path).ToLowerInvariant();
        return e switch
        {
            ".html" or ".htm" => "text/html; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".js" => "text/javascript; charset=utf-8",
            ".json" => "application/json",
            ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".gif" => "image/gif",
            ".svg" => "image/svg+xml", ".txt" => "text/plain; charset=utf-8",
            _ => "application/octet-stream",
        };
    }

    // http.FileServer(fs): the returned handler's ServeHTTP is a net/http internal type
    // with no lowered body under goclr, so the handler value is opaque. gin's Static*
    // routes resolve files via its own fs.Open.
    public static object FileServer(object? fs) => new GoFileServer { Fs = fs };

    // http.StripPrefix(prefix, h): a Handler that removes prefix from the request URL path
    // and dispatches to h with the rewritten request, or replies 404 if the path lacks the
    // prefix. Mirrors net/http.StripPrefix (the RawPath leg is moot — the shim URL has none).
    // A FileServer handler keeps the legacy behaviour (records the prefix on the opaque value)
    // so gin's static routes still resolve.
    public static object StripPrefix(GoString prefix, object? h)
    {
        if (h is GoFileServer fsv) { fsv.StripPrefix = prefix.ToDotNetString(); return fsv; }
        string pfx = prefix.ToDotNetString();
        if (pfx.Length == 0) return h!;
        return new GoClosure { Id = -1, Native = a =>
        {
            var w = a![0];
            var r = a.Length > 1 ? a[1] : null;
            var req = r as GoRequest;
            string path = req?.Url.Path ?? "";
            string p = path.StartsWith(pfx, System.StringComparison.Ordinal) ? path.Substring(pfx.Length) : path;
            if (p.Length < path.Length && req != null)
            {
                var u = req.Url;
                var u2 = new GoUrl { Scheme = u.Scheme, Host = u.Host, Path = p, RawQuery = u.RawQuery, Fragment = u.Fragment, Opaque = u.Opaque, User = u.User };
                var r2 = new GoRequest { Method = req.Method, Url = u2, Body = req.Body, Host = req.Host, RemoteAddr = req.RemoteAddr, Form = req.Form, PostForm = req.PostForm, Header = req.Header, RawBody = req.RawBody };
                ServeHTTPDyn(h, w, r2);
            }
            else NotFound(w!, r);
            return null;
        } };
    }

    // http.Serve(l, handler): serve over a net.Listener (best-effort, not used by goclr's
    // HttpListener path).
    public static object? Serve(object? l, object? handler) => null;

    // http.ListenAndServeTLS(addr, certFile, keyFile, handler): TLS isn't terminated by
    // the HttpListener backend; fall back to plaintext serving on the address.
    public static object? ListenAndServeTLS(GoString addr, GoString certFile, GoString keyFile, object? handler) =>
        ListenAndServe(addr, handler);

    // http.SetCookie(w, cookie): add a Set-Cookie header to the writer.
    public static void SetCookie(object w, object c)
    {
        var s = Cookie_String(c);
        if (s.ToDotNetString().Length == 0) return;
        Header_Add(RW_Header(w), GoString.FromDotNetString("Set-Cookie"), s);
    }

    // http.ParseHTTPVersion(vers) (major, minor int, ok bool).
    public static object?[] ParseHTTPVersion(GoString versS)
    {
        string vers = versS.ToDotNetString();
        if (vers == "HTTP/1.1") return new object?[] { 1L, 1L, true };
        if (vers == "HTTP/1.0") return new object?[] { 1L, 0L, true };
        if (!vers.StartsWith("HTTP/", System.StringComparison.Ordinal) || vers.Length != 8 || vers[6] != '.'
            || !char.IsDigit(vers[5]) || !char.IsDigit(vers[7]))
            return new object?[] { 0L, 0L, false };
        return new object?[] { (long)(vers[5] - '0'), (long)(vers[7] - '0'), true };
    }

    private static readonly GoError BlankCookieErr = new(GoString.FromDotNetString("http: blank cookie"));
    private static readonly GoError NoEqualCookieErr = new(GoString.FromDotNetString("http: '=' not found in cookie"));
    private static readonly GoError InvalidCookieNameErr = new(GoString.FromDotNetString("http: invalid cookie name"));
    private static readonly GoError InvalidCookieValueErr = new(GoString.FromDotNetString("http: invalid cookie value"));
    private static bool IsCookieNameValid(string n)
    {
        if (n.Length == 0) return false;
        foreach (char c in n)
            if (c <= ' ' || c >= 0x7f || "()<>@,;:\\\"/[]?={}".IndexOf(c) >= 0) return false; // RFC 2616 token
        return true;
    }
    // Go's parseCookieValue: optionally strip surrounding quotes, then require every byte be
    // 0x20..0x7e excluding " ; and \. Empty is valid.
    private static (string val, bool ok) ParseCookieValue(string raw, bool allowQuote)
    {
        if (allowQuote && raw.Length > 1 && raw[0] == '"' && raw[^1] == '"') raw = raw.Substring(1, raw.Length - 2);
        foreach (char c in raw)
            if (c < 0x20 || c >= 0x7f || c == '"' || c == ';' || c == '\\') return ("", false);
        return (raw, true);
    }

    // http.ParseCookie(line) ([]*Cookie, error): parse a Cookie header value (strict — any
    // invalid name/value fails the whole parse, matching Go).
    public static object?[] ParseCookie(GoString lineS)
    {
        string line = lineS.ToDotNetString().Trim(' ', '\t');
        var parts = line.Split(';');
        if (parts.Length == 1 && parts[0] == "") return new object?[] { default(GoSlice), BlankCookieErr };
        var list = new System.Collections.Generic.List<object?>();
        foreach (var raw in parts)
        {
            string s = raw.Trim(' ', '\t');
            int eq = s.IndexOf('=');
            if (eq < 0) return new object?[] { default(GoSlice), NoEqualCookieErr };
            string name = s.Substring(0, eq).Trim(' ', '\t');
            string value = s.Substring(eq + 1);
            if (!IsCookieNameValid(name)) return new object?[] { default(GoSlice), InvalidCookieNameErr };
            var (val, ok) = ParseCookieValue(value, true);
            if (!ok) return new object?[] { default(GoSlice), InvalidCookieValueErr };
            list.Add(new GoCookie { Name = name, Value = val });
        }
        return new object?[] { new GoSlice { Data = list.ToArray(), Off = 0, Len = list.Count, Cap = list.Count }, null };
    }

    // http.ParseSetCookie(line) (*Cookie, error): parse a single Set-Cookie line.
    public static object?[] ParseSetCookie(GoString lineS)
    {
        string line = lineS.ToDotNetString().Trim(' ', '\t');
        var parts = line.Split(';');
        if (parts.Length == 1 && parts[0] == "") return new object?[] { null, BlankCookieErr };
        string first = parts[0].Trim(' ', '\t');
        int eq = first.IndexOf('=');
        if (eq < 0) return new object?[] { null, NoEqualCookieErr };
        string name = first.Substring(0, eq);
        if (!IsCookieNameValid(name)) return new object?[] { null, InvalidCookieNameErr };
        var (cval, cok) = ParseCookieValue(first.Substring(eq + 1), true);
        if (!cok) return new object?[] { null, InvalidCookieValueErr };
        var ck = new GoCookie { Name = name, Value = cval };
        for (int i = 1; i < parts.Length; i++)
        {
            string attr = parts[i].Trim(' ', '\t');
            if (attr.Length == 0) continue;
            int e = attr.IndexOf('=');
            string an = e < 0 ? attr : attr.Substring(0, e);
            string av = e < 0 ? "" : attr.Substring(e + 1);
            switch (an.ToLowerInvariant())
            {
                case "path": ck.Path = av; break;
                case "domain": ck.Domain = av; break;
                case "max-age": if (long.TryParse(av, out var ma)) ck.MaxAge = ma; break;
                case "secure": ck.Secure = true; break;
                case "httponly": ck.HttpOnly = true; break;
            }
        }
        return new object?[] { ck, null };
    }

    // http.Cookie field getters/setters + String.
    public static GoString Cookie_Name(object c) => GoString.FromDotNetString(((GoCookie)c).Name);
    public static GoString Cookie_Value(object c) => GoString.FromDotNetString(((GoCookie)c).Value);
    public static GoString Cookie_Path(object c) => GoString.FromDotNetString(((GoCookie)c).Path);
    public static GoString Cookie_Domain(object c) => GoString.FromDotNetString(((GoCookie)c).Domain);
    public static long Cookie_MaxAge(object c) => ((GoCookie)c).MaxAge;
    public static bool Cookie_Secure(object c) => ((GoCookie)c).Secure;
    public static bool Cookie_HttpOnly(object c) => ((GoCookie)c).HttpOnly;
    public static long Cookie_SameSite(object c) => ((GoCookie)c).SameSite;
    public static void Cookie_SetName(object c, GoString v) => ((GoCookie)c).Name = v.ToDotNetString();
    public static void Cookie_SetValue(object c, GoString v) => ((GoCookie)c).Value = v.ToDotNetString();
    public static void Cookie_SetPath(object c, GoString v) => ((GoCookie)c).Path = v.ToDotNetString();
    public static void Cookie_SetDomain(object c, GoString v) => ((GoCookie)c).Domain = v.ToDotNetString();
    public static void Cookie_SetMaxAge(object c, long v) => ((GoCookie)c).MaxAge = v;
    public static void Cookie_SetSecure(object c, bool v) => ((GoCookie)c).Secure = v;
    public static void Cookie_SetHttpOnly(object c, bool v) => ((GoCookie)c).HttpOnly = v;
    public static void Cookie_SetSameSite(object c, long v) => ((GoCookie)c).SameSite = v;
    // (*http.Cookie).String(): the Set-Cookie serialization. Mirrors net/http: an invalid
    // name yields "", the value/path are sanitized, a leading '.' is stripped off Domain,
    // MaxAge<0 emits "Max-Age=0", and SameSite is rendered. Expires is deferred (needs
    // time.Time formatting) — the field is not modeled, so it is never emitted.
    public static GoString Cookie_String(object c)
    {
        var ck = (GoCookie)c;
        if (!IsCookieNameValid(ck.Name)) return GoString.FromDotNetString("");
        var sb = new System.Text.StringBuilder();
        sb.Append(ck.Name).Append('=').Append(SanitizeCookieValue(ck.Value));
        if (ck.Path.Length > 0) sb.Append("; Path=").Append(SanitizeCookiePath(ck.Path));
        if (ck.Domain.Length > 0)
        {
            string d = ck.Domain[0] == '.' ? ck.Domain.Substring(1) : ck.Domain;
            sb.Append("; Domain=").Append(d);
        }
        if (ck.MaxAge > 0) sb.Append("; Max-Age=").Append(ck.MaxAge);
        else if (ck.MaxAge < 0) sb.Append("; Max-Age=0");
        if (ck.HttpOnly) sb.Append("; HttpOnly");
        if (ck.Secure) sb.Append("; Secure");
        switch (ck.SameSite)
        {
            case 2: sb.Append("; SameSite=Lax"); break;    // SameSiteLaxMode
            case 3: sb.Append("; SameSite=Strict"); break; // SameSiteStrictMode
            case 4: sb.Append("; SameSite=None"); break;   // SameSiteNoneMode
            // 0 (unset) and 1 (SameSiteDefaultMode) emit nothing.
        }
        return GoString.FromDotNetString(sb.ToString());
    }

    // net/http.sanitizeCookieValue: drop bytes outside [0x20,0x7f) or in {" ; \\}, then
    // wrap in double quotes if the result contains a space or comma.
    private static string SanitizeCookieValue(string v)
    {
        var sb = new System.Text.StringBuilder(v.Length);
        foreach (char ch in v)
            if (ch >= 0x20 && ch < 0x7f && ch != '"' && ch != ';' && ch != '\\') sb.Append(ch);
        string s = sb.ToString();
        if (s.Length != 0 && (s.IndexOf(' ') >= 0 || s.IndexOf(',') >= 0)) return "\"" + s + "\"";
        return s;
    }

    // net/http.sanitizeCookiePath: drop bytes outside [0x20,0x7f) or equal to ';'.
    private static string SanitizeCookiePath(string v)
    {
        var sb = new System.Text.StringBuilder(v.Length);
        foreach (char ch in v)
            if (ch >= 0x20 && ch < 0x7f && ch != ';') sb.Append(ch);
        return sb.ToString();
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
    // (*http.Request).ParseMultipartForm(maxMemory): parse a multipart/form-data body
    // into r.MultipartForm (and merge values into r.Form / r.PostForm).
    public static object? Req_ParseMultipartForm(object ro, long maxMemory)
    {
        var r = (GoRequest)ro;
        if (r.Extra.Get("MultipartForm") != null) return null;
        string ct = HeaderValue(r, "Content-Type");
        int b = ct.IndexOf("boundary=", System.StringComparison.OrdinalIgnoreCase);
        if (!ct.Contains("multipart/form-data", System.StringComparison.OrdinalIgnoreCase) || b < 0)
            return ErrNotMultipartSentinel;
        string boundary = ct.Substring(b + "boundary=".Length).Trim().Trim('"');
        int semi = boundary.IndexOf(';');
        if (semi >= 0) boundary = boundary.Substring(0, semi);
        var form = Multipart.ParseForm(r.RawBody, boundary);
        r.Extra.Set("MultipartForm", form);
        // Merge text values into Form/PostForm.
        r.Form ??= GoMaps.Make();
        r.PostForm ??= GoMaps.Make();
        if (form.Value?.Data != null)
            foreach (var kv in form.Value.Data) { r.Form.Data![kv.Key] = kv.Value; r.PostForm.Data![kv.Key] = kv.Value; }
        return null;
    }

    // (*http.Request).MultipartReader(): streaming multipart isn't supported (the body
    // is buffered); callers fall back to ParseMultipartForm.
    public static object?[] Req_MultipartReader(object ro) =>
        new object?[] { null, new GoError("http: streaming multipart not supported; use ParseMultipartForm") };

    // (*http.Request).FormFile(name) (multipart.File, *multipart.FileHeader, error).
    public static object?[] Req_FormFile(object ro, GoString name)
    {
        var r = (GoRequest)ro;
        if (r.Extra.Get("MultipartForm") == null)
        {
            var err = Req_ParseMultipartForm(ro, 32 << 20);
            if (err != null) return new object?[] { null, null, err };
        }
        if (r.Extra.Get("MultipartForm") is GoMultipartForm form && form.File?.Data != null
            && form.File.Data.TryGetValue(name, out var v) && v is GoSlice s && s.Len > 0
            && s.Data![s.Off] is GoFileHeader fh)
        {
            var opened = Multipart.FH_Open(fh);
            return new object?[] { opened[0], fh, null };
        }
        return new object?[] { null, null, new GoError("http: no such file") };
    }
    public static GoMap Req_Form(object r)
    {
        var rq = (GoRequest)r;
        if (rq.Form == null) Req_ParseForm(r);
        return rq.Form!;
    }
    public static GoMap Req_PostForm(object r)
    {
        var rq = (GoRequest)r;
        if (rq.PostForm == null) Req_ParseForm(r);
        return rq.PostForm!;
    }

    // (*http.Request).FormValue(key): first value for key from Form (query + body).
    public static GoString Req_FormValue(object r, GoString key)
    {
        var rq = (GoRequest)r;
        if (rq.Form == null) Req_ParseForm(r);
        return FirstValue(rq.Form!, key);
    }
    // (*http.Request).PostFormValue(key): first value for key from PostForm (body only).
    public static GoString Req_PostFormValue(object r, GoString key)
    {
        var rq = (GoRequest)r;
        if (rq.PostForm == null) Req_ParseForm(r);
        return FirstValue(rq.PostForm!, key);
    }
    private static GoString FirstValue(GoMap m, GoString key)
    {
        if (m.Data != null && m.Data.TryGetValue(key, out var v) && v is GoSlice s && s.Len > 0
            && s.Data![s.Off] is GoString gs)
            return gs;
        return GoString.FromDotNetString("");
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

    // Resolve an http.ResponseWriter value to the backing GoRespWriter. Frameworks
    // wrap the writer (gin's responseWriter embeds http.ResponseWriter); since
    // http.ResponseWriter is dispatched statically to these shims, navigate the
    // wrapper's fields to the embedded GoRespWriter. This drives the real response
    // for both promoted (Header) and overridden (Write/WriteHeader) wrapper methods.
    private static GoRespWriter AsRW(object? w) => FindRW(w, 0) ?? throw new System.InvalidCastException("not an http.ResponseWriter");
    private static GoRespWriter? FindRW(object? w, int depth)
    {
        switch (w)
        {
            case null: return null;
            case GoRespWriter rw: return rw;
            case GoPtr p: return depth < 8 ? FindRW(GoPtrs.Get(p), depth + 1) : null;
        }
        if (depth >= 8) return null;
        // A wrapper struct: scan its fields for the embedded ResponseWriter (direct, or
        // nested through another pointer/struct field).
        foreach (var f in w.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            var fv = f.GetValue(w);
            if (fv is GoRespWriter rw) return rw;
            if (fv is GoPtr && FindRW(fv, depth + 1) is GoRespWriter found) return found;
        }
        return null;
    }

    // http.ResponseWriter methods.
    public static object?[] RW_Write(object w, GoSlice p)
    {
        var rw = AsRW(w);
        var buf = new byte[p.Len];
        for (int i = 0; i < p.Len; i++) buf[i] = (byte)System.Convert.ToInt64(p.Data![p.Off + i]);
        rw.Body.Write(buf, 0, buf.Length);
        return new object?[] { (long)p.Len, null };
    }
    // WriteHeader records the status; nothing is committed until the handler returns
    // (Commit), so a later header set still takes effect.
    public static void RW_WriteHeader(object w, long code) { var rw = AsRW(w); if (!rw.WroteHeader) { rw.Status = (int)code; rw.WroteHeader = true; } }

    // Commit flushes the recorded status, headers, and buffered body to the listener.
    // Called once after the handler returns.
    internal static void Commit(GoRespWriter rw)
    {
        try { rw.Resp.StatusCode = rw.Status; } catch { }
        FlushHeaders(rw);
        try { var b = rw.Body.GetBuffer(); rw.Resp.OutputStream.Write(b, 0, (int)rw.Body.Length); } catch { }
    }

    // http.Redirect(w, r, url, code): set Location and write the status code.
    public static void Redirect(object w, object? r, GoString url, long code)
    {
        // Mirror net/http.Redirect: set Location (non-ASCII bytes percent-escaped), add a
        // text/html body for GET so the headers flow through the ResponseWriter's own header
        // map — which makes it work for an httptest recorder (no backing HttpListenerResponse).
        string method = (r as GoRequest)?.Method ?? "GET";
        var h = RW_Header(w);
        bool hadCT = Header_Get(h, GoString.FromDotNetString("Content-Type")).ToDotNetString().Length != 0;
        Header_Set(h, GoString.FromDotNetString("Location"), HexEscapeNonASCII(url.ToDotNetString()));
        if (!hadCT && (method == "GET" || method == "HEAD"))
            Header_Set(h, GoString.FromDotNetString("Content-Type"), GoString.FromDotNetString("text/html; charset=utf-8"));
        RW_WriteHeader(w, code);
        if (!hadCT && method == "GET")
        {
            string body = "<a href=\"" + Html.EscapeString(url).ToDotNetString() + "\">" + StatusText(code).ToDotNetString() + "</a>.\n";
            Fmt.WriteTo(w, body + "\n"); // fmt.Fprintln adds the trailing newline
        }
    }

    // net/http.hexEscapeNonASCII: percent-escape every byte >= 0x80 (UTF-8 continuation/
    // lead bytes) as %XX uppercase, leaving ASCII untouched.
    private static GoString HexEscapeNonASCII(string s)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(s);
        bool any = false;
        foreach (var b in bytes) if (b >= 0x80) { any = true; break; }
        if (!any) return GoString.FromDotNetString(s);
        var sb = new System.Text.StringBuilder();
        foreach (var b in bytes)
        {
            if (b >= 0x80) sb.Append('%').Append("0123456789ABCDEF"[b >> 4]).Append("0123456789ABCDEF"[b & 0xF]);
            else sb.Append((char)b);
        }
        return GoString.FromDotNetString(sb.ToString());
    }

    // http.ResponseWriter.Header() http.Header: a live map[string][]string; entries
    // set on it are flushed to the response on the first Write/WriteHeader.
    public static GoMap RW_Header(object w)
    {
        var rw = AsRW(w);
        return rw.Headers ??= GoMaps.Make();
    }
    private static void FlushHeaders(GoRespWriter rw)
    {
        if (rw.Headers?.Data == null) return;
        foreach (var kv in rw.Headers.Data)
        {
            string k = ((GoString)kv.Key).ToDotNetString();
            if (kv.Value is GoSlice vs && vs.Data != null)
                for (int i = 0; i < vs.Len; i++)
                {
                    string v = ((GoString)vs.Data[vs.Off + i]!).ToDotNetString();
                    // Content-Type is a reserved header on HttpListener (Headers.Add throws);
                    // it must be set through the dedicated property.
                    if (string.Equals(k, "Content-Type", System.StringComparison.OrdinalIgnoreCase)) { try { rw.Resp.ContentType = v; } catch { } continue; }
                    try { rw.Resp.Headers.Add(k, v); } catch { /* restricted header (Content-Length, etc.) */ }
                }
        }
    }

    // http.Header methods (receiver is the map[string][]string).
    private static GoString Canon(GoString k) => Textproto.CanonicalMIMEHeaderKey(k);
    public static GoString Header_Get(GoMap h, GoString key)
    {
        var m = (GoMap)h;
        if (m.Data != null && m.Data.TryGetValue(Canon(key), out var v) && v is GoSlice s && s.Data != null && s.Len > 0)
            return (GoString)s.Data[s.Off]!;
        return GoString.FromDotNetString("");
    }
    public static void Header_Set(GoMap h, GoString key, GoString val)
    {
        var m = (GoMap)h;
        m.Data![Canon(key)] = new GoSlice { Data = new object?[] { val }, Off = 0, Len = 1, Cap = 1 };
    }
    public static void Header_Add(GoMap h, GoString key, GoString val)
    {
        var m = (GoMap)h;
        var ck = Canon(key);
        GoSlice s = m.Data!.TryGetValue(ck, out var ex) && ex is GoSlice gs ? gs : new GoSlice { Data = new object?[0], Off = 0, Len = 0, Cap = 0 };
        m.Data[ck] = GoSlices.AppendOne(s, val);
    }
    public static void Header_Del(GoMap h, GoString key) => h.Data?.Remove(Canon(key));
    // http.Header.Values(key) []string: every value for the canonical key (Go returns
    // all of them, not just the first like Get) — a nil slice when the key is absent.
    public static GoSlice Header_Values(GoMap h, GoString key)
    {
        var m = (GoMap)h;
        if (m.Data != null && m.Data.TryGetValue(Canon(key), out var v) && v is GoSlice s) return s;
        return new GoSlice { Data = null, Off = 0, Len = 0, Cap = 0 };
    }
    public static GoMap Header_Clone(GoMap h)
    {
        var src = (GoMap)h;
        if (src.Data == null) return new GoMap { Data = null };
        var m = GoMaps.Make();
        // Go deep-copies the value slices, so a mutation of the clone (Add/Set) cannot
        // reach back into the original. Copy each []string into its own backing array.
        foreach (var kv in src.Data)
        {
            if (kv.Value is GoSlice s && s.Data != null)
            {
                var d = new object?[s.Len];
                for (int i = 0; i < s.Len; i++) d[i] = s.Data[s.Off + i];
                m.Data![kv.Key] = new GoSlice { Data = d, Off = 0, Len = s.Len, Cap = s.Len };
            }
            else m.Data![kv.Key] = kv.Value;
        }
        return m;
    }
    // http.Header.Write(w): write the header in sorted key order as "Key: value\r\n",
    // one line per value (newlines in a value become spaces, then trimmed) — matching
    // net/http's writeSubset, which sorts keys and emits every value.
    public static object? Header_Write(GoMap h, object? w)
    {
        var m = (GoMap)h;
        if (m.Data == null) return null;
        var pairs = new System.Collections.Generic.List<(string Key, GoSlice Vals)>();
        foreach (var kv in m.Data)
            if (kv.Value is GoSlice s && s.Data != null)
                pairs.Add((((GoString)kv.Key).ToDotNetString(), s));
        pairs.Sort((a, b) => System.String.CompareOrdinal(a.Key, b.Key));
        var sb = new System.Text.StringBuilder();
        foreach (var (key, vals) in pairs)
            for (int i = 0; i < vals.Len; i++)
            {
                string v = ((GoString)vals.Data![vals.Off + i]!).ToDotNetString();
                v = v.Replace('\n', ' ').Replace('\r', ' '); // headerNewlineToSpace
                v = v.Trim(' ', '\t');                        // textproto.TrimString
                sb.Append(key).Append(": ").Append(v).Append("\r\n");
            }
        Fmt.WriteTo(w, sb.ToString());
        return null;
    }

    // *http.Request field getters.
    public static GoString Req_Method(object r) => GoString.FromDotNetString(((GoRequest)r).Method);
    public static object Req_URL(object r) => ((GoRequest)r).Url;
    public static object Req_Body(object r) => ((GoRequest)r).Body;
    public static GoString Req_Host(object r) => GoString.FromDotNetString(((GoRequest)r).Host);
    public static GoString Req_RemoteAddr(object r) => GoString.FromDotNetString(((GoRequest)r).RemoteAddr);
}

/// <summary>net/http and crypto/tls value types that x/net/http2 constructs and reads
/// but that never execute under goclr's shimmed (HttpListener) server — modeled as
/// plain holders so http2 compiles. Callback/timeout/config fields default to nil/0.</summary>
// These net/http and crypto/tls value types are constructed and mutated by
// x/net/http2 but never execute under the HttpListener server. Each stores its
// assignable fields generically so set-then-get is consistent; reads of unset fields
// return the type's zero value.
public sealed class GoFieldBag
{
    private readonly System.Collections.Generic.Dictionary<string, object?> _f = new();
    public object? Get(string k) => _f.TryGetValue(k, out var v) ? v : null;
    public void Set(string k, object? v) => _f[k] = v;
    public long GetL(string k) => _f.TryGetValue(k, out var v) && v != null ? System.Convert.ToInt64(v) : 0;
    public bool GetB(string k) => _f.TryGetValue(k, out var v) && v is bool b && b;
}
public sealed class GoHttpServer { public readonly GoFieldBag F = new(); }
public sealed class GoHttpTransport { public readonly GoFieldBag F = new(); }
public sealed class GoTlsConfig { public readonly GoFieldBag F = new(); }
[GoShim("crypto/tls.Certificate")]
public sealed class GoTlsCert { public readonly GoFieldBag F = new(); }
public sealed class GoHTTP2Config { public readonly GoFieldBag F = new(); }
public sealed class GoHttpProtocols { public bool H1, H2, UH2; }
public sealed class GoTlsConn { }
public sealed class GoTlsConnState { public readonly GoFieldBag F = new(); }
public sealed class GoServeMux
{
    // Registered routes: pattern -> handler (a GoClosure from HandleFunc, or an
    // http.Handler value from Handle). Longest matching pattern wins, like Go.
    public readonly System.Collections.Generic.List<(string pat, object? h)> Routes = new();
}

public static class HttpTypes
{
    // --- crypto/tls pure data tables (byte-exact vs Go) ---
    private static readonly System.Collections.Generic.Dictionary<int, string> CipherNames = new()
    {
        { 0x0005, "TLS_RSA_WITH_RC4_128_SHA" }, { 0x000a, "TLS_RSA_WITH_3DES_EDE_CBC_SHA" },
        { 0x002f, "TLS_RSA_WITH_AES_128_CBC_SHA" }, { 0x0035, "TLS_RSA_WITH_AES_256_CBC_SHA" },
        { 0x003c, "TLS_RSA_WITH_AES_128_CBC_SHA256" }, { 0x009c, "TLS_RSA_WITH_AES_128_GCM_SHA256" },
        { 0x009d, "TLS_RSA_WITH_AES_256_GCM_SHA384" }, { 0xc007, "TLS_ECDHE_ECDSA_WITH_RC4_128_SHA" },
        { 0xc009, "TLS_ECDHE_ECDSA_WITH_AES_128_CBC_SHA" }, { 0xc00a, "TLS_ECDHE_ECDSA_WITH_AES_256_CBC_SHA" },
        { 0xc011, "TLS_ECDHE_RSA_WITH_RC4_128_SHA" }, { 0xc012, "TLS_ECDHE_RSA_WITH_3DES_EDE_CBC_SHA" },
        { 0xc013, "TLS_ECDHE_RSA_WITH_AES_128_CBC_SHA" }, { 0xc014, "TLS_ECDHE_RSA_WITH_AES_256_CBC_SHA" },
        { 0xc023, "TLS_ECDHE_ECDSA_WITH_AES_128_CBC_SHA256" }, { 0xc027, "TLS_ECDHE_RSA_WITH_AES_128_CBC_SHA256" },
        { 0xc02f, "TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256" }, { 0xc02b, "TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256" },
        { 0xc030, "TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384" }, { 0xc02c, "TLS_ECDHE_ECDSA_WITH_AES_256_GCM_SHA384" },
        { 0xcca8, "TLS_ECDHE_RSA_WITH_CHACHA20_POLY1305_SHA256" }, { 0xcca9, "TLS_ECDHE_ECDSA_WITH_CHACHA20_POLY1305_SHA256" },
        { 0x1301, "TLS_AES_128_GCM_SHA256" }, { 0x1302, "TLS_AES_256_GCM_SHA384" }, { 0x1303, "TLS_CHACHA20_POLY1305_SHA256" },
        { 0x5600, "TLS_FALLBACK_SCSV" },
    };
    public static GoString CipherSuiteName(uint id) =>
        GoString.FromDotNetString(CipherNames.TryGetValue((int)(id & 0xffff), out var n) ? n : $"0x{id & 0xffff:X4}");
    public static GoString VersionName(uint version) => GoString.FromDotNetString((version & 0xffff) switch
    {
        0x0300 => "SSLv3", 0x0301 => "TLS 1.0", 0x0302 => "TLS 1.1", 0x0303 => "TLS 1.2", 0x0304 => "TLS 1.3",
        _ => $"0x{version & 0xffff:X4}",
    });
    public static GoString ClientAuthType_String(long i) => GoString.FromDotNetString(i switch
    {
        0 => "NoClientCert", 1 => "RequestClientCert", 2 => "RequireAnyClientCert",
        3 => "VerifyClientCertIfGiven", 4 => "RequireAndVerifyClientCert",
        _ => $"ClientAuthType({i})",
    });
    public static GoString CurveID_String(uint i) => GoString.FromDotNetString((i & 0xffff) switch
    {
        23 => "CurveP256", 24 => "CurveP384", 25 => "CurveP521", 29 => "X25519", 4588 => "X25519MLKEM768",
        _ => $"CurveID({i & 0xffff})",
    });

    private static GoFieldBag SF(object s) => ((GoHttpServer)s).F;
    private static GoFieldBag TF(object t) => ((GoHttpTransport)t).F;
    private static GoFieldBag CF(object c) => ((GoTlsConfig)c).F;
    private static GoFieldBag HF(object c) => ((GoHTTP2Config)c).F;

    // *http.Server: read/write through the field bag; methods are no-ops.
    public static void Server_RegisterOnShutdown(object s, GoClosure? f) { }
    // (*http.Server).Serve(l): goclr serves over the HttpListener backend rather than the
    // caller's net.Listener, so release the port l already bound (echo's newListener grabs
    // it before calling Serve) and serve on the same address with the server's Handler.
    public static object? Server_Serve(object s, object? l)
    {
        string addr = l is GoListener gl && gl.Addr.Length > 0 ? gl.Addr : Server_Addr(s).ToDotNetString();
        Net.ReleaseBound(addr);
        return Http.ListenAndServe(GoString.FromDotNetString(addr), SF(s).Get("Handler"));
    }
    public static void Server_SetKeepAlivesEnabled(object s, bool v) { }
    public static GoString Server_Addr(object s) => SF(s).Get("Addr") is GoString g ? g : GoString.FromDotNetString("");
    public static void Server_SetAddr(object s, GoString v) => SF(s).Set("Addr", v);
    public static void Server_SetHandler(object s, object? v) => SF(s).Set("Handler", v);
    public static void Server_SetErrorLog(object s, object? v) => SF(s).Set("ErrorLog", v);
    public static void Server_SetReadTimeout(object s, long v) => SF(s).Set("ReadTimeout", v);
    public static void Server_SetWriteTimeout(object s, long v) => SF(s).Set("WriteTimeout", v);
    public static void Server_SetReadHeaderTimeout(object s, long v) => SF(s).Set("ReadHeaderTimeout", v);
    public static void Server_SetMaxHeaderBytes(object s, long v) => SF(s).Set("MaxHeaderBytes", v);
    // (*http.Server).ListenAndServe[TLS]: serve over the HttpListener backend using the
    // server's Addr + Handler (TLS isn't terminated here — both share the plain path).
    public static object? Server_ListenAndServe(object s) => Http.ListenAndServe(Server_Addr(s), SF(s).Get("Handler"));
    public static object? Server_ListenAndServeTLS(object s, GoString certFile, GoString keyFile) => Http.ListenAndServe(Server_Addr(s), SF(s).Get("Handler"));
    public static object? Server_Shutdown(object s, object? ctx) => null;
    public static object? Server_Close(object s) => null;
    public static object? Server_TLSConfig(object s) => SF(s).Get("TLSConfig");
    public static void Server_SetTLSConfig(object s, object? v) => SF(s).Set("TLSConfig", v);
    public static GoMap Server_TLSNextProto(object s) => (GoMap)(SF(s).Get("TLSNextProto") ?? GoMaps.Make());
    public static void Server_SetTLSNextProto(object s, GoMap? v) => SF(s).Set("TLSNextProto", v);
    public static object? Server_Handler(object s) => SF(s).Get("Handler");
    public static object? Server_ErrorLog(object s) => SF(s).Get("ErrorLog");
    public static object? Server_BaseContext(object s) => SF(s).Get("BaseContext");
    public static GoClosure? Server_ConnState(object s) => (GoClosure?)SF(s).Get("ConnState");
    public static object? Server_HTTP2(object s) => SF(s).Get("HTTP2");
    public static long Server_ReadTimeout(object s) => SF(s).GetL("ReadTimeout");
    public static long Server_ReadHeaderTimeout(object s) => SF(s).GetL("ReadHeaderTimeout");
    public static long Server_WriteTimeout(object s) => SF(s).GetL("WriteTimeout");
    public static long Server_IdleTimeout(object s) => SF(s).GetL("IdleTimeout");
    public static void Server_SetIdleTimeout(object s, long v) => SF(s).Set("IdleTimeout", v);
    public static long Server_MaxHeaderBytes(object s) => SF(s).GetL("MaxHeaderBytes");

    // *http.Transport field reads.
    public static object? Transport_HTTP2(object t) => TF(t).Get("HTTP2");
    public static object? Transport_TLSClientConfig(object t) => TF(t).Get("TLSClientConfig");
    public static object? Transport_Proxy(object t) => TF(t).Get("Proxy");
    public static object? Transport_DialContext(object t) => TF(t).Get("DialContext");
    public static object? Transport_DialTLSContext(object t) => TF(t).Get("DialTLSContext");
    public static long Transport_MaxHeaderListSize(object t) => TF(t).GetL("MaxHeaderListSize");
    public static long Transport_ExpectContinueTimeout(object t) => TF(t).GetL("ExpectContinueTimeout");
    public static bool Transport_DisableCompression(object t) => TF(t).GetB("DisableCompression");
    public static bool Transport_DisableKeepAlives(object t) => TF(t).GetB("DisableKeepAlives");
    public static bool Transport_ForceAttemptHTTP2(object t) => TF(t).GetB("ForceAttemptHTTP2");
    public static GoMap Transport_TLSNextProto(object t) => (GoMap)(TF(t).Get("TLSNextProto") ?? GoMaps.Make());
    public static void Transport_SetTLSNextProto(object t, GoMap? v) => TF(t).Set("TLSNextProto", v);
    public static long Transport_MaxResponseHeaderBytes(object t) => TF(t).GetL("MaxResponseHeaderBytes");
    public static long Transport_IdleConnTimeout(object t) => TF(t).GetL("IdleConnTimeout");
    public static long Transport_ResponseHeaderTimeout(object t) => TF(t).GetL("ResponseHeaderTimeout");
    public static void Transport_SetTLSClientConfig(object t, object? v) => TF(t).Set("TLSClientConfig", v);
    public static void Transport_SetHTTP2(object t, object? v) => TF(t).Set("HTTP2", v);
    public static void Transport_RegisterProtocol(object t, GoString scheme, object? rt) { } // dead path (client never runs)
    public static void Transport_CloseIdleConnections(object t) { }
    public static object Transport_Clone(object t) => new GoHttpTransport();

    // *tls.Config field reads/writes.
    public static GoSlice Config_NextProtos(object c) => CF(c).Get("NextProtos") is GoSlice s ? s : default;
    public static void Config_SetNextProtos(object c, GoSlice v) => CF(c).Set("NextProtos", v);
    public static GoSlice Config_CipherSuites(object c) => CF(c).Get("CipherSuites") is GoSlice s ? s : default;
    public static uint Config_MinVersion(object c) => (uint)CF(c).GetL("MinVersion");
    public static long Config_MaxVersion(object c) => CF(c).GetL("MaxVersion");
    public static bool Config_InsecureSkipVerify(object c) => CF(c).GetB("InsecureSkipVerify");
    public static object? Config_GetCertificate(object c) => CF(c).Get("GetCertificate");
    public static bool Config_PreferServerCipherSuites(object c) => CF(c).GetB("PreferServerCipherSuites");
    public static void Config_SetPreferServerCipherSuites(object c, bool v) => CF(c).Set("PreferServerCipherSuites", v);
    public static object Config_Clone(object c) => new GoTlsConfig(); // dead path: a fresh config
    public static void Config_BuildNameToCertificate(object c) { } // deprecated no-op
    public static GoString Config_ServerName(object c) => CF(c).Get("ServerName") is GoString g ? g : GoString.FromDotNetString("");
    public static void Config_SetServerName(object c, GoString v) => CF(c).Set("ServerName", v);
    public static void Config_SetMinVersion(object c, uint v) => CF(c).Set("MinVersion", v);
    public static void Config_SetMaxVersion(object c, long v) => CF(c).Set("MaxVersion", v);
    public static void Config_SetInsecureSkipVerify(object c, bool v) => CF(c).Set("InsecureSkipVerify", v);
    public static void Config_SetGetCertificate(object c, GoClosure? v) => CF(c).Set("GetCertificate", v);
    public static object? Config_RootCAs(object c) => CF(c).Get("RootCAs");
    public static GoSlice Config_Certificates(object c) => CF(c).Get("Certificates") is GoSlice s ? s : default;
    public static void Config_SetCertificates(object c, GoSlice v) => CF(c).Set("Certificates", v);
    // tls.Certificate field reads/writes (autocert paths; empty under plain serving).
    public static object? Cert_PrivateKey(object c) => ((GoTlsCert)c).F.Get("PrivateKey");
    public static object? Cert_Leaf(object c) => ((GoTlsCert)c).F.Get("Leaf");
    public static GoSlice Cert_Certificate(object c) => ((GoTlsCert)c).F.Get("Certificate") is GoSlice s ? s : default;
    public static object? Cert_OCSPStaple(object c) => ((GoTlsCert)c).F.Get("OCSPStaple");
    public static void Cert_SetPrivateKey(object c, object? v) => ((GoTlsCert)c).F.Set("PrivateKey", v);
    public static void Cert_SetLeaf(object c, object? v) => ((GoTlsCert)c).F.Set("Leaf", v);
    public static void Cert_SetCertificate(object c, GoSlice v) => ((GoTlsCert)c).F.Set("Certificate", v);
    public static void Cert_SetOCSPStaple(object c, object? v) => ((GoTlsCert)c).F.Set("OCSPStaple", v);
    public static long Config_ClientAuth(object c) => CF(c).GetL("ClientAuth");

    // *http.HTTP2Config field reads.
    public static long H2C_MaxConcurrentStreams(object c) => HF(c).GetL("MaxConcurrentStreams");
    public static long H2C_MaxDecoderHeaderTableSize(object c) => HF(c).GetL("MaxDecoderHeaderTableSize");
    public static long H2C_MaxEncoderHeaderTableSize(object c) => HF(c).GetL("MaxEncoderHeaderTableSize");
    public static long H2C_MaxReadFrameSize(object c) => HF(c).GetL("MaxReadFrameSize");
    public static long H2C_MaxReceiveBufferPerConnection(object c) => HF(c).GetL("MaxReceiveBufferPerConnection");
    public static long H2C_MaxReceiveBufferPerStream(object c) => HF(c).GetL("MaxReceiveBufferPerStream");
    public static long H2C_MaxUploadBufferPerConnection(object c) => HF(c).GetL("MaxUploadBufferPerConnection");
    public static long H2C_MaxUploadBufferPerStream(object c) => HF(c).GetL("MaxUploadBufferPerStream");
    public static bool H2C_PermitProhibitedCipherSuites(object c) => HF(c).GetB("PermitProhibitedCipherSuites");
    public static bool H2C_StrictMaxConcurrentStreams(object c) => HF(c).GetB("StrictMaxConcurrentStreams");
    public static bool H2C_StrictMaxConcurrentRequests(object c) => HF(c).GetB("StrictMaxConcurrentRequests");
    public static long H2C_PingTimeout(object c) => HF(c).GetL("PingTimeout");
    public static long H2C_ReadIdleTimeout(object c) => HF(c).GetL("ReadIdleTimeout");
    public static long H2C_SendPingTimeout(object c) => HF(c).GetL("SendPingTimeout");
    public static long H2C_WriteByteTimeout(object c) => HF(c).GetL("WriteByteTimeout");
    public static GoClosure? H2C_CountError(object c) => (GoClosure?)HF(c).Get("CountError");

    // http.Protocols: a small bitset the caller mutates.
    public static void Proto_SetHTTP1(object p, bool v) => ((GoHttpProtocols)p).H1 = v;
    public static void Proto_SetHTTP2(object p, bool v) => ((GoHttpProtocols)p).H2 = v;
    public static void Proto_SetUnencryptedHTTP2(object p, bool v) => ((GoHttpProtocols)p).UH2 = v;
    public static bool Proto_HTTP1(object p) => ((GoHttpProtocols)p).H1;
    public static bool Proto_HTTP2(object p) => ((GoHttpProtocols)p).H2;
    public static bool Proto_UnencryptedHTTP2(object p) => ((GoHttpProtocols)p).UH2;

    // *tls.Conn methods (dead under the HttpListener server).
    public static object? Conn_Close(object c) => null;
    public static object? Conn_LocalAddr(object c) => new GoNetAddr();
    public static object? Conn_RemoteAddr(object c) => new GoNetAddr();
    public static object?[] Conn_Read(object c, GoSlice p) => new object?[] { 0L, Io.EOFSentinel };
    public static object?[] Conn_Write(object c, GoSlice p) => new object?[] { (long)p.Len, null };
    public static object? Conn_Handshake(object c) => null;
    public static object? Conn_HandshakeContext(object c, object? ctx) => null;
    public static object Conn_ConnectionState(object c) => new GoTlsConnState();
    public static object? Conn_SetDeadline(object c, object? t) => null;
    public static object? Conn_SetReadDeadline(object c, object? t) => null;
    public static object? Conn_SetWriteDeadline(object c, object? t) => null;
    public static object? Conn_NetConn(object c) => null;

    // tls.ConnectionState field reads.
    public static GoString CS_NegotiatedProtocol(object s) => ((GoTlsConnState)s).F.Get("NegotiatedProtocol") is GoString g ? g : GoString.FromDotNetString("");
    public static GoString CS_ServerName(object s) => ((GoTlsConnState)s).F.Get("ServerName") is GoString g ? g : GoString.FromDotNetString("");
    public static uint CS_Version(object s) => (uint)((GoTlsConnState)s).F.GetL("Version");
    public static uint CS_CipherSuite(object s) => (uint)((GoTlsConnState)s).F.GetL("CipherSuite");
    public static bool CS_HandshakeComplete(object s) => ((GoTlsConnState)s).F.GetB("HandshakeComplete");
    public static bool CS_DidResume(object s) => ((GoTlsConnState)s).F.GetB("DidResume");
    public static bool CS_NegotiatedProtocolIsMutual(object s) => true;
    public static GoSlice CS_PeerCertificates(object s) => ((GoTlsConnState)s).F.Get("PeerCertificates") is GoSlice sl ? sl : default;

    public static object NewServer() => new GoHttpServer();
    public static object NewTransport() => new GoHttpTransport();
    public static object NewTlsConfig() => new GoTlsConfig();
    public static object NewTlsCert() => new GoTlsCert();
    // tls.NewListener(inner, config): wrap a net.Listener for TLS. Under goclr's plain
    // serving the TLS handshake is never performed, so the inner listener passes through.
    public static object? NewListener(object? inner, object? config) => inner;
    // crypto/tls.Listen(network, addr, config): bind a plain listener (TLS is terminated by
    // goclr's HttpListener backend, so the config is ignored) so app.ListenTLS still binds.
    public static object?[] TlsListen(GoString network, GoString addr, object? config) => Net.Listen(network, addr);
    // tls.X509KeyPair / tls.LoadX509KeyPair: parse a PEM cert+key into a tls.Certificate.
    // Plain HTTP serving never exercises this; the handle is an opaque placeholder.
    public static object?[] X509KeyPair(GoSlice certPEM, GoSlice keyPEM) => new object?[] { new GoTlsCert(), null };
    public static object?[] LoadX509KeyPair(GoString certFile, GoString keyFile) => new object?[] { new GoTlsCert(), null };
    public static object NewHTTP2Config() => new GoHTTP2Config();
    public static object NewProtocols() => new GoHttpProtocols();
    public static object NewTlsConn() => new GoTlsConn();
    // tls.Dialer (dead path under goclr's server): a dial never succeeds.
    public static object?[] Dialer_DialContext(object d, object? ctx, GoString network, GoString addr) => new object?[] { null, new GoError(GoString.FromDotNetString("tls: dial not supported")) };
    public static object?[] Dialer_Dial(object d, GoString network, GoString addr) => new object?[] { null, new GoError(GoString.FromDotNetString("tls: dial not supported")) };
    public static object NewTlsConnState() => new GoTlsConnState();
    // tls.Server(conn, config) / tls.Client(conn, config) *Conn — the HttpListener
    // backend terminates TLS itself, so these wrap the conn opaquely (dead path here).
    public static object TlsServer(object? conn, object? config) => new GoTlsConn();
    public static object TlsClient(object? conn, object? config) => new GoTlsConn();

    // http.ServeMux + http.DefaultServeMux.
    private static readonly GoServeMux _defaultMux = new();
    public static object DefaultServeMux() => _defaultMux;
    public static object NewServeMux() => new GoServeMux();
    public static void Mux_Handle(object m, GoString pat, object? h)
    { var mux = (GoServeMux)m; lock (mux.Routes) mux.Routes.Add((pat.ToDotNetString(), h)); }
    public static void Mux_HandleFunc(object m, GoString pat, GoClosure h)
    { var mux = (GoServeMux)m; lock (mux.Routes) mux.Routes.Add((pat.ToDotNetString(), h)); }
    public static void Mux_ServeHTTP(object m, object? w, object? r)
    {
        var mux = (GoServeMux)m;
        var req = r as GoRequest;
        string method = req?.Method ?? "GET";
        string path = req?.Url.Path ?? "/";

        object? best = null; int bestScore = int.MinValue;
        System.Collections.Generic.Dictionary<string, string>? bestVals = null;
        var allowed = new System.Collections.Generic.SortedSet<string>();

        lock (mux.Routes)
            foreach (var (pat, h) in mux.Routes)
            {
                var rt = ParsePattern(pat);
                if (!PathMatches(rt, path, out var vals, out int score)) continue;
                if (rt.method.Length > 0 && rt.method != method) { allowed.Add(rt.method); continue; }
                // A method-specific route outranks a method-agnostic one; among equals the
                // most literal (fewest wildcards) wins, matching Go's specificity order.
                int s = score + (rt.method.Length > 0 ? 1 << 20 : 0);
                if (s > bestScore) { bestScore = s; best = h; bestVals = vals; }
            }

        if (best != null)
        {
            if (req != null) req.PathValues = bestVals;
            Http.ServeHTTPDyn(best, w, r);
        }
        else if (allowed.Count > 0)
            Http.Error(w!, GoString.FromDotNetString("Method Not Allowed"), 405);
        else Http.NotFound(w!, r);
    }
    public static object?[] Mux_Handler(object m, object? r) => new object?[] { null, GoString.FromDotNetString("") };

    // A parsed ServeMux pattern: optional leading "METHOD ", path segments (a "{name}" or
    // "{name...}" segment is a wildcard), and whether it is a subtree (ends with "/").
    private struct MuxPat { public string method; public string[] segs; public bool subtree; public bool multi; public string multiName; }

    private static MuxPat ParsePattern(string pat)
    {
        var p = new MuxPat { method = "", multiName = "" };
        int sp = pat.IndexOf(' ');
        if (sp >= 0) { p.method = pat.Substring(0, sp).Trim(); pat = pat.Substring(sp + 1).Trim(); }
        p.subtree = pat.EndsWith("/");
        var segs = pat.Split('/', System.StringSplitOptions.RemoveEmptyEntries);
        if (segs.Length > 0)
        {
            string last = segs[segs.Length - 1];
            if (last.StartsWith("{") && last.EndsWith("...}")) { p.multi = true; p.multiName = last.Substring(1, last.Length - 5); }
        }
        p.segs = segs;
        return p;
    }

    private static bool PathMatches(MuxPat p, string reqPath, out System.Collections.Generic.Dictionary<string, string> vals, out int score)
    {
        vals = new(); score = 0;
        var r = reqPath.Split('/', System.StringSplitOptions.RemoveEmptyEntries);
        if (p.multi)
        {
            int fixedN = p.segs.Length - 1;
            if (r.Length < fixedN) return false;
            for (int i = 0; i < fixedN; i++) if (!MatchSeg(p.segs[i], r[i], vals, ref score)) return false;
            vals[p.multiName] = string.Join("/", r, fixedN, r.Length - fixedN);
            return true;
        }
        if (p.subtree)
        {
            if (r.Length < p.segs.Length) return false;
            for (int i = 0; i < p.segs.Length; i++) if (!MatchSeg(p.segs[i], r[i], vals, ref score)) return false;
            return true;
        }
        if (r.Length != p.segs.Length) return false;
        for (int i = 0; i < p.segs.Length; i++) if (!MatchSeg(p.segs[i], r[i], vals, ref score)) return false;
        return true;
    }

    private static bool MatchSeg(string pseg, string rseg, System.Collections.Generic.Dictionary<string, string> vals, ref int score)
    {
        if (pseg.Length >= 2 && pseg[0] == '{' && pseg[pseg.Length - 1] == '}')
        { vals[pseg.Substring(1, pseg.Length - 2)] = rseg; return true; }
        if (pseg != rseg) return false;
        score++; // a literal segment match is more specific than a wildcard
        return true;
    }
}
