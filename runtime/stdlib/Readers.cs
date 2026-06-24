namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>An in-memory reader (strings.Reader / bytes.Reader / an http response
/// body) over a byte array. Tagged with every Go type it backs so that compiled Go
/// calling an interface method on it (e.g. io.MultiReader's multiReader.Read invoking
/// readers[i].Read, or any user func taking an io.Reader) dispatches to the shim — the
/// strict interface-routing check keys on the static Go type name (strings.Reader,
/// bytes.Reader), not just io.ReadCloser.</summary>
[GoShim("io.ReadCloser")]
[GoShim("strings.Reader")]
[GoShim("bytes.Reader")]
public sealed class GoReader { public byte[] Data = System.Array.Empty<byte>(); public int Pos; public int PrevRune = -1; }

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
    // (*bytes.Reader)/(*strings.Reader).Read(p) (int, error): copy the next bytes into p.
    public static object?[] Reader_Read(object r, GoSlice p)
    {
        var gr = (GoReader)r;
        if (p.Len == 0) return new object?[] { 0L, null };
        if (gr.Pos >= gr.Data.Length) return new object?[] { 0L, Io.EOFSentinel };
        int n = System.Math.Min(p.Len, gr.Data.Length - gr.Pos);
        for (int i = 0; i < n; i++) p.Data![p.Off + i] = (int)gr.Data[gr.Pos + i];
        gr.Pos += n;
        gr.PrevRune = -1; // a non-ReadRune op invalidates UnreadRune
        return new object?[] { (long)n, null };
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
        gr.PrevRune = gr.Pos; // so UnreadRune can restore exactly this read
        gr.Pos += rune.Utf8SequenceLength;
        return new object?[] { rune.Value, (long)rune.Utf8SequenceLength, null };
    }
    // (*strings.Reader / *bytes.Reader).Reset / Seek / ReadAt / WriteTo / UnreadRune.
    public static void Reader_Reset(object r, GoString s) { var gr = (GoReader)r; gr.Data = s.Bytes; gr.Pos = 0; gr.PrevRune = -1; }
    public static void Reader_ResetBytes(object r, GoSlice b) { var gr = (GoReader)r; var d = new byte[b.Len]; for (int i = 0; i < b.Len; i++) d[i] = (byte)System.Convert.ToInt64(b.Data![b.Off + i]); gr.Data = d; gr.Pos = 0; gr.PrevRune = -1; }
    public static object?[] Reader_Seek(object r, long offset, long whence)
    {
        var gr = (GoReader)r;
        long abs;
        switch (whence)
        {
            case 0: abs = offset; break;
            case 1: abs = gr.Pos + offset; break;
            case 2: abs = gr.Data.Length + offset; break;
            default: return new object?[] { 0L, new GoError(GoString.FromDotNetString("strings.Reader.Seek: invalid whence")) };
        }
        if (abs < 0) return new object?[] { 0L, new GoError(GoString.FromDotNetString("strings.Reader.Seek: negative position")) };
        gr.Pos = (int)abs;
        gr.PrevRune = -1;
        return new object?[] { abs, null };
    }
    public static object?[] Reader_ReadAt(object r, GoSlice p, long off)
    {
        var gr = (GoReader)r;
        if (off < 0) return new object?[] { 0L, new GoError(GoString.FromDotNetString("strings.Reader.ReadAt: negative offset")) };
        if (off >= gr.Data.Length) return new object?[] { 0L, Io.EOFSentinel };
        int n = System.Math.Min(p.Len, gr.Data.Length - (int)off);
        for (int i = 0; i < n; i++) p.Data![p.Off + i] = (int)gr.Data[(int)off + i];
        return new object?[] { (long)n, n < p.Len ? Io.EOFSentinel : null };
    }
    public static object?[] Reader_WriteTo(object r, object? w)
    {
        var gr = (GoReader)r;
        int n = gr.Data.Length - gr.Pos;
        if (n <= 0) return new object?[] { 0L, null };
        var rem = new byte[n];
        System.Array.Copy(gr.Data, gr.Pos, rem, 0, n);
        gr.Pos = gr.Data.Length;
        Compress.WriteRaw(w, rem);
        return new object?[] { (long)n, null };
    }
    public static object? Reader_UnreadRune(object r)
    {
        var gr = (GoReader)r;
        if (gr.PrevRune < 0) return new GoError(GoString.FromDotNetString("strings.Reader.UnreadRune: previous operation was not ReadRune"));
        gr.Pos = gr.PrevRune;
        gr.PrevRune = -1;
        return null;
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
            // Any other io.Reader (a compiled multiReader/LimitReader, or a user type): drive
            // its own Read through the callback bridge until EOF.
            case not null when Bridge.HasMethod(r, "Read"): return DrainViaRead(r);
            default: return System.Array.Empty<byte>();
        }
    }

    private static byte[] DrainViaRead(object r)
    {
        var outBytes = new System.Collections.Generic.List<byte>();
        var data = new object?[4096];
        var buf = new GoSlice { Data = data, Off = 0, Len = data.Length, Cap = data.Length };
        while (true)
        {
            var res = Bridge.CallMethod(r, "Read", new object?[] { buf }) as object?[];
            int n = res != null && res.Length > 0 && res[0] != null ? (int)System.Convert.ToInt64(res[0]) : 0;
            object? rerr = res != null && res.Length > 1 ? res[1] : null;
            for (int i = 0; i < n; i++) outBytes.Add((byte)System.Convert.ToInt64(data[i] ?? 0L));
            if (rerr != null || n == 0) break;
        }
        return outBytes.ToArray();
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
