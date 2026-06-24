namespace GoCLR.Stdlib;

using System.IO;
using System.IO.Compression;
using GoCLR.Runtime;

/// <summary>A gzip/zlib/flate writer that buffers and emits on Close.</summary>
public sealed class GoCompWriter { public object? W; public MemoryStream Mem = new(); public Stream Z = null!; public int Kind; }

/// <summary>Shim for compress/gzip, compress/zlib, compress/flate (.NET streams).</summary>
public static class Compress
{
    // compress/gzip + compress/zlib sentinel errors.
    public static readonly GoError GzipErrChecksumSentinel = new(GoString.FromDotNetString("gzip: invalid checksum"));
    public static object GzipErrChecksum() => GzipErrChecksumSentinel;
    public static readonly GoError GzipErrHeaderSentinel = new(GoString.FromDotNetString("gzip: invalid header"));
    public static object GzipErrHeader() => GzipErrHeaderSentinel;
    public static readonly GoError ZlibErrChecksumSentinel = new(GoString.FromDotNetString("zlib: invalid checksum"));
    public static object ZlibErrChecksum() => ZlibErrChecksumSentinel;
    public static readonly GoError ZlibErrHeaderSentinel = new(GoString.FromDotNetString("zlib: invalid header"));
    public static object ZlibErrHeader() => ZlibErrHeaderSentinel;
    public static readonly GoError ZlibErrDictionarySentinel = new(GoString.FromDotNetString("zlib: invalid dictionary"));
    public static object ZlibErrDictionary() => ZlibErrDictionarySentinel;

    private static Stream Wrap(MemoryStream mem, int kind) => kind switch
    {
        1 => new ZLibStream(mem, CompressionMode.Compress, true),
        2 => new DeflateStream(mem, CompressionMode.Compress, true),
        _ => new GZipStream(mem, CompressionMode.Compress, true),
    };
    public static object GzipNewWriter(object? w) { var m = new MemoryStream(); return new GoCompWriter { W = w, Mem = m, Z = Wrap(m, 0), Kind = 0 }; }
    // gzip.NewWriterLevel(w, level) (*Writer, error): valid level is [HuffmanOnly(-2), BestCompression(9)].
    public static object?[] GzipNewWriterLevel(object? w, long level)
    {
        if (level < -2 || level > 9)
            return new object?[] { null, new GoError(GoString.FromDotNetString($"gzip: invalid compression level: {level}")) };
        var m = new MemoryStream();
        return new object?[] { new GoCompWriter { W = w, Mem = m, Z = Wrap(m, 0), Kind = 0 }, null };
    }
    // gzip/zlib/flate Writer.Reset(w): discard buffered state and write subsequent output to w.
    public static void CompW_Reset(object wo, object? w)
    {
        var c = (GoCompWriter)wo;
        c.Z.Dispose();
        c.Mem = new MemoryStream();
        c.W = w;
        c.Z = Wrap(c.Mem, c.Kind);
    }
    public static object ZlibNewWriter(object? w) { var m = new MemoryStream(); return new GoCompWriter { W = w, Mem = m, Z = Wrap(m, 1), Kind = 1 }; }
    // zlib.NewWriterLevel(w, level) (*Writer, error): valid level is [HuffmanOnly(-2), BestCompression(9)].
    public static object?[] ZlibNewWriterLevel(object? w, long level)
    {
        if (level < -2 || level > 9)
            return new object?[] { null, new GoError(GoString.FromDotNetString($"zlib: invalid compression level: {level}")) };
        var m = new MemoryStream();
        return new object?[] { new GoCompWriter { W = w, Mem = m, Z = Wrap(m, 1), Kind = 1 }, null };
    }
    // zlib.NewWriterLevelDict: the preset dictionary is not applied (.NET ZLibStream has no
    // dict support); the stream round-trips with NewReaderDict, which likewise ignores it.
    public static object?[] ZlibNewWriterLevelDict(object? w, long level, GoSlice dict) => ZlibNewWriterLevel(w, level);
    public static object FlateNewWriter(object? w, long level) { var m = new MemoryStream(); return new GoCompWriter { W = w, Mem = m, Z = Wrap(m, 2), Kind = 2 }; }

