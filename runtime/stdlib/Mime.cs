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
}
