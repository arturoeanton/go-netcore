namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>A mime.WordDecoder (its CharsetReader hook is not modelled — only utf-8,
/// iso-8859-1 and us-ascii charsets decode; others error, as Go does without a reader).</summary>
[GoShim("mime.WordDecoder")]
public sealed class GoWordDecoder { }

/// <summary>Shim for a subset of Go's <c>mime</c> (extension -> content type).</summary>
public static class Mime
{
    // ---- RFC 2047 encoded-word (mime.WordEncoder.Encode), ported from encodedword.go ----
    private const int MaxContentLen = 75 - 10 - 2;        // maxEncodedWordLen - len("=?UTF-8?q?") - len("?=")
    private const int MaxBase64Len = MaxContentLen / 4 * 3; // base64.DecodedLen(maxContentLen)

    public static GoString WordEncoder_Encode(int e, GoString charsetG, GoString sG)
    {
        // Operate on the raw bytes (Go's mime works on s[i] bytes, including non-UTF-8 input
        // like Latin-1, which a UTF-8 round-trip would corrupt).
        byte[] b = sG.Bytes;
        string charset = charsetG.ToDotNetString();
        if (!NeedsEncodingBytes(b)) return sG;
        var buf = new System.Text.StringBuilder();
        OpenWord(buf, charset, e);
        if (e == 'b') BEncode(buf, charset, b, e); else QEncode(buf, charset, b, e);
        buf.Append("?=");
        return GoString.FromDotNetString(buf.ToString());
    }
    private static bool NeedsEncodingBytes(byte[] b)
    {
        foreach (byte c in b) if ((c > '~' || c < ' ') && c != '\t') return true;
        return false;
    }

    private static bool IsUtf8(string charset) => string.Equals(charset, "UTF-8", System.StringComparison.OrdinalIgnoreCase);

    // ---- RFC 2047 decoding (mime.WordDecoder.Decode / DecodeHeader) ----------------------
    public static object WordDecoderZero() => new GoWordDecoder();
    private static readonly GoError ErrInvalidWord = new(GoString.FromDotNetString("mime: invalid RFC 2047 encoded-word"));

    public static object?[] WordDecoder_Decode(object d, GoString wordG)
    {
        string word = wordG.ToDotNetString();
        if (word.Length < 8 || !word.StartsWith("=?") || !word.EndsWith("?=") || CountChar(word, '?') != 4)
            return Fail();
        word = word.Substring(2, word.Length - 4);
        int c1 = word.IndexOf('?');
        string charset = word.Substring(0, c1);
        if (charset.Length == 0) return Fail();
        string rest = word.Substring(c1 + 1);
        int c2 = rest.IndexOf('?');
        string encoding = rest.Substring(0, c2);
        if (encoding.Length != 1) return Fail();
        string text = rest.Substring(c2 + 1);
        var dec = DecodeContent(encoding[0], text);
        if (dec.err != null) return new object?[] { GoString.FromDotNetString(""), dec.err };
        var conv = Convert(charset, dec.bytes!);
        if (conv.err != null) return new object?[] { GoString.FromDotNetString(""), conv.err };
        return new object?[] { GoString.FromBytes(conv.bytes!), null };
    }

    public static object?[] WordDecoder_DecodeHeader(object d, GoString headerG)
    {
        string header = headerG.ToDotNetString();
        int first = header.IndexOf("=?", System.StringComparison.Ordinal);
        if (first == -1) return new object?[] { headerG, null };
        var buf = new System.Collections.Generic.List<byte>();
        AppendUtf8(buf, header.Substring(0, first));
        header = header.Substring(first);
        bool betweenWords = false;
        while (true)
        {
            int start = header.IndexOf("=?", System.StringComparison.Ordinal);
            if (start == -1) break;
            int cur = start + 2;
            int i = header.IndexOf('?', cur);
            if (i == -1) break;
            string charset = header.Substring(cur, i - cur);
            cur = i + 1;
            if (header.Length < cur + 4) break;              // "Q??="
            char encoding = header[cur]; cur++;
            if (header[cur] != '?') break;
            cur++;
            int j = header.IndexOf("?=", cur, System.StringComparison.Ordinal);
            if (j == -1) break;
            string text = header.Substring(cur, j - cur);
            int end = j + 2;
            var dec = DecodeContent(encoding, text);
            if (dec.err != null)
            {
                betweenWords = false;
                AppendUtf8(buf, header.Substring(0, start + 2));
                header = header.Substring(start + 2);
                continue;
            }
            if (start > 0 && (!betweenWords || HasNonWhitespace(header.Substring(0, start))))
                AppendUtf8(buf, header.Substring(0, start));
            var conv = Convert(charset, dec.bytes!);
            if (conv.err != null) return new object?[] { GoString.FromDotNetString(""), conv.err };
            buf.AddRange(conv.bytes!);
            header = header.Substring(end);
            betweenWords = true;
        }
        if (header.Length > 0) AppendUtf8(buf, header);
        return new object?[] { GoString.FromBytes(buf.ToArray()), null };
    }

    private static object?[] Fail() => new object?[] { GoString.FromDotNetString(""), ErrInvalidWord };
    private static int CountChar(string s, char c) { int n = 0; foreach (char x in s) if (x == c) n++; return n; }
    private static void AppendUtf8(System.Collections.Generic.List<byte> buf, string s) => buf.AddRange(System.Text.Encoding.UTF8.GetBytes(s));
    private static bool HasNonWhitespace(string s)
    {
        foreach (char b in s) if (b != ' ' && b != '\t' && b != '\n' && b != '\r') return true;
        return false;
    }

    private static (byte[]? bytes, object? err) DecodeContent(char encoding, string text)
    {
        if (encoding == 'B' || encoding == 'b')
        {
            try { return (System.Convert.FromBase64String(text), null); }
            catch { return (null, ErrInvalidWord); }
        }
        if (encoding == 'Q' || encoding == 'q') return QDecode(text);
        return (null, ErrInvalidWord);
    }
    private static (byte[]? bytes, object? err) QDecode(string s)
    {
        var dec = new System.Collections.Generic.List<byte>(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '_') dec.Add((byte)' ');
            else if (c == '=')
            {
                if (i + 2 >= s.Length) return (null, ErrInvalidWord);
                int hb = FromHex(s[i + 1]), lb = FromHex(s[i + 2]);
                if (hb < 0 || lb < 0) return (null, new GoError(GoString.FromDotNetString($"mime: invalid hex byte 0x{(hb < 0 ? (byte)s[i + 1] : (byte)s[i + 2]):x2}")));
                dec.Add((byte)((hb << 4) | lb));
                i += 2;
            }
            else if ((c <= '~' && c >= ' ') || c == '\n' || c == '\r' || c == '\t') dec.Add((byte)c);
            else return (null, ErrInvalidWord);
        }
        return (dec.ToArray(), null);
    }
    private static int FromHex(char b) =>
        b >= '0' && b <= '9' ? b - '0' : b >= 'A' && b <= 'F' ? b - 'A' + 10 : b >= 'a' && b <= 'f' ? b - 'a' + 10 : -1;

    private static (byte[]? bytes, object? err) Convert(string charset, byte[] content)
    {
        if (string.Equals("utf-8", charset, System.StringComparison.OrdinalIgnoreCase)) return (content, null);
        if (string.Equals("iso-8859-1", charset, System.StringComparison.OrdinalIgnoreCase))
        {
            var o = new System.Collections.Generic.List<byte>(content.Length);
            foreach (byte c in content) AppendRune(o, c); // rune(c)
            return (o.ToArray(), null);
        }
        if (string.Equals("us-ascii", charset, System.StringComparison.OrdinalIgnoreCase))
        {
            var o = new System.Collections.Generic.List<byte>(content.Length);
            foreach (byte c in content) { if (c >= 0x80) o.AddRange(new byte[] { 0xEF, 0xBF, 0xBD }); else o.Add(c); }
            return (o.ToArray(), null);
        }
        return (null, new GoError(GoString.FromDotNetString($"mime: unhandled charset {Strconv.Quote(GoString.FromDotNetString(charset)).ToDotNetString()}")));
    }
    private static void AppendRune(System.Collections.Generic.List<byte> o, int r)
    {
        if (r < 0x80) o.Add((byte)r);
        else if (r < 0x800) { o.Add((byte)(0xC0 | (r >> 6))); o.Add((byte)(0x80 | (r & 0x3F))); }
        else { o.Add((byte)(0xE0 | (r >> 12))); o.Add((byte)(0x80 | ((r >> 6) & 0x3F))); o.Add((byte)(0x80 | (r & 0x3F))); }
    }
    private static void OpenWord(System.Text.StringBuilder buf, string charset, int e)
    {
        buf.Append("=?"); buf.Append(charset); buf.Append('?'); buf.Append((char)e); buf.Append('?');
    }
    private static void SplitWord(System.Text.StringBuilder buf, string charset, int e) { buf.Append("?= "); OpenWord(buf, charset, e); }
    // utf8.DecodeRune length: 1 for ASCII and for any invalid sequence (so a lone high byte
    // advances by one, matching Go), 2-4 for a well-formed multi-byte rune.
    private static int RuneLen(byte[] s, int i)
    {
        byte b = s[i];
        if (b < 0x80) return 1;
        int n = b < 0xC0 ? 1 : b < 0xE0 ? 2 : b < 0xF0 ? 3 : b < 0xF8 ? 4 : 1;
        if (n == 1 || i + n > s.Length) return 1;
        for (int k = 1; k < n; k++) if ((s[i + k] & 0xC0) != 0x80) return 1;
        return n;
    }

    private static void WriteQString(System.Text.StringBuilder buf, byte[] s, int off, int len)
    {
        for (int i = off; i < off + len; i++)
        {
            byte b = s[i];
            if (b == ' ') buf.Append('_');
            else if (b >= '!' && b <= '~' && b != '=' && b != '?' && b != '_') buf.Append((char)b);
            else { buf.Append('='); buf.Append(UpperHex[b >> 4]); buf.Append(UpperHex[b & 0x0f]); }
        }
    }
    private static void QEncode(System.Text.StringBuilder buf, string charset, byte[] s, int e)
    {
        if (!IsUtf8(charset)) { WriteQString(buf, s, 0, s.Length); return; }
        int currentLen = 0, runeLen;
        for (int i = 0; i < s.Length; i += runeLen)
        {
            byte b = s[i];
            int encLen;
            if (b >= ' ' && b <= '~' && b != '=' && b != '?' && b != '_') { runeLen = 1; encLen = 1; }
            else { runeLen = RuneLen(s, i); encLen = 3 * runeLen; }
            if (currentLen + encLen > MaxContentLen) { SplitWord(buf, charset, e); currentLen = 0; }
            WriteQString(buf, s, i, runeLen);
            currentLen += encLen;
        }
    }
    private static void BEncode(System.Text.StringBuilder buf, string charset, byte[] s, int e)
    {
        if (!IsUtf8(charset) || (s.Length + 2) / 3 * 4 <= MaxContentLen) { buf.Append(System.Convert.ToBase64String(s)); return; }
        int currentLen = 0, last = 0, runeLen;
        for (int i = 0; i < s.Length; i += runeLen)
        {
            runeLen = RuneLen(s, i);
            if (currentLen + runeLen <= MaxBase64Len) currentLen += runeLen;
            else
            {
                buf.Append(System.Convert.ToBase64String(s, last, i - last));
                SplitWord(buf, charset, e);
                last = i; currentLen = runeLen;
            }
        }
        buf.Append(System.Convert.ToBase64String(s, last, s.Length - last));
    }

    private static readonly System.Collections.Generic.Dictionary<string, string> Types = new()
    {
        [".html"] = "text/html; charset=utf-8", [".htm"] = "text/html; charset=utf-8",
        [".css"] = "text/css; charset=utf-8", [".js"] = "text/javascript; charset=utf-8",
        [".json"] = "application/json", [".xml"] = "text/xml; charset=utf-8",
        [".txt"] = "text/plain; charset=utf-8", [".png"] = "image/png", [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg", [".gif"] = "image/gif", [".svg"] = "image/svg+xml",
        [".pdf"] = "application/pdf", [".zip"] = "application/zip", [".csv"] = "text/csv; charset=utf-8",
        [".wasm"] = "application/wasm", [".webp"] = "image/webp", [".ico"] = "image/x-icon",
    };
    public static GoString TypeByExtension(GoString ext) =>
        GoString.FromDotNetString(Types.TryGetValue(ext.ToDotNetString().ToLowerInvariant(), out var t) ? t : "");

    // ParseMediaType(v) (mediatype string, params map[string]string, err error):
    // split on ';', lowercase the media type, and collect key=value parameters
    // (unquoting double-quoted values). Returns an error for an empty media type.
    public static object?[] ParseMediaType(GoString v)
    {
        string s = v.ToDotNetString();
        int semi = s.IndexOf(';');
        string mt = (semi < 0 ? s : s.Substring(0, semi)).Trim().ToLowerInvariant();
        if (mt.Length == 0)
            return new object?[] { GoString.FromDotNetString(""), new GoMap { Data = null },
                new GoError("mime: no media type") };

        var data = new System.Collections.Generic.Dictionary<object, object?>();
        int i = semi;
        while (i >= 0 && i < s.Length)
        {
            int next = s.IndexOf(';', i + 1);
            string part = (next < 0 ? s.Substring(i + 1) : s.Substring(i + 1, next - i - 1)).Trim();
            int eq = part.IndexOf('=');
            if (eq > 0)
            {
                string key = part.Substring(0, eq).Trim().ToLowerInvariant();
                string val = part.Substring(eq + 1).Trim();
                if (val.Length >= 2 && val[0] == '"' && val[^1] == '"') val = val.Substring(1, val.Length - 2);
                if (key.Length > 0) data[GoString.FromDotNetString(key)] = GoString.FromDotNetString(val);
            }
            i = next;
        }
        return new object?[] { GoString.FromDotNetString(mt), new GoMap { Data = data }, null };
    }

    // mime.ErrInvalidMediaParameter.
    public static readonly GoError ErrInvalidMediaParameterSentinel = new(GoString.FromDotNetString("mime: invalid media parameter"));
    public static object ErrInvalidMediaParameter() => ErrInvalidMediaParameterSentinel;

    private const string TSpecials = "()<>@,;:\\\"/[]?=";
    private static bool IsTSpecial(char r) => TSpecials.IndexOf(r) >= 0;
    private static bool IsTokenChar(char r) => r > 0x20 && r < 0x7f && !IsTSpecial(r);
    private static bool IsToken(string s)
    {
        if (s.Length == 0) return false;
        foreach (char c in s) if (!IsTokenChar(c)) return false;
        return true;
    }
    private static bool NeedsEncoding(string s)
    {
        foreach (char b in s) if ((b < ' ' || b > '~') && b != '\t') return true;
        return false;
    }
    private const string UpperHex = "0123456789ABCDEF";

    // mime.FormatMediaType(t, param) — faithful port of Go's format (sorts on the ORIGINAL
    // attribute keys, lowercases the type and attribute names but not values, RFC 2231
    // percent-encodes a non-ASCII value with the *=utf-8'' form).
    public static GoString FormatMediaType(GoString tS, GoMap? param)
    {
        var sb = new System.Text.StringBuilder();
        string t = tS.ToDotNetString();
        int slash = t.IndexOf('/');
        if (slash < 0)
        {
            if (!IsToken(t)) return GoString.FromDotNetString("");
            sb.Append(t.ToLowerInvariant());
        }
        else
        {
            string major = t.Substring(0, slash), sub = t.Substring(slash + 1);
            if (!IsToken(major) || !IsToken(sub)) return GoString.FromDotNetString("");
            sb.Append(major.ToLowerInvariant()).Append('/').Append(sub.ToLowerInvariant());
        }
        var pm = ParamPairs(param);
        var attrs = new System.Collections.Generic.List<string>(pm.Keys);
        attrs.Sort(System.StringComparer.Ordinal);
        foreach (var attribute in attrs)
        {
            string value = pm[attribute];
            sb.Append(';').Append(' ');
            if (!IsToken(attribute)) return GoString.FromDotNetString("");
            sb.Append(attribute.ToLowerInvariant());
            bool needEnc = NeedsEncoding(value);
            if (needEnc) sb.Append('*');
            sb.Append('=');
            if (needEnc)
            {
                sb.Append("utf-8''");
                foreach (byte ch in System.Text.Encoding.UTF8.GetBytes(value))
                {
                    if (ch <= ' ' || ch >= 0x7F || ch == '*' || ch == '\'' || ch == '%' || IsTSpecial((char)ch))
                    { sb.Append('%').Append(UpperHex[ch >> 4]).Append(UpperHex[ch & 0x0F]); }
                    else sb.Append((char)ch);
                }
            }
            else if (IsToken(value)) sb.Append(value);
            else
            {
                sb.Append('"');
                foreach (char c in value) { if (c == '"' || c == '\\') sb.Append('\\'); sb.Append(c); }
                sb.Append('"');
            }
        }
        return GoString.FromDotNetString(sb.ToString());
    }

    private static System.Collections.Generic.Dictionary<string, string> ParamPairs(object? param)
    {
        var d = new System.Collections.Generic.Dictionary<string, string>();
        if (param is GoMap m && m.Data != null)
            foreach (var kv in m.Data)
            {
                string k = kv.Key is GoString gk ? gk.ToDotNetString() : kv.Key.ToString() ?? "";
                string v = kv.Value is GoString gv ? gv.ToDotNetString() : kv.Value?.ToString() ?? "";
                d[k] = v;
            }
        return d;
    }

    // mime.AddExtensionType(ext, typ) error.
    public static object? AddExtensionType(GoString extS, GoString typS)
    {
        string ext = extS.ToDotNetString();
        if (ext.Length == 0 || ext[0] != '.')
            return new GoError(GoString.FromDotNetString($"mime: extension {GoQuote(ext)} missing leading dot"));
        string typ = typS.ToDotNetString();
        string head = (typ.IndexOf(';') is var sc && sc >= 0 ? typ.Substring(0, sc) : typ).Trim();
        int sl = head.IndexOf('/');
        if (sl < 0)
            return new GoError(GoString.FromDotNetString("mime: expected slash after first token"));
        var pr = ParseMediaType(typS);
        string justType = ((GoString)pr[0]!).ToDotNetString();
        var pmap = ParamPairs(pr[1]);
        string stored = typ;
        if (justType.StartsWith("text/", System.StringComparison.Ordinal) && !pmap.ContainsKey("charset"))
        {
            pmap["charset"] = "utf-8";
            var gm = new GoMap { Data = new System.Collections.Generic.Dictionary<object, object?>() };
            foreach (var kv in pmap) gm.Data![GoString.FromDotNetString(kv.Key)] = GoString.FromDotNetString(kv.Value);
            stored = FormatMediaType(GoString.FromDotNetString(justType), gm).ToDotNetString();
        }
        Types[ext.ToLowerInvariant()] = stored;
        return null;
    }
    private static string GoQuote(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
