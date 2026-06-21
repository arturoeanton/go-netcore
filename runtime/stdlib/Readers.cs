namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>An in-memory reader (strings.Reader / bytes.Reader) over a byte array.</summary>
public sealed class GoReader { public byte[] Data = System.Array.Empty<byte>(); public int Pos; }

/// <summary>strings.NewReader / bytes.NewReader and the shared byte-extraction the
/// io / bufio shims use to consume a reader.</summary>
public static class Readers
{
    public static object NewStringReader(GoString s) => new GoReader { Data = s.Bytes };

    // strings.Reader / bytes.Reader methods.
    public static object?[] Reader_ReadByte(object r)
    {
        var gr = (GoReader)r;
        if (gr.Pos >= gr.Data.Length) return new object?[] { (int)0, Io.EOFSentinel };
        return new object?[] { (int)gr.Data[gr.Pos++], null };
    }
    public static object? Reader_UnreadByte(object r)
    {
        var gr = (GoReader)r;
        if (gr.Pos > 0) gr.Pos--;
        return null;
    }
    public static long Reader_Len(object r) { var gr = (GoReader)r; return gr.Data.Length - gr.Pos; }
    public static long Reader_Size(object r) => ((GoReader)r).Data.Length;
    public static object?[] Reader_ReadRune(object r)
    {
        var gr = (GoReader)r;
        if (gr.Pos >= gr.Data.Length) return new object?[] { (int)0, 0L, Io.EOFSentinel };
        var rem = new byte[gr.Data.Length - gr.Pos];
        System.Array.Copy(gr.Data, gr.Pos, rem, 0, rem.Length);
        var s = System.Text.Encoding.UTF8.GetString(rem);
        var rune = System.Text.Rune.GetRuneAt(s, 0);
        gr.Pos += rune.Utf8SequenceLength;
        return new object?[] { rune.Value, (long)rune.Utf8SequenceLength, null };
    }
    public static object NewBytesReader(GoSlice b)
    {
        var d = new byte[b.Len];
        for (int i = 0; i < b.Len; i++) d[i] = (byte)System.Convert.ToInt64(b.Data![b.Off + i]);
        return new GoReader { Data = d };
    }

    // io.LimitReader(r, n) io.Reader — read at most n bytes. goclr drains readers
    // eagerly, so this truncates the drained content to n bytes.
    public static object LimitReader(object? r, long n)
    {
        var all = Drain(r);
        int len = n < 0 ? 0 : (n < all.Length ? (int)n : all.Length);
        var d = new byte[len];
        System.Array.Copy(all, d, len);
        return new GoReader { Data = d };
    }

    // Drain a reader the runtime understands to its remaining bytes.
    internal static byte[] Drain(object? r)
    {
        switch (r)
        {
            case GoReader gr: { var rem = new byte[gr.Data.Length - gr.Pos]; System.Array.Copy(gr.Data, gr.Pos, rem, 0, rem.Length); gr.Pos = gr.Data.Length; return rem; }
            case GoBuffer gb: { int n = gb.B.Count - gb.Pos; var rem = new byte[n]; for (int i = 0; i < n; i++) rem[i] = gb.B[gb.Pos + i]; gb.Pos = gb.B.Count; return rem; }
            case GoStringBuilder sb: return System.Text.Encoding.UTF8.GetBytes(sb.SB.ToString());
            case GoFile f when f.IsStdin: { string? all = System.Console.In.ReadToEnd(); return System.Text.Encoding.UTF8.GetBytes(all ?? ""); }
            default: return System.Array.Empty<byte>();
        }
    }

    private static GoSlice ByteSlice(byte[] b)
    {
        var d = new object?[b.Length];
        for (int i = 0; i < b.Length; i++) d[i] = (int)b[i];
        return new GoSlice { Data = d, Off = 0, Len = b.Length, Cap = b.Length };
    }

    // io.ReadAll(r) -> ([]byte, error)
    public static object?[] ReadAll(object? r) => new object?[] { ByteSlice(Drain(r)), null };

    // io.Copy(dst, src) -> (n, error): drain src, write to dst (a writer the runtime knows).
    public static object?[] Copy(object? dst, object? src)
    {
        byte[] data = Drain(src);
        Compress.WriteRaw(dst, data); // binary-safe (files, buffers, readers)
        return new object?[] { (long)data.Length, null };
    }
}
