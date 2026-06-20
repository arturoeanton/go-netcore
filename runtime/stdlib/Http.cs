namespace GoCLR.Stdlib;

using System.Net.Http;
using GoCLR.Runtime;

/// <summary>An *http.Response handle (status + body snapshot).</summary>
public sealed class GoResponse { public int StatusCode; public string Status = ""; public GoReader Body = new(); public long ContentLength; }

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
}
