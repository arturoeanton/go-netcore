namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>Shim for Go's <c>encoding/hex</c>.</summary>
public static class Hex
{
    public static GoString EncodeToString(GoSlice src)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < src.Len; i++) sb.Append(((byte)System.Convert.ToInt64(src.Data![src.Off + i])).ToString("x2"));
        return GoString.FromDotNetString(sb.ToString());
    }

    public static object?[] DecodeString(GoString s)
    {
        string str = s.ToDotNetString();
        if ((str.Length & 1) != 0)
            return new object?[] { default(GoSlice), new GoError(GoString.FromDotNetString("encoding/hex: odd length hex string")) };
        var d = new object?[str.Length / 2];
        try
        {
            for (int i = 0; i < d.Length; i++) d[i] = (int)System.Convert.ToByte(str.Substring(i * 2, 2), 16);
        }
        catch { return new object?[] { default(GoSlice), new GoError(GoString.FromDotNetString("encoding/hex: invalid byte")) }; }
        return new object?[] { new GoSlice { Data = d, Off = 0, Len = d.Length, Cap = d.Length }, null };
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

    // hex.Decode(dst, src): decode src into dst, returning (bytesWritten, error).
    public static object?[] Decode(GoSlice dst, GoSlice src)
    {
        int n = src.Len / 2;
        try
        {
            for (int i = 0; i < n; i++)
            {
                int hi = FromHex((byte)System.Convert.ToInt64(src.Data![src.Off + i * 2]));
                int lo = FromHex((byte)System.Convert.ToInt64(src.Data![src.Off + i * 2 + 1]));
                if (hi < 0 || lo < 0) return new object?[] { (long)i, new GoError(GoString.FromDotNetString("encoding/hex: invalid byte")) };
                dst.Data![dst.Off + i] = (hi << 4) | lo;
            }
        }
        catch { return new object?[] { (long)0, new GoError(GoString.FromDotNetString("encoding/hex: invalid byte")) }; }
        return new object?[] { (long)n, null };
    }

    private static int FromHex(byte c) => c switch
    {
        >= (byte)'0' and <= (byte)'9' => c - '0',
        >= (byte)'a' and <= (byte)'f' => c - 'a' + 10,
        >= (byte)'A' and <= (byte)'F' => c - 'A' + 10,
        _ => -1,
    };

    public static long EncodedLen(long n) => n * 2;
    public static long DecodedLen(long n) => n / 2;
}
