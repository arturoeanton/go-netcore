namespace GoCLR.Stdlib;

using System.IO;
using System.IO.Compression;
using GoCLR.Runtime;

/// <summary>A gzip/zlib/flate writer that buffers and emits on Close.</summary>
public sealed class GoCompWriter { public object? W; public MemoryStream Mem = new(); public Stream Z = null!; public int Kind; public string Name = "", Comment = ""; public long ModTime; }

/// <summary>compress/flate.ReadError / WriteError (deprecated): a byte offset + wrapped error.</summary>
[GoShim("compress/flate.ReadError")]
public sealed class GoFlateReadError : IGoError
{
    public long Offset; public object? Err;
    public GoString Error() => GoString.FromDotNetString("flate: read error at offset " + Offset + ": " + Compress.ErrStr(Err));
}
[GoShim("compress/flate.WriteError")]
public sealed class GoFlateWriteError : IGoError
{
    public long Offset; public object? Err;
    public GoString Error() => GoString.FromDotNetString("flate: write error at offset " + Offset + ": " + Compress.ErrStr(Err));
}

/// <summary>Shim for compress/gzip, compress/zlib, compress/flate (.NET streams).</summary>
public static class Compress
{
    internal static string ErrStr(object? e) =>
        e is IGoError g ? g.Error().ToDotNetString()
        : e != null && Bridge.HasMethod(e, "Error") && Bridge.CallMethod(e, "Error") is GoString gs ? gs.ToDotNetString() : "";

    // flate.CorruptInputError (int64) / InternalError (string): Error() formatting.
    public static GoString CorruptInputError_Error(long e) => GoString.FromDotNetString("flate: corrupt input before offset " + e);
    public static GoString InternalError_Error(GoString e) => GoString.FromDotNetString("flate: internal error: " + e.ToDotNetString());

    // flate.ReadError / WriteError struct shims.
    public static object ReadErrorZero() => new GoFlateReadError();
    public static long ReadError_Offset(object e) => ((GoFlateReadError)e).Offset;
    public static object? ReadError_Err(object e) => ((GoFlateReadError)e).Err;
    public static void ReadError_SetOffset(object e, long v) => ((GoFlateReadError)e).Offset = v;
    public static void ReadError_SetErr(object e, object? v) => ((GoFlateReadError)e).Err = v;
    public static GoString ReadError_Error(object e) => ((GoFlateReadError)e).Error();
    public static object WriteErrorZero() => new GoFlateWriteError();
    public static long WriteError_Offset(object e) => ((GoFlateWriteError)e).Offset;
    public static object? WriteError_Err(object e) => ((GoFlateWriteError)e).Err;
    public static void WriteError_SetOffset(object e, long v) => ((GoFlateWriteError)e).Offset = v;
    public static void WriteError_SetErr(object e, object? v) => ((GoFlateWriteError)e).Err = v;
    public static GoString WriteError_Error(object e) => ((GoFlateWriteError)e).Error();
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
    // gzip buffers the raw bytes (Z stays null) and frames the whole stream on Close, so the
    // Name/Comment header fields can be written; zlib/flate stream through the .NET wrapper.
    public static object GzipNewWriter(object? w) => new GoCompWriter { W = w, Mem = new MemoryStream(), Kind = 0 };
    // gzip.NewWriterLevel(w, level) (*Writer, error): valid level is [HuffmanOnly(-2), BestCompression(9)].
    public static object?[] GzipNewWriterLevel(object? w, long level)
    {
        if (level < -2 || level > 9)
            return new object?[] { null, new GoError(GoString.FromDotNetString($"gzip: invalid compression level: {level}")) };
        return new object?[] { new GoCompWriter { W = w, Mem = new MemoryStream(), Kind = 0 }, null };
    }

    // (*gzip.Writer) header field accessors (Name/Comment, promoted from gzip.Header).
    public static void Writer_SetName(object w, GoString v) => ((GoCompWriter)w).Name = v.ToDotNetString();
    public static void Writer_SetComment(object w, GoString v) => ((GoCompWriter)w).Comment = v.ToDotNetString();
    public static GoString Writer_Name(object w) => GoString.FromDotNetString(((GoCompWriter)w).Name);
    public static GoString Writer_Comment(object w) => GoString.FromDotNetString(((GoCompWriter)w).Comment);
    public static GoString GzReader_Name(object r) => GoString.FromDotNetString(((GoReader)r).GzName ?? "");
    public static GoString GzReader_Comment(object r) => GoString.FromDotNetString(((GoReader)r).GzComment ?? "");

