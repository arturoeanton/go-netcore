namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>Shim for a subset of Go's <c>net/textproto</c>.</summary>
public static class Textproto
{
    // CanonicalMIMEHeaderKey("content-type") => "Content-Type": upper-case the first
    // letter and any letter after a '-', lower-case the rest. Non-token bytes leave the
    // key unchanged (returned as-is), matching Go.
    // TrimString(s): trim leading and trailing ASCII space and tab.
    public static GoString TrimString(GoString sg)
    {
        string s = sg.ToDotNetString();
        int i = 0, j = s.Length;
        while (i < j && (s[i] == ' ' || s[i] == '\t')) i++;
        while (j > i && (s[j - 1] == ' ' || s[j - 1] == '\t')) j--;
        return GoString.FromDotNetString(s.Substring(i, j - i));
    }

    // TrimBytes([]byte): same over a byte slice.
    public static GoSlice TrimBytes(GoSlice b)
    {
        var s = GoString.FromBytes(BytesOf(b));
        var t = TrimString(s);
        return new GoSlice { Data = ToBytes(t.Bytes), Off = 0, Len = t.Bytes.Length, Cap = t.Bytes.Length };
    }
    private static byte[] BytesOf(GoSlice b)
    {
        if (b.Data == null) return System.Array.Empty<byte>();
        var r = new byte[b.Len];
        for (int i = 0; i < b.Len; i++) r[i] = (byte)System.Convert.ToInt64(b.Data[b.Off + i]);
        return r;
    }
    private static object?[] ToBytes(byte[] b)
    {
        var d = new object?[b.Length];
        for (int i = 0; i < b.Length; i++) d[i] = (int)b[i];
        return d;
    }

    public static GoString CanonicalMIMEHeaderKey(GoString sg)
    {
        string s = sg.ToDotNetString();
        var b = s.ToCharArray();
        bool upper = true;
        foreach (char c in b)
        {
            if (!(c == '-' || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')))
                return sg; // not a valid header key — return unchanged
        }
        for (int i = 0; i < b.Length; i++)
        {
            char c = b[i];
            if (upper && c >= 'a' && c <= 'z') c -= (char)32;
            else if (!upper && c >= 'A' && c <= 'Z') c += (char)32;
            b[i] = c;
            upper = c == '-';
        }
        return GoString.FromDotNetString(new string(b));
    }
}
