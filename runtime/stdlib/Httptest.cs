namespace GoCLR.Stdlib;

using System.Net;
using GoCLR.Runtime;

/// <summary>An httptest.Server: a real HttpListener on a loopback port, served on a
/// background thread that dispatches to the Go handler through the http shim.</summary>
public sealed class GoHttptestServer
{
    public HttpListener Listener = null!;
    public string Url = "";
    public volatile bool Running = true;
}

/// <summary>Shim for net/http/httptest: a live test server, a request constructor and an
/// in-memory ResponseRecorder (which is just a server-less GoRespWriter).</summary>
public static class Httptest
{
    private static int FreePort()
    {
        var t = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        t.Start();
        int p = ((IPEndPoint)t.LocalEndpoint).Port;
        t.Stop();
        return p;
    }

    public static object NewServer(object? handler)
    {
        int port = FreePort();
        var l = new HttpListener();
        l.Prefixes.Add($"http://127.0.0.1:{port}/");
        l.Start();
        var srv = new GoHttptestServer { Listener = l, Url = $"http://127.0.0.1:{port}" };
        var th = new System.Threading.Thread(() =>
        {
            while (srv.Running)
            {
                HttpListenerContext ctx;
                try { ctx = l.GetContext(); }
                catch { break; }
                var w = new GoRespWriter { Resp = ctx.Response };
                var req = Http.MakeRequest(ctx.Request);
                try { Http.Dispatch(handler, w, req, ctx.Request.Url?.AbsolutePath ?? "/"); }
                catch { }
                try { Http.Commit(w); } catch { }
                try { ctx.Response.OutputStream.Close(); } catch { }
            }
        })
        { IsBackground = true };
        th.Start();
        return srv;
    }

    // httptest.NewTLSServer falls back to plaintext (the HttpListener backend does not
    // terminate TLS), matching how the http shim treats ListenAndServeTLS.
    public static object NewTLSServer(object? handler) => NewServer(handler);
    public static object NewUnstartedServer(object? handler) => NewServer(handler);

    public static GoString Server_URL(object s) => GoString.FromDotNetString(((GoHttptestServer)s).Url);

    public static void Server_Close(object s)
    {
        var srv = (GoHttptestServer)s;
        srv.Running = false;
        try { srv.Listener.Stop(); } catch { }
    }

    // The server's Client() is a plain http.Client; the shim's client is stateless, so
    // returning null lets http.Get / the default client reach the loopback server.
    public static object? Server_Client(object s) => null;
    public static void Server_Start(object s) { }

    // --- ResponseRecorder: a GoRespWriter with no backing HttpListenerResponse -------
    public static object NewRecorder() => new GoRespWriter { Headers = GoMaps.Make() };

    public static long Recorder_Code(object r) => ((GoRespWriter)r).Status;
    public static object Recorder_Body(object r)
    {
        var buf = new GoBuffer();
        buf.B.AddRange(((GoRespWriter)r).Body.ToArray());
        return buf;
    }
    public static object Recorder_HeaderMap(object r) => ((GoRespWriter)r).Headers ??= GoMaps.Make();
    public static object Recorder_Header(object r) => ((GoRespWriter)r).Headers ??= GoMaps.Make();
    public static object?[] Recorder_Write(object r, GoSlice p) => Http.RW_Write(r, p);
    public static void Recorder_WriteHeader(object r, long code) => Http.RW_WriteHeader(r, code);
    public static GoString Recorder_BodyString(object r) =>
        GoString.FromDotNetString(System.Text.Encoding.UTF8.GetString(((GoRespWriter)r).Body.ToArray()));

    // --- NewRequest(method, target, body) *http.Request ------------------------------
    public static object NewRequest(GoString method, GoString target, object? body)
    {
        string t = target.ToDotNetString();
        var u = new GoUrl();
        int q = t.IndexOf('?');
        string path = q >= 0 ? t.Substring(0, q) : t;
        string rawQuery = q >= 0 ? t.Substring(q + 1) : "";
        if (path.StartsWith("http://") || path.StartsWith("https://"))
        {
            var parsed = new System.Uri(path);
            u.Scheme = parsed.Scheme; u.Host = parsed.Authority; u.Path = parsed.AbsolutePath;
        }
        else u.Path = path;
        u.RawQuery = rawQuery;
        byte[] raw = body == null ? System.Array.Empty<byte>() : Readers.Drain(body);
        return new GoRequest
        {
            Method = method.ToDotNetString(),
            Url = u,
            Body = new GoReader { Data = raw },
            RawBody = raw,
            Host = u.Host,
            Header = GoMaps.Make(),
        };
    }
}
