namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>A base64 *Encoding handle: a 64-char alphabet plus a padding char
/// (NoPadding == -1). Std/URL/Raw and NewEncoding/WithPadding all funnel here.</summary>
public sealed class GoBase64
{
    public string Alphabet = StdAlphabet;
    public int Padding = '=';
    public bool Url => Alphabet == UrlAlphabet; // kept for any legacy callers
    public bool Pad => Padding >= 0;

    public const string StdAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
    public const string UrlAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
}

/// <summary>Shim for Go's <c>encoding/base64</c>. StdEncoding/URLEncoding/RawStd/RawURL
/// are package vars (shim variables); the *Encoding methods dispatch here.</summary>
public static class Base64
{
    public static object StdEncoding() => new GoBase64 { Alphabet = GoBase64.StdAlphabet, Padding = '=' };
    public static object URLEncoding() => new GoBase64 { Alphabet = GoBase64.UrlAlphabet, Padding = '=' };
    public static object RawStdEncoding() => new GoBase64 { Alphabet = GoBase64.StdAlphabet, Padding = -1 };
    public static object RawURLEncoding() => new GoBase64 { Alphabet = GoBase64.UrlAlphabet, Padding = -1 };

    // base64.NewEncoding(encoder string): a custom 64-byte alphabet, padded with '=' by default.
    public static object NewEncoding(GoString encoder)
    {
        string a = encoder.ToDotNetString();
        if (a.Length != 64)
            throw new GoPanicException(GoString.FromDotNetString("encoding/base64: encoding alphabet is not 64-bytes long"));
        return new GoBase64 { Alphabet = a, Padding = '=' };
    }
    // (enc Encoding).WithPadding(padding rune): a copy using a different padding char (NoPadding == -1).
    public static object WithPadding(object enc, int padding)
    {
        var e = (GoBase64)enc;
        if (padding != -1 && (padding == '\r' || padding == '\n' || padding > 0xff))
            throw new GoPanicException(GoString.FromDotNetString("invalid padding"));
        if (padding != -1 && e.Alphabet.IndexOf((char)padding) >= 0)
            throw new GoPanicException(GoString.FromDotNetString("padding contained in alphabet"));
        return new GoBase64 { Alphabet = e.Alphabet, Padding = padding };
    }

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

    public static object Strict(object enc) => enc;

    // Manual base64 over the encoding's alphabet (byte-exact, supports custom alphabets/padding).
    private static string Enc(byte[] src, GoBase64 e)
    {
        string a = e.Alphabet;
        var sb = new System.Text.StringBuilder();
        int i = 0;
        for (; i + 3 <= src.Length; i += 3)
        {
            int n = (src[i] << 16) | (src[i + 1] << 8) | src[i + 2];
            sb.Append(a[(n >> 18) & 63]).Append(a[(n >> 12) & 63]).Append(a[(n >> 6) & 63]).Append(a[n & 63]);
        }
        int rem = src.Length - i;
        if (rem == 1)
        {
            int n = src[i] << 16;
            sb.Append(a[(n >> 18) & 63]).Append(a[(n >> 12) & 63]);
            if (e.Padding >= 0) sb.Append((char)e.Padding).Append((char)e.Padding);
        }
        else if (rem == 2)
        {
            int n = (src[i] << 16) | (src[i + 1] << 8);
            sb.Append(a[(n >> 18) & 63]).Append(a[(n >> 12) & 63]).Append(a[(n >> 6) & 63]);
            if (e.Padding >= 0) sb.Append((char)e.Padding);
        }
        return sb.ToString();
    }

