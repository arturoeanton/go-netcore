namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>Shim for Go's <c>html</c> package (EscapeString / UnescapeString).</summary>
public static partial class Html
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

    // Windows-1252 replacements for numeric references in 0x80..0x9F (Go's replacementTable).
    private static readonly int[] ReplacementTable =
    {
        0x20AC, 0x0081, 0x201A, 0x0192, 0x201E, 0x2026, 0x2020, 0x2021, 0x02C6, 0x2030, 0x0160,
        0x2039, 0x0152, 0x008D, 0x017D, 0x008F, 0x0090, 0x2018, 0x2019, 0x201C, 0x201D, 0x2022,
        0x2013, 0x2014, 0x02DC, 0x2122, 0x0161, 0x203A, 0x0153, 0x009D, 0x017E, 0x0178,
    };
    private const int LongestEntityWithoutSemicolon = 6;

    // html.UnescapeString: a faithful port of Go's UnescapeString/unescapeEntity — full HTML5
    // named references (with the with/without-semicolon distinction), decimal and hex numeric
    // references, and the Windows-1252 / replacement-character fix-ups for invalid code points.
    public static GoString UnescapeString(GoString s)
    {
        string str = s.ToDotNetString();
        if (str.IndexOf('&') < 0) return s;
        var sb = new System.Text.StringBuilder(str.Length);
        int src = 0, n = str.Length;
        while (src < n)
        {
            int i = str.IndexOf('&', src);
            if (i < 0) { sb.Append(str, src, n - src); break; }
            sb.Append(str, src, i - src);
            src = UnescapeEntity(str, sb, i);
        }
        return GoString.FromDotNetString(sb.ToString());
    }

    // src points at '&'. Appends the decoded text (or the literal consumed chars when there is
    // no valid reference) to sb and returns the index just past what was consumed.
    private static int UnescapeEntity(string s, System.Text.StringBuilder sb, int src)
    {
        int n = s.Length;
        if (n - src <= 1) { sb.Append(s[src]); return src + 1; }

        if (s[src + 1] == '#')
        {
            if (n - src <= 3) { sb.Append(s[src]); return src + 1; } // need at least "&#."
            int i = src + 2;
            bool hex = false;
            char c = s[i];
            if (c == 'x' || c == 'X') { hex = true; i++; }
            int x = 0;
            while (i < n)
            {
                c = s[i]; i++;
                if (hex)
                {
                    if (c >= '0' && c <= '9') { x = 16 * x + (c - '0'); continue; }
                    if (c >= 'a' && c <= 'f') { x = 16 * x + (c - 'a' + 10); continue; }
                    if (c >= 'A' && c <= 'F') { x = 16 * x + (c - 'A' + 10); continue; }
                }
                else if (c >= '0' && c <= '9') { x = 10 * x + (c - '0'); continue; }
                if (c != ';') i--;
                break;
            }
            if (i - src <= 3) { sb.Append(s[src]); return src + 1; } // no digits matched
            if (x >= 0x80 && x <= 0x9F) x = ReplacementTable[x - 0x80];
            else if (x == 0 || (x >= 0xD800 && x <= 0xDFFF) || x > 0x10FFFF) x = 0xFFFD;
            sb.Append(char.ConvertFromUtf32(x));
            return i;
        }

        // Named reference: consume the maximal run of letters/digits (then an optional ';').
        int j = src + 1;
        while (j < n)
        {
            char c = s[j]; j++;
            if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) continue;
            if (c != ';') j--;
            break;
        }
        string name = s.Substring(src + 1, j - (src + 1));
        if (name.Length != 0)
        {
            if (Entities.TryGetValue(name, out var val)) { sb.Append(val); return j; }
            // Longest matching prefix that is itself an entity without a trailing ';'.
            int maxLen = System.Math.Min(name.Length - 1, LongestEntityWithoutSemicolon);
            for (int k = maxLen; k > 1; k--)
                if (Entities.TryGetValue(name.Substring(0, k), out var pv)) { sb.Append(pv); return src + 1 + k; }
        }
        sb.Append(s, src, j - src); // no match: emit '&' and the consumed chars verbatim
        return j;
    }
}
