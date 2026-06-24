namespace GoCLR.Stdlib;

using System.Text;
using GoCLR.Runtime;

/// <summary>An encoding/base32.Encoding: a 32-symbol alphabet plus a padding rune
/// (-1 for NoPadding).</summary>
public sealed class GoBase32Encoding { public string Alphabet = ""; public int Padding = '='; }

/// <summary>Shim for encoding/base32 (RFC 4648 Std + Hex, and custom alphabets).</summary>
public static class Base32
{
    private const string Std = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private const string Hex = "0123456789ABCDEFGHIJKLMNOPQRSTUV";
    private static readonly GoBase32Encoding StdEnc = new() { Alphabet = Std, Padding = '=' };
    private static readonly GoBase32Encoding HexEnc = new() { Alphabet = Hex, Padding = '=' };

    public static object StdEncoding() => StdEnc;
    public static object HexEncoding() => HexEnc; // a package var: accessor returns object
    public static object NewEncoding(GoString alpha) => new GoBase32Encoding { Alphabet = alpha.ToDotNetString(), Padding = '=' };
    public static object Enc_WithPadding(object e, int pad) => new GoBase32Encoding { Alphabet = ((GoBase32Encoding)e).Alphabet, Padding = pad };

    private static byte[] Bytes(GoSlice s) { var b = new byte[s.Len]; for (int i = 0; i < s.Len; i++) b[i] = (byte)System.Convert.ToInt64(s.Data![s.Off + i]); return b; }
    private static GoSlice Slice(byte[] b) { var d = new object?[b.Length]; for (int i = 0; i < b.Length; i++) d[i] = (int)b[i]; return new GoSlice { Data = d, Off = 0, Len = b.Length, Cap = b.Length }; }

    private static string EncodeStr(GoBase32Encoding enc, byte[] bytes)
    {
        var sb = new StringBuilder();
        int bits = 0, val = 0;
        foreach (byte b in bytes) { val = (val << 8) | b; bits += 8; while (bits >= 5) { sb.Append(enc.Alphabet[(val >> (bits - 5)) & 31]); bits -= 5; } }
        if (bits > 0) sb.Append(enc.Alphabet[(val << (5 - bits)) & 31]);
        if (enc.Padding >= 0) while (sb.Length % 8 != 0) sb.Append((char)enc.Padding);
        return sb.ToString();
    }
    private static byte[]? DecodeBytes(GoBase32Encoding enc, string s)
    {
        if (enc.Padding >= 0) s = s.TrimEnd((char)enc.Padding);
        var outb = new System.Collections.Generic.List<byte>();
        int bits = 0, val = 0;
        foreach (char c in s)
        {
            int idx = enc.Alphabet.IndexOf(c);
            if (idx < 0) return null;
            val = (val << 5) | idx; bits += 5;
            if (bits >= 8) { outb.Add((byte)((val >> (bits - 8)) & 0xff)); bits -= 8; }
        }
        return outb.ToArray();
    }

    public static GoString EncodeToString(object e, GoSlice src) => GoString.FromDotNetString(EncodeStr((GoBase32Encoding)e, Bytes(src)));
    public static object?[] DecodeString(object e, GoString s)
    {
        var b = DecodeBytes((GoBase32Encoding)e, s.ToDotNetString());
        return b == null
            ? new object?[] { default(GoSlice), new GoError(GoString.FromDotNetString("illegal base32 data at input byte 0")) }
            : new object?[] { Slice(b), null };
    }
    public static long Enc_EncodedLen(object e, long n) => ((GoBase32Encoding)e).Padding < 0 ? (n * 8 + 4) / 5 : (n + 4) / 5 * 8;
    public static long Enc_DecodedLen(object e, long n) => ((GoBase32Encoding)e).Padding < 0 ? n * 5 / 8 : n / 8 * 5;
    public static void Enc_Encode(object e, GoSlice dst, GoSlice src)
    {
        string s = EncodeStr((GoBase32Encoding)e, Bytes(src));
        for (int i = 0; i < s.Length && i < dst.Len; i++) dst.Data![dst.Off + i] = (int)(byte)s[i];
    }
    public static object?[] Enc_Decode(object e, GoSlice dst, GoSlice src)
    {
        var b = DecodeBytes((GoBase32Encoding)e, GoString.FromBytes(Bytes(src)).ToDotNetString());
        if (b == null) return new object?[] { 0L, new GoError(GoString.FromDotNetString("illegal base32 data at input byte 0")) };
        for (int i = 0; i < b.Length && i < dst.Len; i++) dst.Data![dst.Off + i] = (int)b[i];
        return new object?[] { (long)b.Length, null };
    }
    public static GoSlice Enc_AppendEncode(object e, GoSlice dst, GoSlice src)
        => Rt.AppendSlice(dst, Slice(Encoding.ASCII.GetBytes(EncodeStr((GoBase32Encoding)e, Bytes(src)))));
    public static object?[] Enc_AppendDecode(object e, GoSlice dst, GoSlice src)
    {
        var b = DecodeBytes((GoBase32Encoding)e, GoString.FromBytes(Bytes(src)).ToDotNetString());
        return b == null
            ? new object?[] { dst, new GoError(GoString.FromDotNetString("illegal base32 data at input byte 0")) }
            : new object?[] { Rt.AppendSlice(dst, Slice(b)), null };
    }

    // NewEncoder/NewDecoder: the streaming wrappers. The decoder eagerly decodes its source
    // (the common whole-stream case); the encoder accumulates and emits on Close.
    public static object NewDecoder(object e, object? r)
    {
        var raw = Readers.Drain(r);
        var dec = DecodeBytes((GoBase32Encoding)e, Encoding.ASCII.GetString(raw)) ?? System.Array.Empty<byte>();
        return new GoReader { Data = dec };
    }
    public static object NewEncoder(object e, object? w) => new GoBase32Stream { Enc = (GoBase32Encoding)e, W = w };

    // CorruptInputError is an int64 offset; Error() reports the byte position.
    public static GoString CorruptInputError_Error(long n) => GoString.FromDotNetString("illegal base32 data at input byte " + n);
}

/// <summary>A streaming base32 encoder (io.WriteCloser): buffers writes, emits the
/// encoding to the underlying writer on Close.</summary>
public sealed class GoBase32Stream
{
    public GoBase32Encoding Enc = null!;
    public object? W;
    public readonly System.Collections.Generic.List<byte> Buf = new();
}
