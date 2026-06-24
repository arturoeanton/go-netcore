namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>A mime/quotedprintable.Writer: RFC 2045 quoted-printable encoder over an
/// io.Writer, ported verbatim from src/mime/quotedprintable/writer.go (76-char lines with
/// soft "=\r\n" breaks, trailing-whitespace escaping).</summary>
public sealed class GoQPWriter { public object? W; public bool Binary; public int I; public byte[] Line = new byte[78]; public bool Cr; }

public static class QuotedPrintable
{
    private const int LineMaxLen = 76;
    private const string UpperHex = "0123456789ABCDEF";

    public static object NewWriter(object? w) => new GoQPWriter { W = w };

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
