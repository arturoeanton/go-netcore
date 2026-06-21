namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>A mime/multipart.Form: parsed multipart form values + files.</summary>
public sealed class GoMultipartForm
{
    public GoMap? Value; // map[string][]string
    public GoMap? File;  // map[string][]*FileHeader
}

/// <summary>Shim for a subset of mime/multipart (Form + RemoveAll). Full reader/part
/// parsing is added in the multipart phase.</summary>
public static class Multipart
{
    public static object? Form_RemoveAll(object f) => null; // no temp files to clean
    public static object? Form_Value(object f) => ((GoMultipartForm)f).Value ?? GoMaps.Make();
    public static object? Form_File(object f) => ((GoMultipartForm)f).File ?? GoMaps.Make();
}
