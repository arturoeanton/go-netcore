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

    // ---- MIMEHeader (map[string][]string) methods: canonical-key map operations ----
    private static GoSlice SliceOf1(GoString v) =>
        new() { Data = new object?[] { v }, Off = 0, Len = 1, Cap = 1 };
    private static GoSlice Append1(GoSlice old, GoString v)
    {
        int n = old.Data == null ? 0 : old.Len;
        var d = new object?[n + 1];
        for (int i = 0; i < n; i++) d[i] = old.Data![old.Off + i];
        d[n] = v;
        return new GoSlice { Data = d, Off = 0, Len = n + 1, Cap = n + 1 };
    }

    public static GoString MIMEHeader_Get(GoMap? h, GoString key)
    {
        var v = GoMaps.Get(h, CanonicalMIMEHeaderKey(key), null);
        if (v is GoSlice s && s.Len > 0 && s.Data != null) return (GoString)s.Data[s.Off]!;
        return GoString.FromDotNetString("");
    }
    public static GoSlice MIMEHeader_Values(GoMap? h, GoString key) =>
        GoMaps.Get(h, CanonicalMIMEHeaderKey(key), null) is GoSlice s ? s
            : new GoSlice { Data = null, Off = 0, Len = 0, Cap = 0 };
    public static void MIMEHeader_Set(GoMap? h, GoString key, GoString value) =>
        GoMaps.Set(h, CanonicalMIMEHeaderKey(key), SliceOf1(value));
    public static void MIMEHeader_Add(GoMap? h, GoString key, GoString value)
    {
        var ck = CanonicalMIMEHeaderKey(key);
        var old = GoMaps.Get(h, ck, null) is GoSlice es ? es : new GoSlice { Data = null, Off = 0, Len = 0, Cap = 0 };
        GoMaps.Set(h, ck, Append1(old, value));
    }
    public static void MIMEHeader_Del(GoMap? h, GoString key) =>
        GoMaps.Delete(h, CanonicalMIMEHeaderKey(key));
}