    public static object?[] CompW_Write(object wo, GoSlice p)
    {
        var w = (GoCompWriter)wo;
        var buf = new byte[p.Len];
        for (int i = 0; i < p.Len; i++) buf[i] = (byte)System.Convert.ToInt64(p.Data![p.Off + i]);
        w.Z.Write(buf, 0, buf.Length);
        return new object?[] { (long)p.Len, null };
    }
    public static object? CompW_Close(object wo)
    {
        var w = (GoCompWriter)wo;
        w.Z.Dispose();
        WriteRaw(w.W, w.Mem.ToArray());
        return null;
    }
    public static object? CompW_Flush(object wo) { ((GoCompWriter)wo).Z.Flush(); return null; }

    // Write raw (binary) bytes to a writer the runtime understands.
    internal static void WriteRaw(object? w, byte[] data)
    {
        switch (w)
        {
            case IGoWriter gw: gw.GoWrite(data); break;
            case GoFile f when f.Wr != null: f.Wr.Write(data, 0, data.Length); break;
            case GoBuffer buf: buf.B.AddRange(data); break;
            case GoReader gr: { var n = new byte[gr.Data.Length + data.Length]; gr.Data.CopyTo(n, 0); data.CopyTo(n, gr.Data.Length); gr.Data = n; break; }
            case GoConn gc: gc.S.Write(data, 0, data.Length); break; // a net.Conn (fasthttp response) — write to the socket, not stdout
            default: Fmt.WriteTo(w, System.Text.Encoding.UTF8.GetString(data)); break;
        }
    }

    private static GoSlice Bytes(byte[] b)
    {
        var d = new object?[b.Length];
        for (int i = 0; i < b.Length; i++) d[i] = (int)b[i];
        return new GoSlice { Data = d, Off = 0, Len = b.Length, Cap = b.Length };
    }
    private static object DecompReader(object? r, int kind)
    {
        byte[] data = Readers.Drain(r);
        using var input = new MemoryStream(data);
        Stream z = kind switch
        {
            1 => new ZLibStream(input, CompressionMode.Decompress),
            2 => new DeflateStream(input, CompressionMode.Decompress),
            _ => new GZipStream(input, CompressionMode.Decompress),
        };
        using var outp = new MemoryStream();
        z.CopyTo(outp);
        return new GoReader { Data = outp.ToArray() };
    }
    public static object?[] GzipNewReader(object? r) => new object?[] { DecompReader(r, 0), null };
    public static object ZlibNewReaderObj(object? r) => DecompReader(r, 1);
    // zlib.NewReaderDict(r, dict) (io.ReadCloser, error): dict ignored (see NewWriterLevelDict).
    public static object?[] ZlibNewReaderDict(object? r, GoSlice dict) => new object?[] { DecompReader(r, 1), null };
    public static object?[] ZlibNewReader(object? r) => new object?[] { DecompReader(r, 1), null };
    public static object FlateNewReader(object? r) => DecompReader(r, 2);

    // compress/gzip.Reader / zlib.Reader / flate Reader methods (receiver is a GoReader
    // holding the fully-decompressed bytes; reads stream from that snapshot).
    public static object?[] CompR_Read(object rd, GoSlice p)
    {
        var gr = (GoReader)rd;
        int avail = gr.Data.Length - gr.Pos, n = System.Math.Min(p.Len, avail);
        for (int i = 0; i < n; i++) p.Data![p.Off + i] = (int)gr.Data[gr.Pos + i];
        gr.Pos += n;
        return new object?[] { (long)n, n == 0 ? Io.EOFSentinel : null };
    }
    public static object? CompR_Reset(object rd, object? r)
    {
        var fresh = (GoReader)DecompReader(r, 0);
        var gr = (GoReader)rd; gr.Data = fresh.Data; gr.Pos = 0;
        return null;
    }
    public static object? CompR_Close(object rd) => null;
    // gzip.Reader.Multistream(ok): the snapshot reader already holds the fully-decompressed
    // stream, so toggling multistream mode is a no-op here.
    public static void CompR_Multistream(object rd, bool ok) { }
}
