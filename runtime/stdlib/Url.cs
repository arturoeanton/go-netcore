namespace GoCLR.Stdlib;

using System.Text;
using GoCLR.Runtime;

/// <summary>Shim for the function surface of Go's <c>net/url</c> (escapes; URL
/// struct parsing with field access is deferred — see LIMITATIONS).</summary>
public static class Url
{
    public static GoString QueryEscape(GoString s) => GoString.FromDotNetString(Escape(s.ToDotNetString(), false));
    public static GoString PathEscape(GoString s) => GoString.FromDotNetString(Escape(s.ToDotNetString(), true));

    public static object?[] QueryUnescape(GoString s) => Unescape(s.ToDotNetString(), true);
    public static object?[] PathUnescape(GoString s) => Unescape(s.ToDotNetString(), false);

    private static bool Unreserved(char c) =>
        (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') ||
        c == '-' || c == '_' || c == '.' || c == '~';

    private static string Escape(string s, bool path)
    {
        var sb = new StringBuilder();
        foreach (byte b in Encoding.UTF8.GetBytes(s))
        {
            char c = (char)b;
            if (Unreserved(c)) sb.Append(c);
            else if (!path && c == ' ') sb.Append('+');
            else if (path && (c == '$' || c == '&' || c == '+' || c == ',' || c == '/' || c == ':' || c == ';' || c == '=' || c == '?' || c == '@')) sb.Append('%').Append(b.ToString("X2"));
            else sb.Append('%').Append(b.ToString("X2"));
        }
        return sb.ToString();
    }

    private static object?[] Unescape(string s, bool query)
    {
        var bytes = new System.Collections.Generic.List<byte>();
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '%')
            {
                if (i + 2 >= s.Length) return new object?[] { GoString.FromDotNetString(""), new GoError(GoString.FromDotNetString("invalid URL escape")) };
                try { bytes.Add(System.Convert.ToByte(s.Substring(i + 1, 2), 16)); i += 2; }
                catch { return new object?[] { GoString.FromDotNetString(""), new GoError(GoString.FromDotNetString("invalid URL escape")) }; }
            }
            else if (query && c == '+') bytes.Add((byte)' ');
            else bytes.Add((byte)c);
        }
        return new object?[] { GoString.FromBytes(bytes.ToArray()), null };
    }
}
