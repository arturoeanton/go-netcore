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

    public static long EncodedLen(long n) => n * 2;
    public static long DecodedLen(long n) => n / 2;
}