    // Frame the buffered raw bytes as a gzip stream (RFC 1952): header (+ optional FNAME /
    // FCOMMENT), the raw deflate body, then CRC-32 and ISIZE little-endian.
    private static byte[] BuildGzip(GoCompWriter w)
    {
        byte[] raw = w.Mem.ToArray();
        var o = new System.Collections.Generic.List<byte> { 0x1f, 0x8b, 8 };
        byte flg = 0;
        if (w.Name.Length > 0) flg |= 0x08;
        if (w.Comment.Length > 0) flg |= 0x10;
        o.Add(flg);
        uint mt = (uint)w.ModTime;
        o.Add((byte)mt); o.Add((byte)(mt >> 8)); o.Add((byte)(mt >> 16)); o.Add((byte)(mt >> 24));
        o.Add(0);   // XFL
        o.Add(255); // OS = unknown
        if (w.Name.Length > 0) { foreach (char c in w.Name) o.Add((byte)c); o.Add(0); }
        if (w.Comment.Length > 0) { foreach (char c in w.Comment) o.Add((byte)c); o.Add(0); }
        byte[] body;
        using (var def = new MemoryStream())
        {
            using (var ds = new DeflateStream(def, CompressionLevel.Optimal, true)) ds.Write(raw, 0, raw.Length);
            body = def.ToArray();
        }
        if (body.Length == 0) body = new byte[] { 0x03, 0x00 }; // canonical empty-deflate final block
        o.AddRange(body);
        uint crc = Crc32(raw), isize = (uint)raw.Length;
        o.Add((byte)crc); o.Add((byte)(crc >> 8)); o.Add((byte)(crc >> 16)); o.Add((byte)(crc >> 24));
        o.Add((byte)isize); o.Add((byte)(isize >> 8)); o.Add((byte)(isize >> 16)); o.Add((byte)(isize >> 24));
        return o.ToArray();
    }

    private static uint Crc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++) crc = (crc >> 1) ^ (0xEDB88320u & (uint)-(int)(crc & 1));
        }
        return ~crc;
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
    // flate.NewWriter(w, level) (*Writer, error).
    public static object?[] FlateNewWriter(object? w, long level) { var m = new MemoryStream(); return new object?[] { new GoCompWriter { W = w, Mem = m, Z = Wrap(m, 2), Kind = 2 }, null }; }

    public static object?[] CompW_Write(object wo, GoSlice p)
    {
        var w = (GoCompWriter)wo;
        var buf = new byte[p.Len];
        for (int i = 0; i < p.Len; i++) buf[i] = (byte)System.Convert.ToInt64(p.Data![p.Off + i]);
        if (w.Kind == 0) w.Mem.Write(buf, 0, buf.Length); // gzip: buffer raw, frame on Close
        else w.Z.Write(buf, 0, buf.Length);               // zlib/flate: stream through the .NET wrapper
        return new object?[] { (long)p.Len, null };
    }
    public static object? CompW_Close(object wo)
    {
        var w = (GoCompWriter)wo;
        if (w.Kind == 0) { WriteRaw(w.W, BuildGzip(w)); return null; }
        w.Z.Dispose();
        WriteRaw(w.W, w.Mem.ToArray());
        return null;
    }
    public static object? CompW_Flush(object wo) { var w = (GoCompWriter)wo; if (w.Kind != 0) w.Z.Flush(); return null; }

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
            case GoBufWriter bw: bw.Buf.AddRange(data); break; // bufio.Writer — append raw, Flush emits
            case GoBufReadWriter rw when rw.W is GoBufWriter bww: bww.Buf.AddRange(data); break;
            // A user io.Writer (lowered Write adapter): drive its own Write with the RAW bytes
            // through the bridge — routing via Fmt.WriteTo(string) would UTF-8-mangle them.
            case not null when Bridge.HasMethod(w, "Write"): Bridge.CallMethod(w, "Write", Bytes(data)); break;
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
    public static object?[] GzipNewReader(object? r)
    {
        byte[] data = Readers.Drain(r);
        string? name = null, comment = null;
        if (data.Length >= 10 && data[0] == 0x1f && data[1] == 0x8b)
        {
            byte flg = data[3];
            int pos = 10;
            if ((flg & 0x04) != 0 && pos + 2 <= data.Length) { int xlen = data[pos] | data[pos + 1] << 8; pos += 2 + xlen; } // FEXTRA
            if ((flg & 0x08) != 0) { int s = pos; while (pos < data.Length && data[pos] != 0) pos++; name = Latin1(data, s, pos - s); pos++; }
            if ((flg & 0x10) != 0) { int s = pos; while (pos < data.Length && data[pos] != 0) pos++; comment = Latin1(data, s, pos - s); pos++; }
        }
        using var input = new MemoryStream(data);
        using var z = new GZipStream(input, CompressionMode.Decompress);
        using var outp = new MemoryStream();
        z.CopyTo(outp);
        return new object?[] { new GoReader { Data = outp.ToArray(), GzName = name, GzComment = comment }, null };
    }
    private static string Latin1(byte[] d, int s, int len) { var sb = new System.Text.StringBuilder(); for (int i = 0; i < len; i++) sb.Append((char)d[s + i]); return sb.ToString(); }
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
