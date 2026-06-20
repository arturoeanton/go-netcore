namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>A base64 *Encoding handle (std/url alphabet, with or without padding).</summary>
public sealed class GoBase64 { public bool Url; public bool Pad; }

/// <summary>Shim for Go's <c>encoding/base64</c>. StdEncoding/URLEncoding/RawStd/RawURL
/// are package vars (shim variables); the *Encoding methods dispatch here.</summary>
public static class Base64
{
    public static object StdEncoding() => new GoBase64 { Url = false, Pad = true };
    public static object URLEncoding() => new GoBase64 { Url = true, Pad = true };
    public static object RawStdEncoding() => new GoBase64 { Url = false, Pad = false };
    public static object RawURLEncoding() => new GoBase64 { Url = true, Pad = false };

    private static byte[] Bytes(GoSlice s)
    {
        var b = new byte[s.Len];
        for (int i = 0; i < s.Len; i++) b[i] = (byte)System.Convert.ToInt64(s.Data![s.Off + i]);
        return b;
    }
    private static GoSlice Slice(byte[] b)
    {
        var d = new object?[b.Length];
        for (int i = 0; i < b.Length; i++) d[i] = (int)b[i];
        return new GoSlice { Data = d, Off = 0, Len = b.Length, Cap = b.Length };
    }

    public static GoString EncodeToString(object enc, GoSlice src)
    {
        var e = (GoBase64)enc;
        string s = System.Convert.ToBase64String(Bytes(src));
        if (e.Url) s = s.Replace('+', '-').Replace('/', '_');
        if (!e.Pad) s = s.TrimEnd('=');
        return GoString.FromDotNetString(s);
    }

    public static object?[] DecodeString(object enc, GoString src)
    {
        var e = (GoBase64)enc;
        string s = src.ToDotNetString();
        if (e.Url) s = s.Replace('-', '+').Replace('_', '/');
        if (!e.Pad) { int m = s.Length % 4; if (m != 0) s += new string('=', 4 - m); }
        try { return new object?[] { Slice(System.Convert.FromBase64String(s)), null }; }
        catch { return new object?[] { default(GoSlice), new GoError(GoString.FromDotNetString("illegal base64 data")) }; }
    }
}