    // Returns (bytes, errorOffset) — errorOffset >= 0 marks the first illegal input byte.
    private static (byte[]?, int) Dec(string s, GoBase64 e)
    {
        var inv = new int[256];
        for (int k = 0; k < 256; k++) inv[k] = -1;
        for (int k = 0; k < 64; k++) inv[e.Alphabet[k]] = k;
        var bits = new System.Collections.Generic.List<int>();
        var outp = new System.Collections.Generic.List<byte>();
        int acc = 0, nbits = 0;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '\r' || c == '\n') continue;                 // the std decoder skips newlines
            if (e.Padding >= 0 && c == (char)e.Padding) break;     // padding ends the data
            int v = inv[c & 0xff];
            if (v < 0) return (null, i);
            acc = (acc << 6) | v; nbits += 6;
            if (nbits >= 8) { nbits -= 8; outp.Add((byte)((acc >> nbits) & 0xff)); }
        }
        return (outp.ToArray(), -1);
    }

    public static GoString EncodeToString(object enc, GoSlice src) =>
        GoString.FromDotNetString(Enc(Bytes(src), (GoBase64)enc));

    public static object?[] DecodeString(object enc, GoString src)
    {
        var (b, off) = Dec(src.ToDotNetString(), (GoBase64)enc);
        if (b == null) return new object?[] { default(GoSlice), CorruptInputErr(off) };
        return new object?[] { Slice(b), null };
    }

    public static long EncodedLen(object enc, long n) =>
        ((GoBase64)enc).Pad ? (n + 2) / 3 * 4 : (n * 8 + 5) / 6;
    public static long DecodedLen(object enc, long n) =>
        ((GoBase64)enc).Pad ? n / 4 * 3 : n * 6 / 8;

    public static void Encode(object enc, GoSlice dst, GoSlice src)
    {
        var by = GoString.FromDotNetString(Enc(Bytes(src), (GoBase64)enc)).Bytes;
        for (int i = 0; i < by.Length && i < dst.Len; i++) dst.Data![dst.Off + i] = (int)by[i];
    }
    public static object?[] Decode(object enc, GoSlice dst, GoSlice src)
    {
        var (b, off) = Dec(GoString.FromBytes(Bytes(src)).ToDotNetString(), (GoBase64)enc);
        if (b == null) return new object?[] { 0L, CorruptInputErr(off) };
        for (int i = 0; i < b.Length && i < dst.Len; i++) dst.Data![dst.Off + i] = (int)b[i];
        return new object?[] { (long)b.Length, null };
    }

    // AppendEncode(dst, src) / AppendDecode(dst, src) (Go 1.22).
    public static GoSlice AppendEncode(object enc, GoSlice dst, GoSlice src)
    {
        var by = GoString.FromDotNetString(Enc(Bytes(src), (GoBase64)enc)).Bytes;
        return Append(dst, by);
    }
    public static object?[] AppendDecode(object enc, GoSlice dst, GoSlice src)
    {
        var (b, off) = Dec(GoString.FromBytes(Bytes(src)).ToDotNetString(), (GoBase64)enc);
        if (b == null) return new object?[] { Append(dst, System.Array.Empty<byte>()), CorruptInputErr(off) };
        return new object?[] { Append(dst, b), null };
    }
    private static GoSlice Append(GoSlice dst, byte[] extra)
    {
        var d = new object?[dst.Len + extra.Length];
        for (int i = 0; i < dst.Len; i++) d[i] = dst.Data![dst.Off + i];
        for (int i = 0; i < extra.Length; i++) d[dst.Len + i] = (int)extra[i];
        return new GoSlice { Data = d, Off = 0, Len = d.Length, Cap = d.Length };
    }

    // base64.CorruptInputError is an int64 offset; Error() reports the byte position.
    private static GoError CorruptInputErr(int off) =>
        new(GoString.FromDotNetString("illegal base64 data at input byte " + off));
    public static GoString CorruptInputError_Error(long n) =>
        GoString.FromDotNetString("illegal base64 data at input byte " + n);

    // base64.NewDecoder(enc, r) io.Reader: eagerly decode the source into a readable snapshot.
    public static object NewDecoder(object enc, object? r)
    {
        var raw = Readers.Drain(r);
        var (b, _) = Dec(System.Text.Encoding.ASCII.GetString(raw), (GoBase64)enc);
        return new GoReader { Data = b ?? System.Array.Empty<byte>() };
    }
}
