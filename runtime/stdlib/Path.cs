namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>Shim for Go's <c>path</c> and (on a slash OS) <c>path/filepath</c>.
/// Operations are slash-based, matching Go on darwin/linux.</summary>
public static class Path
{
    private static string J(GoSlice elems)
    {
        var parts = new System.Collections.Generic.List<string>();
        for (int i = 0; i < elems.Len; i++)
        {
            string e = ((GoString)elems.Data![elems.Off + i]!).ToDotNetString();
            if (e.Length > 0) parts.Add(e);
        }
        return parts.Count == 0 ? "" : Clean(string.Join("/", parts));
    }
    public static GoString Join(GoSlice elems) => GoString.FromDotNetString(J(elems));

    public static GoString Base(GoString p)
    {
        string s = p.ToDotNetString();
        if (s.Length == 0) return GoString.FromDotNetString(".");
        s = s.TrimEnd('/');
        if (s.Length == 0) return GoString.FromDotNetString("/");
        int i = s.LastIndexOf('/');
        if (i >= 0) s = s.Substring(i + 1);
        return GoString.FromDotNetString(s.Length == 0 ? "/" : s);
    }

    public static GoString Dir(GoString p)
    {
        string s = p.ToDotNetString();
        int i = s.LastIndexOf('/');
        string dir = i < 0 ? "" : s.Substring(0, i + 1);
        return GoString.FromDotNetString(Clean(dir.Length == 0 ? "." : dir));
    }

    public static GoString Ext(GoString p)
    {
        string s = p.ToDotNetString();
        for (int i = s.Length - 1; i >= 0 && s[i] != '/'; i--)
            if (s[i] == '.') return GoString.FromDotNetString(s.Substring(i));
        return GoString.FromDotNetString("");
    }

    public static object?[] Split(GoString p)
    {
        string s = p.ToDotNetString();
        int i = s.LastIndexOf('/');
        return new object?[] { GoString.FromDotNetString(s.Substring(0, i + 1)), GoString.FromDotNetString(s.Substring(i + 1)) };
    }

    public static bool IsAbs(GoString p) { string s = p.ToDotNetString(); return s.Length > 0 && s[0] == '/'; }

    // Go's path.Clean algorithm (lexical).
    public static GoString Clean(GoString p) => GoString.FromDotNetString(Clean(p.ToDotNetString()));
    private static string Clean(string path)
    {
        if (path.Length == 0) return ".";
        bool rooted = path[0] == '/';
        var outp = new System.Text.StringBuilder();
        int r = 0, dotdot = 0;
        if (rooted) { outp.Append('/'); r = 1; dotdot = 1; }
        while (r < path.Length)
        {
            if (path[r] == '/') { r++; }
            else if (path[r] == '.' && (r + 1 == path.Length || path[r + 1] == '/')) { r++; }
            else if (path[r] == '.' && path[r + 1] == '.' && (r + 2 == path.Length || path[r + 2] == '/'))
            {
                r += 2;
                if (outp.Length > dotdot)
                {
                    int w = outp.Length - 1;
                    while (w > dotdot && outp[w] != '/') w--;
                    outp.Length = w;
                }
                else if (!rooted)
                {
                    if (outp.Length > 0) outp.Append('/');
                    outp.Append(".."); dotdot = outp.Length;
                }
            }
            else
            {
                if ((rooted && outp.Length != 1) || (!rooted && outp.Length != 0)) outp.Append('/');
                while (r < path.Length && path[r] != '/') outp.Append(path[r++]);
            }
        }
        return outp.Length == 0 ? "." : outp.ToString();
    }

    // filepath aliases (slash OS).
    public static GoString ToSlash(GoString p) => p;
    public static GoString FromSlash(GoString p) => p;
}
