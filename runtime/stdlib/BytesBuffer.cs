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
    public static void Reset(object b) { var g = G(b); g.B.Clear(); g.Pos = 0; }

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
