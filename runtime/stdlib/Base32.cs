namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>Shim for encoding/base32 StdEncoding (RFC 4648).</summary>
public static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static object StdEncoding() => new object();

    public static GoString EncodeToString(object _, GoSlice src)
    {
        var bytes = new byte[src.Len];
        for (int i = 0; i < src.Len; i++) bytes[i] = (byte)System.Convert.ToInt64(src.Data![src.Off + i]);
        var sb = new System.Text.StringBuilder();
        int bits = 0, val = 0;
        foreach (byte b in bytes)
        {
            val = (val << 8) | b; bits += 8;
            while (bits >= 5) { sb.Append(Alphabet[(val >> (bits - 5)) & 31]); bits -= 5; }
        }
        if (bits > 0) sb.Append(Alphabet[(val << (5 - bits)) & 31]);
        while (sb.Length % 8 != 0) sb.Append('=');
        return GoString.FromDotNetString(sb.ToString());
    }

    public static object?[] DecodeString(object _, GoString s)
    {
        string str = s.ToDotNetString().TrimEnd('=');
        var outb = new System.Collections.Generic.List<byte>();
        int bits = 0, val = 0;
        foreach (char c in str)
        {
            int idx = Alphabet.IndexOf(char.ToUpperInvariant(c));
            if (idx < 0) return new object?[] { default(GoSlice), new GoError(GoString.FromDotNetString("illegal base32 data")) };
            val = (val << 5) | idx; bits += 5;
            if (bits >= 8) { outb.Add((byte)((val >> (bits - 8)) & 0xff)); bits -= 8; }
        }
        var d = new object?[outb.Count];
        for (int i = 0; i < outb.Count; i++) d[i] = (int)outb[i];
        return new object?[] { new GoSlice { Data = d, Off = 0, Len = d.Length, Cap = d.Length }, null };
    }
}
