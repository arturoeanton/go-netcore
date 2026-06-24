namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>Shim for Go's <c>encoding/hex</c>.</summary>
public static class Hex
{
    // hex.ErrLength sentinel (== returned for odd-length input, so `err == hex.ErrLength` works).
    public static readonly GoError ErrLengthSentinel = new(GoString.FromDotNetString("encoding/hex: odd length hex string"));
    public static object ErrLength() => ErrLengthSentinel;

    // hex.InvalidByteError(b): Error() is fmt.Sprintf("encoding/hex: invalid byte: %#U", rune(b)).
    public static GoString InvalidByteError_Error(long e) => GoString.FromDotNetString(InvalidByteMsg((int)e));
    private static string InvalidByteMsg(int e) => "encoding/hex: invalid byte: " + SharpU(e);
    private static GoError InvalidByteErr(int b) => new(GoString.FromDotNetString(InvalidByteMsg(b)));
    private static string SharpU(int r)
    {
        string s = "U+" + r.ToString("X4");
        if (r >= 0x20 && r < 0x7f) s += " '" + (char)r + "'"; // unicode.IsPrint for the ASCII bytes hex rejects
        return s;
    }

    public static GoString EncodeToString(GoSlice src)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < src.Len; i++) sb.Append(((byte)System.Convert.ToInt64(src.Data![src.Off + i])).ToString("x2"));
        return GoString.FromDotNetString(sb.ToString());
    }

    public static object?[] DecodeString(GoString s)
    {
        byte[] src = s.Bytes;
        var dst = new object?[src.Length / 2];
        var (n, err) = DecodeBytes(dst, src);
        return new object?[] { new GoSlice { Data = dst, Off = 0, Len = (int)n, Cap = dst.Length }, err };
    }

    // Faithful port of Go's hex.Decode loop: report the invalid byte before the odd-length error.
    private static (long n, object? err) DecodeBytes(object?[] dst, byte[] src)
    {
        int i = 0, j = 1;
        for (; j < src.Length; j += 2)
        {
            int a = FromHex(src[j - 1]);
            int b = FromHex(src[j]);
            if (a < 0) return (i, InvalidByteErr(src[j - 1]));
            if (b < 0) return (i, InvalidByteErr(src[j]));
            dst[i] = (a << 4) | b;
            i++;
        }
        if ((src.Length & 1) == 1)
        {
            if (FromHex(src[j - 1]) < 0) return (i, InvalidByteErr(src[j - 1]));
            return (i, ErrLengthSentinel);
        }
        return (i, null);
    }

    // hex.Encode(dst, src): write the hex encoding of src into dst, returning bytes written.
    private const string HexDigits = "0123456789abcdef";
    public static long Encode(GoSlice dst, GoSlice src)
    {
        int j = 0;
        for (int i = 0; i < src.Len; i++)
        {
            byte b = (byte)System.Convert.ToInt64(src.Data![src.Off + i]);
            dst.Data![dst.Off + j] = (int)(byte)HexDigits[b >> 4];
            dst.Data![dst.Off + j + 1] = (int)(byte)HexDigits[b & 0x0f];
            j += 2;
        }
        return j;
    }

    // hex.Decode(dst, src): decode src into dst, returning (bytesWritten, error). Reports an
    // invalid byte before the odd-length error, as Go does.
    public static object?[] Decode(GoSlice dst, GoSlice src)
    {
        int i = 0, j = 1;
        for (; j < src.Len; j += 2)
        {
            byte p = (byte)System.Convert.ToInt64(src.Data![src.Off + j - 1]);
            byte q = (byte)System.Convert.ToInt64(src.Data![src.Off + j]);
            int a = FromHex(p), b = FromHex(q);
            if (a < 0) return new object?[] { (long)i, InvalidByteErr(p) };
            if (b < 0) return new object?[] { (long)i, InvalidByteErr(q) };
            dst.Data![dst.Off + i] = (a << 4) | b;
            i++;
        }
        if ((src.Len & 1) == 1)
        {
            byte last = (byte)System.Convert.ToInt64(src.Data![src.Off + j - 1]);
            if (FromHex(last) < 0) return new object?[] { (long)i, InvalidByteErr(last) };
            return new object?[] { (long)i, ErrLengthSentinel };
        }
        return new object?[] { (long)i, null };
    }

    private static int FromHex(byte c) => c switch
    {
        >= (byte)'0' and <= (byte)'9' => c - '0',
        >= (byte)'a' and <= (byte)'f' => c - 'a' + 10,
        >= (byte)'A' and <= (byte)'F' => c - 'A' + 10,
        _ => -1,
    };

    // hex.Dump(data): a `hexdump -C` style dump (8-digit offset, 16 hex bytes split 8|8,
    // then the printable ASCII in |...|), matching encoding/hex.Dump byte for byte.
    public static GoString Dump(GoSlice data)
    {
        int len = data.Len;
        if (len == 0) return GoString.FromDotNetString("");
        var sb = new System.Text.StringBuilder();
        byte[] b = new byte[len];
        for (int i = 0; i < len; i++) b[i] = (byte)System.Convert.ToInt64(data.Data![data.Off + i]);
        for (int off = 0; off < len; off += 16)
        {
            sb.Append(off.ToString("x8")).Append("  ");
            for (int i = 0; i < 16; i++)
            {
                if (off + i < len) sb.Append(HexDigits[b[off + i] >> 4]).Append(HexDigits[b[off + i] & 0xf]).Append(' ');
                else sb.Append("   ");
                if (i == 7) sb.Append(' ');
            }
            sb.Append(' '); // separator between the fixed-width hex field and the ASCII gutter
            sb.Append('|');
            for (int i = 0; i < 16 && off + i < len; i++)
            {
                byte c = b[off + i];
                sb.Append(c >= 0x20 && c < 0x7f ? (char)c : '.');
            }
            sb.Append("|\n");
        }
        return GoString.FromDotNetString(sb.ToString());
    }

    public static long EncodedLen(long n) => n * 2;
    public static long DecodedLen(long n) => n / 2;

    // hex.AppendEncode(dst, src): append the hex encoding of src to dst.
    public static GoSlice AppendEncode(GoSlice dst, GoSlice src)
    {
        var outd = new System.Collections.Generic.List<object?>(dst.Len + src.Len * 2);
        for (int i = 0; i < dst.Len; i++) outd.Add(dst.Data![dst.Off + i]);
        for (int i = 0; i < src.Len; i++)
        {
            byte b = (byte)System.Convert.ToInt64(src.Data![src.Off + i]);
            outd.Add((int)(byte)HexDigits[b >> 4]);
            outd.Add((int)(byte)HexDigits[b & 0x0f]);
        }
        return new GoSlice { Data = outd.ToArray(), Off = 0, Len = outd.Count, Cap = outd.Count };
    }

    // hex.AppendDecode(dst, src): decode src and append the bytes to dst.
    public static object?[] AppendDecode(GoSlice dst, GoSlice src)
    {
        var tmp = new object?[src.Len / 2];
        var srcb = new byte[src.Len];
        for (int i = 0; i < src.Len; i++) srcb[i] = (byte)System.Convert.ToInt64(src.Data![src.Off + i]);
        var (n, err) = DecodeBytes(tmp, srcb);
        var outd = new System.Collections.Generic.List<object?>(dst.Len + (int)n);
        for (int i = 0; i < dst.Len; i++) outd.Add(dst.Data![dst.Off + i]);
        for (int i = 0; i < n; i++) outd.Add(tmp[i]);
        return new object?[] { new GoSlice { Data = outd.ToArray(), Off = 0, Len = outd.Count, Cap = outd.Count }, err };
    }

    // hex.NewDecoder(r) io.Reader: eagerly decode r's hex content into a readable byte snapshot.
    public static object NewDecoder(object? r)
    {
        byte[] raw = Readers.Drain(r);
        var dst = new object?[raw.Length / 2];
        var (n, _) = DecodeBytes(dst, raw);
        var outb = new byte[n];
        for (int i = 0; i < n; i++) outb[i] = (byte)System.Convert.ToInt64(dst[i]);
        return new GoReader { Data = outb };
    }
}
