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

    // (enc Encoding).Strict() Encoding: strict decoding rejects trailing non-zero bits;
    // goclr decodes leniently, so this returns the same encoding handle.
    public static object Strict(object enc) => enc;

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

    public static long EncodedLen(object enc, long n) =>
        ((GoBase64)enc).Pad ? (n + 2) / 3 * 4 : (n * 8 + 5) / 6;
    public static long DecodedLen(object enc, long n) =>
        ((GoBase64)enc).Pad ? n / 4 * 3 : n * 6 / 8;

    // Encode(dst, src): write the encoding of src into dst, returning the byte count.
    public static void Encode(object enc, GoSlice dst, GoSlice src)
    {
        var s = EncodeToString(enc, src);
        var by = s.Bytes;
        for (int i = 0; i < by.Length && i < dst.Len; i++) dst.Data![dst.Off + i] = (int)by[i];
    }
    // Decode(dst, src) (n int, err error).
    public static object?[] Decode(object enc, GoSlice dst, GoSlice src)
    {
        var r = DecodeString(enc, GoString.FromBytes(Bytes(src)));
        if (r[1] != null) return new object?[] { 0L, r[1] };
        var decoded = (GoSlice)r[0]!;
        for (int i = 0; i < decoded.Len && i < dst.Len; i++) dst.Data![dst.Off + i] = decoded.Data![decoded.Off + i];
        return new object?[] { (long)decoded.Len, null };
    }
}
