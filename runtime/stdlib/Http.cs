namespace GoCLR.Stdlib;

using System.Net;
using System.Net.Http;
using GoCLR.Runtime;

/// <summary>An *http.Response handle (status + body snapshot).</summary>
public sealed class GoResponse { public int StatusCode; public string Status = ""; public GoReader Body = new(); public long ContentLength; }

/// <summary>An http.ResponseWriter backed by an HttpListenerResponse.</summary>
public sealed class GoRespWriter { public HttpListenerResponse Resp = null!; public bool WroteHeader; public GoMap? Headers; }

/// <summary>An *http.Request handle (server side).</summary>
public sealed class GoRequest { public string Method = ""; public GoUrl Url = new(); public GoReader Body = new(); public string Host = ""; public string RemoteAddr = ""; public GoMap? Form; public GoMap? PostForm; public GoMap? Header; public byte[] RawBody = System.Array.Empty<byte>(); public readonly GoFieldBag Extra = new(); }

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

    // http sentinel errors + well-known vars.
    public static readonly GoError ErrAbortHandlerSentinel = new(GoString.FromDotNetString("net/http: abort Handler"));
    public static object ErrAbortHandler() => ErrAbortHandlerSentinel;
    public static readonly GoError ErrBodyNotAllowedSentinel = new(GoString.FromDotNetString("http: request method or response status code does not allow body"));
    public static object ErrBodyNotAllowed() => ErrBodyNotAllowedSentinel;
    public static readonly GoError ErrNotSupportedSentinel = new(GoString.FromDotNetString("feature not supported"));
    public static object ErrNotSupported() => ErrNotSupportedSentinel;
    public static readonly GoError ErrSkipAltProtocolSentinel = new(GoString.FromDotNetString("net/http: skip alternate protocol"));
    public static object ErrSkipAltProtocol() => ErrSkipAltProtocolSentinel;
    public static readonly GoError ErrServerClosedSentinel = new(GoString.FromDotNetString("http: Server closed"));
    public static object ErrServerClosed() => ErrServerClosedSentinel;
    public static readonly GoError ErrHandlerTimeoutSentinel = new(GoString.FromDotNetString("http: Handler timeout"));
    public static object ErrHandlerTimeout() => ErrHandlerTimeoutSentinel;
    private static readonly GoReader _noBody = new();
    public static object NoBody() => _noBody;
    private static readonly object _localAddrKey = new();
    public static object LocalAddrContextKey() => _localAddrKey;
    private static readonly object _serverCtxKey = new();
    public static object ServerContextKey() => _serverCtxKey;

    // http.Error(w, msg, code): write the status code and message.
    public static void Error(object w, GoString msg, long code)
    {
        RW_WriteHeader(w, code);
        var by = (msg.ToDotNetString() + "\n");
        Fmt.WriteTo(w, by);
    }

    // http.CanonicalHeaderKey(s): canonical MIME header key ("content-type" -> "Content-Type").
    public static GoString CanonicalHeaderKey(GoString s) => Textproto.CanonicalMIMEHeaderKey(s);
    // http.StatusText(code): the text for an HTTP status code.
    public static GoString StatusText(long code) => GoString.FromDotNetString(((System.Net.HttpStatusCode)(int)code).ToString());
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

    // (*http.Request).Context(): goclr has no per-request cancellation, so this is a
    // fresh background context.
    public static object Req_Context(object r) => Context.Background();

    // Extra *http.Request fields read/written by x/net/http2 (stored in the field bag).
    public static long Req_ContentLength(object r) => ((GoRequest)r).Extra.GetL("ContentLength");
    public static void Req_SetContentLength(object r, long v) => ((GoRequest)r).Extra.Set("ContentLength", v);
    public static object? Req_Trailer(object r) => ((GoRequest)r).Extra.Get("Trailer");
    public static void Req_SetTrailer(object r, object? v) => ((GoRequest)r).Extra.Set("Trailer", v);
    public static object? Req_TLS(object r) => ((GoRequest)r).Extra.Get("TLS");
    public static void Req_SetTLS(object r, object? v) => ((GoRequest)r).Extra.Set("TLS", v);
    public static object? Req_MultipartForm(object r) => ((GoRequest)r).Extra.Get("MultipartForm");
    public static void Req_SetBody(object r, object? v) => ((GoRequest)r).Extra.Set("Body", v);
    public static GoString Req_Proto(object r) => GoString.FromDotNetString("HTTP/1.1");
    public static long Req_ProtoMajor(object r) => 1;
    public static long Req_ProtoMinor(object r) => 1;
    public static GoString Req_RequestURI(object r) => Url.URL_RequestURI(((GoRequest)r).Url);
    // (*http.Request).WithContext/Clone: goclr has no request context, so return self.
    public static object Req_WithContext(object r, object? ctx) => r;
    public static object Req_Clone(object r, object? ctx) => r;
    public static GoString Req_UserAgent(object r) => GoString.FromDotNetString("");
    public static GoString Req_Referer(object r) => GoString.FromDotNetString("");
    public static object?[] Req_Cookie(object r, GoString name) => new object?[] { null, new GoError("http: named cookie not present") };
    public static GoSlice Req_Cookies(object r) => default;

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
    public static object Header_Clone(object h)
    {
        var src = (GoMap)h;
        if (src.Data == null) return new GoMap { Data = null };
        var m = GoMaps.Make();
        foreach (var kv in src.Data) m.Data![kv.Key] = kv.Value;
        return m;
    }
    public static object? Header_Write(object h, object? w) => null;

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
public sealed class GoHTTP2Config { public readonly GoFieldBag F = new(); }
public sealed class GoHttpProtocols { public bool H1, H2, UH2; }
public sealed class GoTlsConn { }
public sealed class GoTlsConnState { public readonly GoFieldBag F = new(); }
public sealed class GoServeMux { }

