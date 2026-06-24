namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>A mime/quotedprintable.Writer: RFC 2045 quoted-printable encoder over an
/// io.Writer, ported verbatim from src/mime/quotedprintable/writer.go (76-char lines with
/// soft "=\r\n" breaks, trailing-whitespace escaping).</summary>
public sealed class GoQPWriter { public object? W; public bool Binary; public int I; public byte[] Line = new byte[78]; public bool Cr; }

/// <summary>A mime/quotedprintable.Reader: decodes its source eagerly on first Read.</summary>
public sealed class GoQPReader { public object? Src; public byte[]? Decoded; public int Pos; public object? Err; }

public static class QuotedPrintable
{
    private const int LineMaxLen = 76;
    private const string UpperHex = "0123456789ABCDEF";

    public static object NewWriter(object? w) => new GoQPWriter { W = w };

    // ---- Reader (RFC 2045 decoder, ported from reader.go) --------------------------------
    public static object NewReader(object? r) => new GoQPReader { Src = r };

    // Used by Readers.Drain so io.ReadAll(qpReader) returns the decoded bytes (the error,
    // if any, is dropped by io.ReadAll's runtime path — read directly to observe it).
    internal static byte[] DrainDecoded(GoQPReader r)
    {
        if (r.Decoded == null) { var (dec, err) = DecodeAll(Readers.Drain(r.Src)); r.Decoded = dec; r.Err = err; }
        var rem = new byte[r.Decoded.Length - r.Pos];
        System.Array.Copy(r.Decoded, r.Pos, rem, 0, rem.Length);
        r.Pos = r.Decoded.Length;
        return rem;
    }

    public static object?[] QPReader_Read(object ro, GoSlice p)
    {
        var r = (GoQPReader)ro;
        if (r.Decoded == null) { var (dec, err) = DecodeAll(Readers.Drain(r.Src)); r.Decoded = dec; r.Err = err; }
        int avail = r.Decoded.Length - r.Pos;
        int n = System.Math.Min(p.Len, avail);
        for (int i = 0; i < n; i++) p.Data![p.Off + i] = (int)r.Decoded[r.Pos + i];
        r.Pos += n;
        if (n > 0) return new object?[] { (long)n, null };
        return new object?[] { 0L, r.Err ?? Io.EOFSentinel };
    }

    private static (byte[], object?) DecodeAll(byte[] input)
    {
        var outp = new System.Collections.Generic.List<byte>();
        int pos = 0;
        byte[] line = System.Array.Empty<byte>();
        int lp = 0;
        while (true)
        {
            if (lp >= line.Length)
            {
                if (pos >= input.Length) return (outp.ToArray(), Io.EOFSentinel);
                int idx = System.Array.IndexOf(input, (byte)'\n', pos);
                byte[] whole;
                bool atEof;
                if (idx >= 0) { whole = Sub(input, pos, idx + 1); pos = idx + 1; atEof = false; }
                else { whole = Sub(input, pos, input.Length); pos = input.Length; atEof = true; }
                var pl = ProcessLine(whole, atEof, out var perr);
                if (perr != null) return (outp.ToArray(), perr); // invalid bytes after = (after this line's content)
                line = pl; lp = 0;
                continue;
            }
            byte b = line[lp];
            if (b == '=')
            {
                var (hb, herr) = ReadHexByte(line, lp + 1);
                if (herr != null)
                {
                    if (line.Length - lp >= 2 && line[lp + 1] != '\r' && line[lp + 1] != '\n') b = (byte)'=';
                    else return (outp.ToArray(), herr);
                }
                else { b = hb; lp += 2; }
            }
            else if (b == '\t' || b == '\r' || b == '\n') { }
            else if (b >= 0x80) { }
            else if (b < ' ' || b > '~')
                return (outp.ToArray(), new GoError(GoString.FromDotNetString($"quotedprintable: invalid unescaped byte 0x{b:x2} in body")));
            outp.Add(b); lp++;
        }
    }

    private static byte[] ProcessLine(byte[] whole, bool atEof, out object? perr)
    {
        perr = null;
        bool hasLF = whole.Length >= 1 && whole[^1] == '\n';
        bool hasCR = whole.Length >= 2 && whole[^2] == '\r' && whole[^1] == '\n';
        int end = whole.Length;
        while (end > 0 && (whole[end - 1] == '\n' || whole[end - 1] == '\r' || whole[end - 1] == ' ' || whole[end - 1] == '\t')) end--;
        var line = Sub(whole, 0, end);
        if (line.Length >= 1 && line[^1] == '=')
        {
            int rs = end;
            while (rs < whole.Length && (whole[rs] == ' ' || whole[rs] == '\t')) rs++;
            var rightStripped = Sub(whole, rs, whole.Length);
            line = Sub(line, 0, line.Length - 1);
            bool startsLF = rightStripped.Length >= 1 && rightStripped[0] == '\n';
            bool startsCRLF = rightStripped.Length >= 2 && rightStripped[0] == '\r' && rightStripped[1] == '\n';
            if (!startsLF && !startsCRLF && !(rightStripped.Length == 0 && line.Length > 0 && atEof))
                perr = new GoError(GoString.FromDotNetString($"quotedprintable: invalid bytes after =: {Strconv.Quote(GoString.FromBytes(rightStripped)).ToDotNetString()}"));
        }
        else if (hasLF)
        {
            line = hasCR ? Concat(line, new byte[] { (byte)'\r', (byte)'\n' }) : Concat(line, new byte[] { (byte)'\n' });
        }
        return line;
    }

