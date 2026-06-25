namespace GoCLR.Stdlib;

using System.Collections.Generic;
using GoCLR.Runtime;

/// <summary>A net/textproto.Reader over the underlying reader's drained bytes, read
/// line-by-line from a cursor (CRLF-aware), matching Go's bufio-backed Reader.</summary>
public sealed class GoTextprotoReader { public byte[] Data = System.Array.Empty<byte>(); public int Pos; }

/// <summary>A net/textproto.Writer: writes lines / dot-encoded bodies to an underlying writer.</summary>
public sealed class GoTextprotoWriter { public object? W; }

/// <summary>net/textproto.Error: a numeric protocol response code + message.</summary>
[GoShim("net/textproto.Error")]
public sealed class GoTextprotoError : IGoError
{
    public long Code;
    public string Msg = "";
    public GoString Error() => GoString.FromDotNetString(Code.ToString("D3") + " " + Msg);
}

/// <summary>The io.WriteCloser from (*Writer).DotWriter: dot-encodes written lines (escapes a
/// leading '.', terminates with ".\r\n" on Close), a faithful port of Go's dotWriter.</summary>
[GoShim("net/textproto.dotWriter")]
public sealed class GoTextprotoDotWriter : IGoWriter
{
    public object? W;
    public int State; // 0=beginLine, 1=data, 2=cr
    public void GoWrite(byte[] data) => Textproto.DotWriter_Write(this, Textproto.RawSlice(data));
}

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

    // ---- net/textproto.Reader / Writer ------------------------------------------------
    internal static GoSlice RawSlice(byte[] b)
    {
        var d = new object?[b.Length];
        for (int i = 0; i < b.Length; i++) d[i] = (int)b[i];
        return new GoSlice { Data = d, Off = 0, Len = b.Length, Cap = b.Length };
    }

    public static object NewReader(object? r) => new GoTextprotoReader { Data = Readers.Drain(r) };
    public static object NewWriter(object? w) => new GoTextprotoWriter { W = w };

    // Read one line from the cursor, stripping a trailing CRLF (or lone LF). null at EOF.
    private static byte[]? RawLine(GoTextprotoReader r)
    {
        if (r.Pos >= r.Data.Length) return null;
        int start = r.Pos;
        int nl = System.Array.IndexOf(r.Data, (byte)'\n', start);
        int end = nl < 0 ? r.Data.Length : nl;
        r.Pos = nl < 0 ? r.Data.Length : nl + 1;
        int lineEnd = (nl >= 0 && end > start && r.Data[end - 1] == (byte)'\r') ? end - 1 : end;
        var line = new byte[lineEnd - start];
        System.Array.Copy(r.Data, start, line, 0, line.Length);
        return line;
    }

    private static byte[] TrimWs(byte[] b)
    {
        int i = 0, j = b.Length;
        while (i < j && (b[i] == ' ' || b[i] == '\t')) i++;
        while (j > i && (b[j - 1] == ' ' || b[j - 1] == '\t')) j--;
        if (i == 0 && j == b.Length) return b;
        var r = new byte[j - i];
        System.Array.Copy(b, i, r, 0, j - i);
        return r;
    }

    public static object?[] Reader_ReadLine(object ro)
    {
        var line = RawLine((GoTextprotoReader)ro);
        return line == null
            ? new object?[] { GoString.FromDotNetString(""), Io.EOFSentinel }
            : new object?[] { GoString.FromBytes(line), null };
    }
    public static object?[] Reader_ReadLineBytes(object ro)
    {
        var line = RawLine((GoTextprotoReader)ro);
        return line == null
            ? new object?[] { default(GoSlice), Io.EOFSentinel }
            : new object?[] { RawSlice(line), null };
    }

    // Read a (possibly continuation-folded) line: a following line beginning with space or
    // tab is appended after trimming, joined by a single space, matching Go.
    private static byte[]? ContinuedLine(GoTextprotoReader r)
    {
        var line = RawLine(r);
        if (line == null) return null;
        if (line.Length == 0) return line; // blank line: no continuation
        var buf = new List<byte>(TrimWs(line));
        while (r.Pos < r.Data.Length && (r.Data[r.Pos] == ' ' || r.Data[r.Pos] == '\t'))
        {
            while (r.Pos < r.Data.Length && (r.Data[r.Pos] == ' ' || r.Data[r.Pos] == '\t')) r.Pos++;
            var nxt = RawLine(r);
            if (nxt == null) break;
            buf.Add((byte)' ');
            buf.AddRange(TrimWs(nxt));
        }
        return buf.ToArray();
    }
    public static object?[] Reader_ReadContinuedLine(object ro)
    {
        var line = ContinuedLine((GoTextprotoReader)ro);
        return line == null
            ? new object?[] { GoString.FromDotNetString(""), Io.EOFSentinel }
            : new object?[] { GoString.FromBytes(line), null };
    }
    public static object?[] Reader_ReadContinuedLineBytes(object ro)
    {
        var line = ContinuedLine((GoTextprotoReader)ro);
        return line == null
            ? new object?[] { default(GoSlice), Io.EOFSentinel }
            : new object?[] { RawSlice(line), null };
    }

    private static bool ValidHeaderKey(byte[] k)
    {
        if (k.Length == 0) return false;
        foreach (byte c in k)
        {
            // RFC 7230 token chars.
            bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')
                || "!#$%&'*+-.^_`|~".IndexOf((char)c) >= 0;
            if (!ok) return false;
        }
        return true;
    }

    public static object?[] Reader_ReadMIMEHeader(object ro)
    {
        var r = (GoTextprotoReader)ro;
        var m = GoMaps.Make();
        // The first header line cannot start with leading whitespace.
        if (r.Pos < r.Data.Length && (r.Data[r.Pos] == ' ' || r.Data[r.Pos] == '\t'))
        {
            var line = RawLine(r) ?? System.Array.Empty<byte>();
            return new object?[] { m, new GoError(GoString.FromDotNetString(
                "malformed MIME header initial line: " + GoString.FromBytes(line).ToDotNetString())) };
        }
        while (true)
        {
            var kv = ContinuedLine(r);
            if (kv == null) return new object?[] { m, Io.EOFSentinel };
            if (kv.Length == 0) return new object?[] { m, null }; // blank line terminates
            int ci = System.Array.IndexOf(kv, (byte)':');
            byte[] keyBytes = ci < 0 ? kv : kv[..ci];
            if (ci < 0 || !ValidHeaderKey(keyBytes))
                return new object?[] { m, new GoError(GoString.FromDotNetString(
                    "malformed MIME header line: " + GoString.FromBytes(kv).ToDotNetString())) };
            string key = CanonicalMIMEHeaderKey(GoString.FromBytes(keyBytes)).ToDotNetString();
            int vs = ci + 1;
            while (vs < kv.Length && (kv[vs] == ' ' || kv[vs] == '\t')) vs++;
            string value = GoString.FromBytes(kv[vs..]).ToDotNetString();
            MIMEHeader_Add(m, GoString.FromDotNetString(key), GoString.FromDotNetString(value));
        }
    }

    // ---- Numeric response lines (SMTP/FTP style) ----
    private static (long code, bool cont, string msg, object? err) ParseCodeLine(string line, long expectCode)
    {
        if (line.Length < 4 || (line[3] != ' ' && line[3] != '-'))
            return (0, false, "", new GoError(GoString.FromDotNetString("short response: " + line)));
        bool cont = line[3] == '-';
        if (!int.TryParse(line.Substring(0, 3), System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out int code) || code < 100)
            return (0, false, "", new GoError(GoString.FromDotNetString("invalid response code: " + line)));
        string message = line.Substring(4);
        object? err = null;
        if ((1 <= expectCode && expectCode < 10 && code / 100 != expectCode) ||
            (10 <= expectCode && expectCode < 100 && code / 10 != expectCode) ||
            (100 <= expectCode && expectCode < 1000 && code != expectCode))
            err = new GoTextprotoError { Code = code, Msg = message };
        return (code, cont, message, err);
    }
    private static (long, bool, string, object?) ReadCodeLineInner(GoTextprotoReader r, long expectCode)
    {
        var lr = RawLine(r);
        if (lr == null) return (0, false, "", Io.EOFSentinel);
        return ParseCodeLine(GoString.FromBytes(lr).ToDotNetString(), expectCode);
    }
    public static object?[] Reader_ReadCodeLine(object ro, long expectCode)
    {
        var (code, cont, msg, err) = ReadCodeLineInner((GoTextprotoReader)ro, expectCode);
        if (err == null && cont)
            err = new GoError(GoString.FromDotNetString("unexpected multi-line response: " + msg));
        return new object?[] { code, GoString.FromDotNetString(msg), err };
    }
    public static object?[] Reader_ReadResponse(object ro, long expectCode)
    {
        var r = (GoTextprotoReader)ro;
        var (code, cont, msg, err) = ReadCodeLineInner(r, expectCode);
        while (cont)
        {
            var lr = RawLine(r);
            if (lr == null) { err ??= Io.ErrUnexpectedEOFSentinel; break; }
            string line = GoString.FromBytes(lr).ToDotNetString();
            var (code2, cont2, more, err2) = ParseCodeLine(line, 0);
            if (err2 != null || code2 != code)
            {
                msg += "\n" + line;
                cont = true;
                continue;
            }
            msg += "\n" + more;
            cont = cont2;
        }
        return new object?[] { code, GoString.FromDotNetString(msg), err };
    }

    // ---- Dot-encoded bodies ----
    private static byte[] DecodeDot(GoTextprotoReader r, out bool truncated)
    {
        truncated = false;
        var outp = new List<byte>();
        while (true)
        {
            var line = RawLine(r);
            if (line == null) { truncated = true; break; }
            if (line.Length > 0 && line[0] == '.')
            {
                if (line.Length == 1) break;       // "." terminates the body
                line = line[1..];                  // unescape one leading dot
            }
            outp.AddRange(line);
            outp.Add((byte)'\n');
        }
        return outp.ToArray();
    }
    public static object DotReader(object ro) => new GoReader { Data = DecodeDot((GoTextprotoReader)ro, out _) };
    public static object?[] Reader_ReadDotBytes(object ro)
    {
        var bytes = DecodeDot((GoTextprotoReader)ro, out bool trunc);
        return new object?[] { RawSlice(bytes), trunc ? Io.ErrUnexpectedEOFSentinel : null };
    }
    public static object?[] Reader_ReadDotLines(object ro)
    {
        var r = (GoTextprotoReader)ro;
        var v = new List<object?>();
        object? err = null;
        while (true)
        {
            var line = RawLine(r);
            if (line == null) { err = Io.ErrUnexpectedEOFSentinel; break; }
            if (line.Length > 0 && line[0] == '.')
            {
                if (line.Length == 1) break;
                line = line[1..];
            }
            v.Add(GoString.FromBytes(line));
        }
        return new object?[] { new GoSlice { Data = v.ToArray(), Off = 0, Len = v.Count, Cap = v.Count }, err };
    }

    // (*Writer).PrintfLine(format, args...) — formatted line terminated by CRLF.
    public static object? Writer_PrintfLine(object wo, GoString format, GoSlice args)
    {
        var w = (GoTextprotoWriter)wo;
        string s = Fmt.Sprintf(format, args).ToDotNetString();
        Compress.WriteRaw(w.W, System.Text.Encoding.UTF8.GetBytes(s + "\r\n"));
        Bufio.FlushIfBuffered(w.W); // Go's PrintfLine flushes the underlying *bufio.Writer
        return null;
    }

    // (*Writer).DotWriter() io.WriteCloser — port of dotWriter's state machine.
    public static object Writer_DotWriter(object wo) => new GoTextprotoDotWriter { W = ((GoTextprotoWriter)wo).W };
    public static object?[] DotWriter_Write(object dwo, GoSlice b)
    {
        var d = (GoTextprotoDotWriter)dwo;
        var outp = new List<byte>();
        for (int i = 0; i < b.Len; i++)
        {
            byte c = (byte)System.Convert.ToInt64(b.Data![b.Off + i]);
            switch (d.State)
            {
                case 0: // beginLine
                    d.State = 1;
                    if (c == '.') outp.Add((byte)'.'); // escape leading dot
                    goto case 1;
                case 1: // data
                    if (c == '\r') d.State = 2;
                    else if (c == '\n') d.State = 0;
                    break;
                case 2: // cr
                    d.State = c == '\n' ? 0 : 1;
                    break;
            }
            outp.Add(c);
        }
        Compress.WriteRaw(d.W, outp.ToArray());
        return new object?[] { (long)b.Len, null };
    }
    public static object? DotWriter_Close(object dwo)
    {
        var d = (GoTextprotoDotWriter)dwo;
        string tail = d.State switch { 2 => "\n", 0 => "", _ => "\r\n" };
        Compress.WriteRaw(d.W, System.Text.Encoding.ASCII.GetBytes(tail + ".\r\n"));
        Bufio.FlushIfBuffered(d.W); // Go's dotWriter.Close flushes the underlying *bufio.Writer
        return null;
    }

    // net/textproto.Error (struct) accessors + Error().
    public static object Error_Zero() => new GoTextprotoError();
    public static long Error_Code(object e) => ((GoTextprotoError)e).Code;
    public static GoString Error_Msg(object e) => GoString.FromDotNetString(((GoTextprotoError)e).Msg);
    public static void Error_SetCode(object e, long v) => ((GoTextprotoError)e).Code = v;
    public static void Error_SetMsg(object e, GoString v) => ((GoTextprotoError)e).Msg = v.ToDotNetString();
    public static GoString Error_Error(object e) => ((GoTextprotoError)e).Error();

    // net/textproto.ProtocolError is a named string; Error() returns the string itself.
    public static GoString ProtocolError_Error(GoString s) => s;
}
