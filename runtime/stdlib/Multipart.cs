namespace GoCLR.Stdlib;

using System.Collections.Generic;
using System.Text;
using GoCLR.Runtime;

/// <summary>A mime/multipart.Form: parsed multipart form values + files.</summary>
public sealed class GoMultipartForm
{
    public GoMap? Value; // map[string][]string
    public GoMap? File;  // map[string][]*FileHeader
}

/// <summary>A mime/multipart.FileHeader: a single uploaded form file.</summary>
public sealed class GoFileHeader
{
    public string Filename = "";
    public long Size;
    public GoMap Header = GoMaps.Make();
    public byte[] Content = System.Array.Empty<byte>();
}

/// <summary>A mime/multipart.Reader: an underlying reader + the boundary, parsed lazily.
/// On the first NextPart/NextRawPart the body is drained and split into Parts.</summary>
public sealed class GoMultipartReader { public object? R; public string Boundary = ""; public List<GoMultipartPart>? Parts; public int Idx; }

/// <summary>A mime/multipart.Part: one section of a multipart body — its (canonicalized)
/// header, its raw body bytes with a read cursor, and the parsed Content-Disposition.</summary>
[GoShim("mime/multipart.Part")]
public sealed class GoMultipartPart
{
    public GoMap Header = GoMaps.Make();
    public byte[] Content = System.Array.Empty<byte>();
    public int Pos;
    public string Disposition = "";
    public Dictionary<string, string> DispParams = new();
}

/// <summary>A mime/multipart.Writer: marshals form fields/parts to an underlying writer.</summary>
public sealed class GoMultipartWriter { public object? W; public string Boundary = "goclrFormBoundary7MA4YWxkTrZu0gW"; public bool HasPart; }

/// <summary>Shim for a subset of mime/multipart (Form parsing + FileHeader/File).</summary>
public static class Multipart
{
    // multipart.NewReader(r io.Reader, boundary string) *Reader.
    public static object NewReader(object? r, GoString boundary) =>
        new GoMultipartReader { R = r, Boundary = boundary.ToDotNetString() };

    // (*multipart.Reader).ReadForm(maxMemory int64) (*Form, error): drain the underlying
    // reader and parse the whole multipart body. goclr buffers form bodies in memory, so
    // maxMemory (the spill-to-disk threshold) is not used.
    public static object?[] Reader_ReadForm(object mr, long maxMemory)
    {
        var r = (GoMultipartReader)mr;
        var raw = Readers.Drain(r.R);
        return new object?[] { ParseForm(raw, r.Boundary), null };
    }

    // (*multipart.Reader).NextPart() (*Part, error): the next body part, decoding a
    // quoted-printable Content-Transfer-Encoding (and dropping that header). NextRawPart is
    // the same without the transfer-encoding decode. io.EOF when the parts are exhausted.
    public static object?[] Reader_NextPart(object mr) => Next((GoMultipartReader)mr, true);
    public static object?[] Reader_NextRawPart(object mr) => Next((GoMultipartReader)mr, false);

    private static object?[] Next(GoMultipartReader r, bool decode)
    {
        if (r.Parts == null) { r.Parts = ParseParts(Readers.Drain(r.R), r.Boundary); r.Idx = 0; }
        if (r.Idx >= r.Parts.Count) return new object?[] { null, Io.EOFSentinel };
        var part = r.Parts[r.Idx++];
        if (decode)
        {
            var cte = Textproto.MIMEHeader_Get(part.Header, GoString.FromDotNetString("Content-Transfer-Encoding"));
            if (cte.ToDotNetString() == "quoted-printable")
            {
                part.Content = QuotedPrintable.DecodeBytes(part.Content);
                part.Header.Data!.Remove(GoString.FromDotNetString("Content-Transfer-Encoding"));
            }
        }
        return new object?[] { part, null };
    }

