namespace GoCLR.Stdlib;

using System.Text;
using GoCLR.Runtime;

public sealed class GoBuffer { public readonly System.Collections.Generic.List<byte> B = new(); public int Pos; }

/// <summary>Shim for bytes.Buffer.</summary>
public static class BytesBuffer
{
    public static object New() => new GoBuffer();
    public static object NewBuffer(GoSlice b) { var g = new GoBuffer(); for (int i = 0; i < b.Len; i++) g.B.Add((byte)System.Convert.ToInt64(b.Data![b.Off + i])); return g; }
    public static object NewBufferString(GoString s) { var g = new GoBuffer(); g.B.AddRange(s.Bytes); return g; }
    private static GoBuffer G(object b) => (GoBuffer)b;

    public static GoString String(object b) { var g = G(b); return GoString.FromBytesOwned(g.B.GetRange(g.Pos, g.B.Count - g.Pos).ToArray()); }
    public static long Len(object b) { var g = G(b); return g.B.Count - g.Pos; }

    // ReadString reads until (and including) the first delim byte, returning
    // (string, error); io.EOF if delim is not found before the end.
    public static object?[] ReadString(object b, int delim)
    {
        var g = G(b);
        byte d = (byte)(delim & 0xff);
        int i = g.Pos;
        while (i < g.B.Count && g.B[i] != d) i++;
        bool found = i < g.B.Count;
        int end = found ? i + 1 : g.B.Count;
        var bytes = g.B.GetRange(g.Pos, end - g.Pos).ToArray();
        g.Pos = end;
        var s = GoString.FromBytesOwned(bytes);
        return new object?[] { s, found ? null : Io.EOFSentinel };
    }
    public static void Reset(object b) { var g = G(b); g.B.Clear(); g.Pos = 0; }

    // Truncate discards all but the first n unread bytes (Go panics if n is out of
    // range; n == 0 is equivalent to Reset).
    public static void Truncate(object b, long n)
    {
        var g = G(b);
        if (n == 0) { g.B.Clear(); g.Pos = 0; return; }
        int keep = g.Pos + (int)n;
        if (keep < g.B.Count) g.B.RemoveRange(keep, g.B.Count - keep);
    }

    // Grow is a capacity hint; the List-backed buffer grows on demand, so no-op.
    public static void Grow(object b, long n) { }

    public static GoSlice Bytes(object b)
    {
        var g = G(b);
        int n = g.B.Count - g.Pos;
        var d = new object?[n];
        for (int i = 0; i < n; i++) d[i] = (int)g.B[g.Pos + i];
        return new GoSlice { Data = d, Off = 0, Len = n, Cap = n };
    }

    public static object?[] WriteString(object b, GoString s) { var by = s.Bytes; G(b).B.AddRange(by); return new object?[] { (long)by.Length, null }; }
    public static object? WriteByte(object b, int c) { G(b).B.Add((byte)c); return null; }
    // WriteTo(w io.Writer) (n int64, err error): drain the unread bytes into w and
    // empty the buffer, like Go. Writes raw bytes to a buffer; otherwise routes text
    // through the shared writer dispatch (stdout/stderr/builder/response writer).
    public static object?[] WriteTo(object b, object? w)
    {
        var g = G(b);
        var data = g.B.GetRange(g.Pos, g.B.Count - g.Pos).ToArray();
        long n = data.Length;
        switch (w)
        {
            case GoBuffer buf: buf.B.AddRange(data); break;
            case GoStringBuilder sb: sb.SB.Append(Encoding.UTF8.GetString(data)); break;
            default: Fmt.WriteTo(w, Encoding.UTF8.GetString(data)); break;
        }
        g.B.Clear();
        g.Pos = 0;
        return new object?[] { n, null };
    }
    public static object?[] WriteRune(object b, int r)
    {
        var rune = System.Text.Rune.IsValid(r) ? new System.Text.Rune(r) : System.Text.Rune.ReplacementChar;
        var by = Encoding.UTF8.GetBytes(rune.ToString());
        G(b).B.AddRange(by);
        return new object?[] { (long)by.Length, null };
    }
    public static object?[] Write(object b, GoSlice p)
    {
        var g = G(b);
        for (int i = 0; i < p.Len; i++) g.B.Add((byte)System.Convert.ToInt32(p.Data[p.Off + i]));
        return new object?[] { (long)p.Len, null };
    }

    // --- read path (advances Pos) ---
    public static object?[] ReadByte(object b)
    {
        var g = G(b);
        if (g.Pos >= g.B.Count) return new object?[] { 0, new GoError(GoString.FromDotNetString("EOF")) };
        return new object?[] { (int)g.B[g.Pos++], null };
    }
    public static object?[] ReadRune(object b)
    {
        var g = G(b);
        if (g.Pos >= g.B.Count) return new object?[] { (int)0, 0L, new GoError(GoString.FromDotNetString("EOF")) };
        byte first = g.B[g.Pos];
        int n = first < 0x80 ? 1 : first >= 0xF0 ? 4 : first >= 0xE0 ? 3 : first >= 0xC0 ? 2 : 1;
        var bytes = new byte[n];
        for (int i = 0; i < n && g.Pos < g.B.Count; i++) bytes[i] = g.B[g.Pos + i];
        g.Pos += n;
        var s = System.Text.Encoding.UTF8.GetString(bytes);
        int cp = s.Length > 0 ? char.ConvertToUtf32(s, 0) : 0xFFFD;
        return new object?[] { cp, (long)n, null };
    }
    public static GoSlice Next(object b, long n)
    {
        var g = G(b);
        int take = (int)System.Math.Min(n, g.B.Count - g.Pos);
        var d = new object?[take];
        for (int i = 0; i < take; i++) d[i] = (int)g.B[g.Pos++];
        return new GoSlice { Data = d, Off = 0, Len = take, Cap = take };
    }

    // Read up to n bytes for binary.Read (advances Pos); returns the bytes.
    internal static byte[] ReadRaw(object b, int n)
    {
        var g = G(b);
        int take = System.Math.Min(n, g.B.Count - g.Pos);
        var r = new byte[take];
        for (int i = 0; i < take; i++) r[i] = g.B[g.Pos++];
        return r;
    }
}
