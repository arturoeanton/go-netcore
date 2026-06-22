namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>Shim for Go's <c>html</c> package (EscapeString / UnescapeString).</summary>
public static class Html
{
    // html.EscapeString escapes <, >, &, ' and " — matching Go's escaper exactly
    // (it uses &#39; and &#34; for the quotes, not &apos;/&quot;).
    public static GoString EscapeString(GoString s)
    {
        var str = s.ToDotNetString();
        var sb = new System.Text.StringBuilder(str.Length);
        foreach (char c in str)
        {
            switch (c)
            {
                case '&': sb.Append("&amp;"); break;
                case '\'': sb.Append("&#39;"); break;
                case '<': sb.Append("&lt;"); break;
                case '>': sb.Append("&gt;"); break;
                case '"': sb.Append("&#34;"); break;
                default: sb.Append(c); break;
            }
        }
        return GoString.FromDotNetString(sb.ToString());
    }

    public static GoString UnescapeString(GoString s)
    {
        var str = s.ToDotNetString()
            .Replace("&#39;", "'").Replace("&#34;", "\"")
            .Replace("&lt;", "<").Replace("&gt;", ">")
            .Replace("&quot;", "\"").Replace("&apos;", "'")
            .Replace("&amp;", "&");
        return GoString.FromDotNetString(str);
    }
}
