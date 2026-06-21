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

/// <summary>Shim for a subset of mime/multipart (Form parsing + FileHeader/File).</summary>
public static class Multipart
{
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
