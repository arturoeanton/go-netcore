namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>An in-memory reader (strings.Reader / bytes.Reader) over a byte array.</summary>
public sealed class GoReader { public byte[] Data = System.Array.Empty<byte>(); public int Pos; }

/// <summary>strings.NewReader / bytes.NewReader and the shared byte-extraction the
/// io / bufio shims use to consume a reader.</summary>
public static class Readers
{
    public static object NewStringReader(GoString s) => new GoReader { Data = s.Bytes };
    public static object NewBytesReader(GoSlice b)
    {
        var d = new byte[b.Len];
        for (int i = 0; i < b.Len; i++) d[i] = (byte)System.Convert.ToInt64(b.Data![b.Off + i]);
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
        long n = Fmt.WriteTo(dst, System.Text.Encoding.UTF8.GetString(data));
        return new object?[] { (long)data.Length, null };
    }
}
