namespace GoCLR.Stdlib;

using System.Text;
using GoCLR.Runtime;

public sealed class GoBuffer { public readonly System.Collections.Generic.List<byte> B = new(); public int Pos; }

/// <summary>Shim for bytes.Buffer.</summary>
public static class BytesBuffer
{
    public static object New() => new GoBuffer();
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
}
