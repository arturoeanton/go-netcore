namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>Shim for a subset of Go's <c>net/textproto</c>.</summary>
public static class Textproto
{
    // CanonicalMIMEHeaderKey("content-type") => "Content-Type": upper-case the first
    // letter and any letter after a '-', lower-case the rest. Non-token bytes leave the
    // key unchanged (returned as-is), matching Go.
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