    // (*multipart.Part) methods/fields.
    public static GoMap Part_Header(object po) => ((GoMultipartPart)po).Header;
    public static GoString Part_FormName(object po)
    {
        var p = (GoMultipartPart)po;
        if (p.Disposition != "form-data") return GoString.FromDotNetString("");
        return GoString.FromDotNetString(p.DispParams.TryGetValue("name", out var n) ? n : "");
    }
    public static GoString Part_FileName(object po)
    {
        var p = (GoMultipartPart)po;
        if (!p.DispParams.TryGetValue("filename", out var f) || f.Length == 0) return GoString.FromDotNetString("");
        return GoString.FromDotNetString(FilepathBase(f));
    }
    public static object?[] Part_Read(object po, GoSlice buf)
    {
        var p = (GoMultipartPart)po;
        int avail = p.Content.Length - p.Pos;
        if (avail <= 0) return new object?[] { 0L, Io.EOFSentinel };
        int n = System.Math.Min(buf.Len, avail);
        for (int i = 0; i < n; i++) buf.Data![buf.Off + i] = (int)p.Content[p.Pos + i];
        p.Pos += n;
        return new object?[] { (long)n, null };
    }
    public static object? Part_Close(object po) => null;

    // filepath.Base (unix '/'): strip directory from an uploaded filename (RFC 7578 §4.2).
    private static string FilepathBase(string s)
    {
        if (s.Length == 0) return ".";
        s = s.TrimEnd('/');
        if (s.Length == 0) return "/";
        int i = s.LastIndexOf('/');
        if (i >= 0) s = s.Substring(i + 1);
        return s.Length == 0 ? "/" : s;
    }

    // Split a multipart body into its parts (header + raw body), the streaming-API counterpart
    // of ParseForm. Unlike ParseForm it keeps every part, including those without a name.
    private static List<GoMultipartPart> ParseParts(byte[] raw, string boundary)
    {
        var parts = new List<GoMultipartPart>();
        if (raw.Length == 0 || boundary.Length == 0) return parts;
        byte[] delim = Encoding.ASCII.GetBytes("--" + boundary);
        var starts = new List<int>();
        for (int i = 0; (i = IndexOf(raw, delim, i)) >= 0; i += delim.Length) starts.Add(i);
        for (int s = 0; s < starts.Count; s++)
        {
            int from = starts[s] + delim.Length;
            if (from + 1 < raw.Length && raw[from] == '-' && raw[from + 1] == '-') break; // closing boundary
            if (from + 1 < raw.Length && raw[from] == '\r' && raw[from + 1] == '\n') from += 2;
            int to = s + 1 < starts.Count ? starts[s + 1] : raw.Length;
            if (to >= 2 && raw[to - 2] == '\r' && raw[to - 1] == '\n') to -= 2;
            if (to < from) continue;
            var part = MakePart(raw, from, to);
            if (part != null) parts.Add(part);
        }
        return parts;
    }

    private static GoMultipartPart? MakePart(byte[] raw, int from, int to)
    {
        int hEnd = IndexOf(raw, new byte[] { (byte)'\r', (byte)'\n', (byte)'\r', (byte)'\n' }, from);
        if (hEnd < 0 || hEnd > to) return null;
        string headerBlock = Encoding.ASCII.GetString(raw, from, hEnd - from);
        int bodyStart = hEnd + 4;
        int bodyLen = to - bodyStart;
        if (bodyLen < 0) bodyLen = 0;
        var content = new byte[bodyLen];
        System.Array.Copy(raw, bodyStart, content, 0, bodyLen);
        var part = new GoMultipartPart { Content = content };
        string disp = "";
        foreach (var line in headerBlock.Split("\r\n"))
        {
            int c = line.IndexOf(':');
            if (c < 0) continue;
            string hk = line.Substring(0, c).Trim();
            string hv = line.Substring(c + 1).Trim();
            string ck = Textproto.CanonicalMIMEHeaderKey(GoString.FromDotNetString(hk)).ToDotNetString();
            part.Header.Data![GoString.FromDotNetString(ck)] =
                new GoSlice { Data = new object?[] { GoString.FromDotNetString(hv) }, Off = 0, Len = 1, Cap = 1 };
            if (hk.Equals("Content-Disposition", System.StringComparison.OrdinalIgnoreCase)) disp = hv;
        }
        var segs = disp.Split(';');
        part.Disposition = segs.Length > 0 ? segs[0].Trim().ToLowerInvariant() : "";
        for (int i = 1; i < segs.Length; i++)
        {
            var pseg = segs[i].Trim();
            int eq = pseg.IndexOf('=');
            if (eq < 0) continue;
            string k = pseg.Substring(0, eq).Trim().ToLowerInvariant();
            string v = pseg.Substring(eq + 1).Trim();
            if (v.Length >= 2 && v[0] == '"' && v[^1] == '"') v = v.Substring(1, v.Length - 2);
            part.DispParams[k] = v;
        }
        return part;
    }

