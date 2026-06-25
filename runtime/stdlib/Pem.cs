namespace GoCLR.Stdlib;

using System;
using System.Security.Cryptography;
using GoCLR.Runtime;

/// <summary>An encoding/pem.Block.</summary>
[GoShim("encoding/pem.Block")]
public sealed class GoPemBlock
{
    public string Type = "";
    public GoSlice Bytes;
    public GoMap? Headers;
}

/// <summary>Shim for encoding/pem over System.Security.Cryptography.PemEncoding.</summary>
public static class Pem
{
    public static object NewBlock() => new GoPemBlock();
    public static GoString Block_Type(object b) => GoString.FromDotNetString(((GoPemBlock)b).Type);
    public static GoSlice Block_Bytes(object b) => ((GoPemBlock)b).Bytes;
    // Headers is map[string]string (GoMap); the getter/setter must use that exact CLR type,
    // since the compiler emits the field access with a GoMap argument/result (an object?
    // signature yields a MissingMethodException at the call site).
    public static GoMap Block_Headers(object b) => ((GoPemBlock)b).Headers ?? new GoMap { Data = null };
    public static void Block_SetType(object b, GoString v) => ((GoPemBlock)b).Type = v.ToDotNetString();
    public static void Block_SetBytes(object b, GoSlice v) => ((GoPemBlock)b).Bytes = v;
    public static void Block_SetHeaders(object b, GoMap v) => ((GoPemBlock)b).Headers = v;

    // pem.Decode(data) (*Block, rest []byte) — parse the first PEM block; rest is the
    // input after it. Returns (nil, data) when there is no block. Ported from Go so the
    // header lines between the BEGIN line and the base64 body are preserved (.NET's
    // RFC 7468 PemEncoding rejects headers).
    public static object?[] Decode(GoSlice data)
    {
        byte[] raw = Raw(data);
        string text = System.Text.Encoding.ASCII.GetString(raw);
        const string begin = "-----BEGIN ", eol = "-----", end = "-----END ";
        int search = 0;
        while (true)
        {
            int bi = text.IndexOf(begin, search, System.StringComparison.Ordinal);
            // BEGIN must be at the start of the input or a line.
            if (bi < 0) return new object?[] { null, data };
            if (bi != 0 && text[bi - 1] != '\n') { search = bi + begin.Length; continue; }
            int lineStart = bi + begin.Length;
            int nl = text.IndexOf('\n', lineStart);
            if (nl < 0) return new object?[] { null, data };
            string typeLine = text.Substring(lineStart, nl - lineStart).TrimEnd('\r');
            if (!typeLine.EndsWith(eol, System.StringComparison.Ordinal)) { search = bi + begin.Length; continue; }
            string type = typeLine.Substring(0, typeLine.Length - eol.Length);

            var headers = GoMaps.Make();
            int pos = nl + 1;
            // Header lines: "key: value" until a line without a colon (the base64 body).
            while (pos < text.Length)
            {
                int lnl = text.IndexOf('\n', pos);
                if (lnl < 0) lnl = text.Length;
                string line = text.Substring(pos, lnl - pos).TrimEnd('\r');
                int colon = line.IndexOf(':');
                if (colon < 0) break;
                string k = line.Substring(0, colon).Trim();
                string v = line.Substring(colon + 1).Trim();
                headers.Data![GoString.FromDotNetString(k)] = GoString.FromDotNetString(v);
                pos = lnl + 1;
            }
            // Base64 body up to the END trailer.
            string endTrailer = end + type + eol;
            int endIdx = text.IndexOf(endTrailer, pos, System.StringComparison.Ordinal);
            if (endIdx < 0) { search = bi + begin.Length; continue; }
            string b64 = text.Substring(pos, endIdx - pos).Replace("\n", "").Replace("\r", "").Replace(" ", "").Replace("\t", "");
            byte[] der;
            try { der = System.Convert.FromBase64String(b64); }
            catch { search = bi + begin.Length; continue; }
            var block = new GoPemBlock { Type = type, Bytes = Bytes(der), Headers = headers };
            int after = endIdx + endTrailer.Length;
            if (after < text.Length && text[after] == '\r') after++;
            if (after < text.Length && text[after] == '\n') after++;
            int restLen = raw.Length - after;
            var rest = new byte[restLen < 0 ? 0 : restLen];
            if (restLen > 0) System.Array.Copy(raw, after, rest, 0, restLen);
            return new object?[] { block, Bytes(rest) };
        }
    }

    // pem.EncodeToMemory(b) []byte. Matches Go: BEGIN line, headers (Proc-Type first then
    // the rest sorted, with a trailing blank line), base64 wrapped at 64 columns, END line.
    public static GoSlice EncodeToMemory(object b)
    {
        var blk = (GoPemBlock)b;
        var sb = new System.Text.StringBuilder();
        sb.Append("-----BEGIN ").Append(blk.Type).Append("-----\n");
        if (blk.Headers?.Data != null && blk.Headers.Data.Count > 0)
        {
            var keys = new System.Collections.Generic.List<string>();
            bool hasProcType = false;
            foreach (var k in blk.Headers.Data.Keys)
            {
                string ks = ((GoString)k).ToDotNetString();
                if (ks == "Proc-Type") hasProcType = true; else keys.Add(ks);
            }
            if (hasProcType) sb.Append("Proc-Type: ").Append(HeaderVal(blk.Headers, "Proc-Type")).Append('\n');
            keys.Sort(System.StringComparer.Ordinal);
            foreach (var k in keys) sb.Append(k).Append(": ").Append(HeaderVal(blk.Headers, k)).Append('\n');
            sb.Append('\n');
        }
        string b64 = System.Convert.ToBase64String(Raw(blk.Bytes));
        for (int i = 0; i < b64.Length; i += 64)
            sb.Append(b64, i, System.Math.Min(64, b64.Length - i)).Append('\n');
        sb.Append("-----END ").Append(blk.Type).Append("-----\n");
        return Bytes(System.Text.Encoding.ASCII.GetBytes(sb.ToString()));
    }

    private static string HeaderVal(GoMap h, string key)
    {
        if (h.Data != null && h.Data.TryGetValue(GoString.FromDotNetString(key), out var v) && v is GoString gs)
            return gs.ToDotNetString();
        return "";
    }

    // pem.Encode(w, b) error — write the PEM to an io.Writer.
    public static object? Encode(object? w, object b)
    {
        Fmt.WriteTo(w, System.Text.Encoding.ASCII.GetString(Raw(EncodeToMemory(b))));
        return null;
    }

    private static byte[] Raw(GoSlice s)
    {
        var b = new byte[s.Len];
        for (int i = 0; i < s.Len; i++) b[i] = (byte)System.Convert.ToInt64(s.Data![s.Off + i]);
        return b;
    }
    private static GoSlice Bytes(byte[] b)
    {
        var d = new object?[b.Length];
        for (int i = 0; i < b.Length; i++) d[i] = (int)b[i];
        return new GoSlice { Data = d, Off = 0, Len = b.Length, Cap = b.Length };
    }
}