    private static (byte, object?) ReadHexByte(byte[] v, int off)
    {
        if (v.Length - off < 2) return (0, new GoError(GoString.FromDotNetString("unexpected EOF")));
        int hb = FromHex(v[off]), lb = FromHex(v[off + 1]);
        if (hb < 0) return (0, new GoError(GoString.FromDotNetString($"quotedprintable: invalid hex byte 0x{v[off]:x2}")));
        if (lb < 0) return (0, new GoError(GoString.FromDotNetString($"quotedprintable: invalid hex byte 0x{v[off + 1]:x2}")));
        return ((byte)((hb << 4) | lb), null);
    }
    private static int FromHex(byte b) =>
        b >= '0' && b <= '9' ? b - '0' : b >= 'A' && b <= 'F' ? b - 'A' + 10 : b >= 'a' && b <= 'f' ? b - 'a' + 10 : -1;
    private static byte[] Sub(byte[] s, int from, int to) { var r = new byte[to - from]; System.Array.Copy(s, from, r, 0, to - from); return r; }
    private static byte[] Concat(byte[] a, byte[] b) { var r = new byte[a.Length + b.Length]; a.CopyTo(r, 0); b.CopyTo(r, a.Length); return r; }

    public static bool QPWriter_GetBinary(object wo) => ((GoQPWriter)wo).Binary;
    public static void QPWriter_SetBinary(object wo, bool b) => ((GoQPWriter)wo).Binary = b;

    public static object?[] QPWriter_Write(object wo, GoSlice p)
    {
        var w = (GoQPWriter)wo;
        byte[] pb = ToBytes(p);
        int n = 0;
        for (int i = 0; i < pb.Length; i++)
        {
            byte b = pb[i];
            if ((b >= '!' && b <= '~' && b != '=') || IsWhitespace(b) || (!w.Binary && (b == '\n' || b == '\r')))
                continue;
            if (i > n) { Write(w, pb, n, i); n = i; }
            Encode(w, b);
            n++;
        }
        if (n == pb.Length) return new object?[] { (long)n, null };
        Write(w, pb, n, pb.Length);
        return new object?[] { (long)pb.Length, null };
    }

    public static object? QPWriter_Close(object wo)
    {
        var w = (GoQPWriter)wo;
        CheckLastByte(w);
        Flush(w);
        return null;
    }

    // ---- ported helpers ------------------------------------------------------------------
    private static bool IsWhitespace(byte b) => b == ' ' || b == '\t';

    private static void Write(GoQPWriter w, byte[] p, int from, int to)
    {
        for (int k = from; k < to; k++)
        {
            byte b = p[k];
            if (b == '\n' || b == '\r')
            {
                if (w.Cr && b == '\n') { w.Cr = false; continue; }
                if (b == '\r') w.Cr = true;
                CheckLastByte(w);
                InsertCRLF(w);
                continue;
            }
            if (w.I == LineMaxLen - 1) InsertSoftLineBreak(w);
            w.Line[w.I] = b; w.I++; w.Cr = false;
        }
    }

    private static void Encode(GoQPWriter w, byte b)
    {
        if (LineMaxLen - 1 - w.I < 3) InsertSoftLineBreak(w);
        w.Line[w.I] = (byte)'='; w.Line[w.I + 1] = (byte)UpperHex[b >> 4]; w.Line[w.I + 2] = (byte)UpperHex[b & 0x0f];
        w.I += 3;
    }

    private static void CheckLastByte(GoQPWriter w)
    {
        if (w.I == 0) return;
        byte b = w.Line[w.I - 1];
        if (IsWhitespace(b)) { w.I--; Encode(w, b); }
    }

    private static void Flush(GoQPWriter w)
    {
        var outp = new byte[w.I];
        System.Array.Copy(w.Line, outp, w.I);
        Compress.WriteRaw(w.W, outp);
        w.I = 0;
    }

    private static void InsertSoftLineBreak(GoQPWriter w) { w.Line[w.I] = (byte)'='; w.I++; InsertCRLF(w); }
    private static void InsertCRLF(GoQPWriter w) { w.Line[w.I] = (byte)'\r'; w.Line[w.I + 1] = (byte)'\n'; w.I += 2; Flush(w); }

    private static byte[] ToBytes(GoSlice s)
    {
        var b = new byte[s.Len];
        for (int i = 0; i < s.Len; i++) b[i] = (byte)System.Convert.ToInt64(s.Data![s.Off + i]);
        return b;
    }
}