    // ---- mime/multipart.Writer ----
    // Go's multipart.NewWriter seeds a random boundary of 30 bytes rendered as 60 hex chars
    // (multipart.randomBoundary); match that format and length (each writer gets a unique one).
    public static object NewWriter(object? w) => new GoMultipartWriter { W = w, Boundary = RandomBoundary() };
    private static string RandomBoundary()
    {
        var buf = new byte[30];
        System.Security.Cryptography.RandomNumberGenerator.Fill(buf);
        var sb = new StringBuilder(60);
        foreach (byte b in buf) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
    public static GoString Writer_Boundary(object mw) => GoString.FromDotNetString(((GoMultipartWriter)mw).Boundary);
    public static GoString Writer_FormDataContentType(object mw) =>
        GoString.FromDotNetString("multipart/form-data; boundary=" + ((GoMultipartWriter)mw).Boundary);
    public static object? Writer_SetBoundary(object mw, GoString b)
    {
        string s = b.ToDotNetString();
        if (s.Length == 0 || s.Length > 70) return new GoError(GoString.FromDotNetString("mime: invalid boundary length"));
        ((GoMultipartWriter)mw).Boundary = s;
        return null;
    }
    // Write a part header. Go separates parts with a leading "\r\n--boundary" after the first
    // part (the first part gets no leading CRLF); the closing boundary likewise gets one.
    private static void WritePartHeader(GoMultipartWriter w, string headerLines)
    {
        string prefix = w.HasPart ? "\r\n--" + w.Boundary + "\r\n" : "--" + w.Boundary + "\r\n";
        w.HasPart = true;
        Compress.WriteRaw(w.W, Encoding.UTF8.GetBytes(prefix + headerLines + "\r\n"));
    }
    private static string EscapeQuotes(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    // (*Writer).WriteField(name, value string) error — a single error result (goclr emits the
    // call expecting one object, so this must return object?, not an object?[] tuple).
    public static object? Writer_WriteField(object mw, GoString name, GoString val)
    {
        var w = (GoMultipartWriter)mw;
        WritePartHeader(w, "Content-Disposition: form-data; name=\"" + EscapeQuotes(name.ToDotNetString()) + "\"\r\n");
        Compress.WriteRaw(w.W, Encoding.UTF8.GetBytes(val.ToDotNetString()));
        return null;
    }
    // CreateFormField(name) / CreateFormFile(name, filename) (io.Writer, error): write the part
    // header, then return the underlying writer so the caller streams the body into it.
    public static object?[] Writer_CreateFormField(object mw, GoString name)
    {
        var w = (GoMultipartWriter)mw;
        WritePartHeader(w, "Content-Disposition: form-data; name=\"" + EscapeQuotes(name.ToDotNetString()) + "\"\r\n");
        return new object?[] { w.W, null };
    }
    public static object?[] Writer_CreateFormFile(object mw, GoString name, GoString filename)
    {
        var w = (GoMultipartWriter)mw;
        WritePartHeader(w, "Content-Disposition: form-data; name=\"" + EscapeQuotes(name.ToDotNetString())
            + "\"; filename=\"" + EscapeQuotes(filename.ToDotNetString()) + "\"\r\nContent-Type: application/octet-stream\r\n");
        return new object?[] { w.W, null };
    }
    public static object?[] Writer_CreatePart(object mw, object? header)
    {
        var w = (GoMultipartWriter)mw;
        var sb = new StringBuilder();
        if (header is GoMap hm && hm.Data != null)
            foreach (var kv in hm.Data)
            {
                string hk = kv.Key is GoString gk ? gk.ToDotNetString() : kv.Key?.ToString() ?? "";
                if (kv.Value is GoSlice vs)
                    for (int i = 0; i < vs.Len; i++)
                    {
                        var v = vs.Data![vs.Off + i];
                        string hv = v is GoString gv ? gv.ToDotNetString() : v?.ToString() ?? "";
                        sb.Append(hk).Append(": ").Append(hv).Append("\r\n");
                    }
            }
        WritePartHeader(w, sb.ToString());
        return new object?[] { w.W, null };
    }
    public static object? Writer_Close(object mw)
    {
        var w = (GoMultipartWriter)mw;
        string s = w.HasPart ? "\r\n--" + w.Boundary + "--\r\n" : "--" + w.Boundary + "--\r\n";
        Compress.WriteRaw(w.W, Encoding.ASCII.GetBytes(s));
        return null;
    }

    // mime/multipart.FileContentDisposition(fieldName, fileName) (Go 1.25).
    public static GoString FileContentDisposition(GoString field, GoString file) =>
        GoString.FromDotNetString("form-data; name=\"" + EscapeQuotes(field.ToDotNetString())
            + "\"; filename=\"" + EscapeQuotes(file.ToDotNetString()) + "\"");

    public static readonly GoError ErrMessageTooLargeSentinel = new(GoString.FromDotNetString("multipart: message too large"));
    public static object ErrMessageTooLarge() => ErrMessageTooLargeSentinel;

    public static object? Form_RemoveAll(object f) => null; // no temp files to clean
    public static GoMap Form_Value(object f) => (GoMap)(((GoMultipartForm)f).Value ?? GoMaps.Make());
    public static GoMap Form_File(object f) => (GoMap)(((GoMultipartForm)f).File ?? GoMaps.Make());

    // *multipart.FileHeader.
    public static GoString FH_Filename(object fh) => GoString.FromDotNetString(((GoFileHeader)fh).Filename);
    public static long FH_Size(object fh) => ((GoFileHeader)fh).Size;
    public static object FH_Header(object fh) => ((GoFileHeader)fh).Header;
    // FileHeader.Open() (multipart.File, error): a reader over the file's buffered bytes.
    public static object?[] FH_Open(object fh) => new object?[] { new GoReader { Data = ((GoFileHeader)fh).Content }, null };

    // multipart.File interface methods (receiver is the GoReader from Open).
    public static object?[] File_Read(object f, GoSlice p) => Io.ReadFull(f, p);
    public static object? File_Close(object f) => null;
    public static object?[] File_Seek(object f, long offset, long whence)
    {
        var gr = (GoReader)f;
        gr.Pos = whence switch { 1 => gr.Pos + (int)offset, 2 => gr.Data.Length + (int)offset, _ => (int)offset };
        if (gr.Pos < 0) gr.Pos = 0;
        return new object?[] { (long)gr.Pos, null };
    }

    /// <summary>Parse a multipart/form-data body into a Form. Values without a filename
    /// become Value entries; parts with a filename become File entries.</summary>
    public static GoMultipartForm ParseForm(byte[] raw, string boundary)
    {
        var form = new GoMultipartForm { Value = GoMaps.Make(), File = GoMaps.Make() };
        if (raw.Length == 0 || boundary.Length == 0) return form;
        byte[] delim = Encoding.ASCII.GetBytes("--" + boundary);

        var starts = new List<int>();
        for (int i = 0; (i = IndexOf(raw, delim, i)) >= 0; i += delim.Length) starts.Add(i);
        for (int s = 0; s < starts.Count; s++)
        {
            int from = starts[s] + delim.Length;
            // Closing boundary "--boundary--": stop.
            if (from + 1 < raw.Length && raw[from] == '-' && raw[from + 1] == '-') break;
            // Skip the CRLF after the delimiter.
            if (from + 1 < raw.Length && raw[from] == '\r' && raw[from + 1] == '\n') from += 2;
            int to = s + 1 < starts.Count ? starts[s + 1] : raw.Length;
            // Strip the trailing CRLF before the next delimiter.
            if (to >= 2 && raw[to - 2] == '\r' && raw[to - 1] == '\n') to -= 2;
            if (to < from) continue;
            ParsePart(raw, from, to, form);
        }
        return form;
    }

    private static void ParsePart(byte[] raw, int from, int to, GoMultipartForm form)
    {
        // Headers end at the first blank line (CRLF CRLF).
        int hEnd = IndexOf(raw, new byte[] { (byte)'\r', (byte)'\n', (byte)'\r', (byte)'\n' }, from);
        if (hEnd < 0 || hEnd > to) return;
        string headerBlock = Encoding.ASCII.GetString(raw, from, hEnd - from);
        int bodyStart = hEnd + 4;
        int bodyLen = to - bodyStart;
        if (bodyLen < 0) bodyLen = 0;

        string name = "", filename = "", contentType = "";
        var fileHdr = GoMaps.Make();
        foreach (var line in headerBlock.Split("\r\n"))
        {
            int c = line.IndexOf(':');
            if (c < 0) continue;
            string hk = line.Substring(0, c).Trim();
            string hv = line.Substring(c + 1).Trim();
            if (hk.Equals("Content-Disposition", System.StringComparison.OrdinalIgnoreCase))
            {
                name = Param(hv, "name");
                filename = Param(hv, "filename");
            }
            else if (hk.Equals("Content-Type", System.StringComparison.OrdinalIgnoreCase))
                contentType = hv;
            fileHdr.Data![GoString.FromDotNetString(Textproto.CanonicalMIMEHeaderKey(GoString.FromDotNetString(hk)).ToDotNetString())] =
                new GoSlice { Data = new object?[] { GoString.FromDotNetString(hv) }, Off = 0, Len = 1, Cap = 1 };
        }
        if (name.Length == 0) return;

        if (filename.Length > 0)
        {
            var content = new byte[bodyLen];
            System.Array.Copy(raw, bodyStart, content, 0, bodyLen);
            var fh = new GoFileHeader { Filename = filename, Size = bodyLen, Header = fileHdr, Content = content };
            Append(form.File!, name, fh);
        }
        else
        {
            string val = Encoding.UTF8.GetString(raw, bodyStart, bodyLen);
            Append(form.Value!, name, GoString.FromDotNetString(val));
        }
    }

    // Append v to the []T stored at key in m (a map[string][]T).
    private static void Append(GoMap m, string key, object v)
    {
        var k = GoString.FromDotNetString(key);
        var existing = m.Data!.TryGetValue(k, out var cur) && cur is GoSlice gs ? gs : new GoSlice { Data = System.Array.Empty<object?>(), Off = 0, Len = 0, Cap = 0 };
        int n = existing.Len;
        var data = new object?[n + 1];
        for (int i = 0; i < n; i++) data[i] = existing.Data![existing.Off + i];
        data[n] = v;
        m.Data![k] = new GoSlice { Data = data, Off = 0, Len = n + 1, Cap = n + 1 };
    }

    // Extract a parameter value (e.g. name="x") from a header field.
    private static string Param(string field, string key)
    {
        foreach (var part in field.Split(';'))
        {
            string p = part.Trim();
            if (p.StartsWith(key + "=", System.StringComparison.OrdinalIgnoreCase))
                return p.Substring(key.Length + 1).Trim('"');
        }
        return "";
    }

    private static int IndexOf(byte[] hay, byte[] needle, int start)
    {
        for (int i = start; i + needle.Length <= hay.Length; i++)
        {
            bool ok = true;
            for (int j = 0; j < needle.Length; j++) if (hay[i + j] != needle[j]) { ok = false; break; }
            if (ok) return i;
        }
        return -1;
    }
}