public static class HttpTypes
{
    private static GoFieldBag SF(object s) => ((GoHttpServer)s).F;
    private static GoFieldBag TF(object t) => ((GoHttpTransport)t).F;
    private static GoFieldBag CF(object c) => ((GoTlsConfig)c).F;
    private static GoFieldBag HF(object c) => ((GoHTTP2Config)c).F;

    // *http.Server: read/write through the field bag; methods are no-ops.
    public static void Server_RegisterOnShutdown(object s, object? f) { }
    public static object? Server_Serve(object s, object? l) => Io.EOFSentinel;
    public static void Server_SetKeepAlivesEnabled(object s, bool v) { }
    public static object? Server_TLSConfig(object s) => SF(s).Get("TLSConfig");
    public static void Server_SetTLSConfig(object s, object? v) => SF(s).Set("TLSConfig", v);
    public static object Server_TLSNextProto(object s) => SF(s).Get("TLSNextProto") ?? GoMaps.Make();
    public static void Server_SetTLSNextProto(object s, object? v) => SF(s).Set("TLSNextProto", v);
    public static object? Server_Handler(object s) => SF(s).Get("Handler");
    public static object? Server_ErrorLog(object s) => SF(s).Get("ErrorLog");
    public static object? Server_BaseContext(object s) => SF(s).Get("BaseContext");
    public static object? Server_ConnState(object s) => SF(s).Get("ConnState");
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
    public static object Transport_TLSNextProto(object t) => TF(t).Get("TLSNextProto") ?? GoMaps.Make();
    public static void Transport_SetTLSNextProto(object t, object? v) => TF(t).Set("TLSNextProto", v);

    // *tls.Config field reads/writes.
    public static GoSlice Config_NextProtos(object c) => CF(c).Get("NextProtos") is GoSlice s ? s : default;
    public static void Config_SetNextProtos(object c, GoSlice v) => CF(c).Set("NextProtos", v);
    public static GoSlice Config_CipherSuites(object c) => CF(c).Get("CipherSuites") is GoSlice s ? s : default;
    public static long Config_MinVersion(object c) => CF(c).GetL("MinVersion");
    public static long Config_MaxVersion(object c) => CF(c).GetL("MaxVersion");
    public static bool Config_InsecureSkipVerify(object c) => CF(c).GetB("InsecureSkipVerify");
    public static object? Config_GetCertificate(object c) => CF(c).Get("GetCertificate");
    public static bool Config_PreferServerCipherSuites(object c) => CF(c).GetB("PreferServerCipherSuites");
    public static void Config_SetPreferServerCipherSuites(object c, bool v) => CF(c).Set("PreferServerCipherSuites", v);

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
    public static object? H2C_CountError(object c) => HF(c).Get("CountError");

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
    public static object? Conn_SetDeadline(object c, long t) => null;
    public static object? Conn_SetReadDeadline(object c, long t) => null;
    public static object? Conn_SetWriteDeadline(object c, long t) => null;
    public static object? Conn_NetConn(object c) => null;

    // tls.ConnectionState field reads.
    public static GoString CS_NegotiatedProtocol(object s) => ((GoTlsConnState)s).F.Get("NegotiatedProtocol") is GoString g ? g : GoString.FromDotNetString("");
    public static GoString CS_ServerName(object s) => ((GoTlsConnState)s).F.Get("ServerName") is GoString g ? g : GoString.FromDotNetString("");
    public static long CS_Version(object s) => ((GoTlsConnState)s).F.GetL("Version");
    public static long CS_CipherSuite(object s) => ((GoTlsConnState)s).F.GetL("CipherSuite");
    public static bool CS_HandshakeComplete(object s) => ((GoTlsConnState)s).F.GetB("HandshakeComplete");
    public static bool CS_DidResume(object s) => ((GoTlsConnState)s).F.GetB("DidResume");
    public static GoSlice CS_PeerCertificates(object s) => ((GoTlsConnState)s).F.Get("PeerCertificates") is GoSlice sl ? sl : default;

    public static object NewServer() => new GoHttpServer();
    public static object NewTransport() => new GoHttpTransport();
    public static object NewTlsConfig() => new GoTlsConfig();
    public static object NewHTTP2Config() => new GoHTTP2Config();
    public static object NewProtocols() => new GoHttpProtocols();
    public static object NewTlsConn() => new GoTlsConn();
    public static object NewTlsConnState() => new GoTlsConnState();

    // http.ServeMux + http.DefaultServeMux.
    private static readonly GoServeMux _defaultMux = new();
    public static object DefaultServeMux() => _defaultMux;
    public static object NewServeMux() => new GoServeMux();
    public static void Mux_Handle(object m, GoString pat, object? h) { }
    public static void Mux_HandleFunc(object m, GoString pat, object? h) { }
    public static void Mux_ServeHTTP(object m, object? w, object? r) { }
    public static object?[] Mux_Handler(object m, object? r) => new object?[] { null, GoString.FromDotNetString("") };
}
