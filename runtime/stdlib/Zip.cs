namespace GoCLR.Stdlib;

using System.IO;
using System.IO.Compression;
using GoCLR.Runtime;

// archive/zip — backed by .NET's ZipArchive (standard Deflate). The produced bytes are not
// byte-identical to Go's writer (different compressor), but the write→read round-trip recovers
// every entry name and content. Writer.Create returns a bytes.Buffer the caller streams into
// (its writes accumulate per entry, finalized into the archive on the next Create or Close);
// File.Open returns a bytes.Reader (io.ReadCloser whose Close is the standard no-op).

[GoShim("archive/zip.Writer")]
public sealed class GoZipWriter
{
    public object? W;
    public MemoryStream Mem = new();
    public ZipArchive? Z;
    public string? CurName;
    public GoBuffer? CurBuf;
}

[GoShim("archive/zip.Reader")]
public sealed class GoZipReader { public ZipArchive? Z; public GoSlice File; }

[GoShim("archive/zip.File")]
public sealed class GoZipFile { public ZipArchiveEntry E = null!; public string Name = ""; }

public static class Zip
{
    public static object NewWriter(object? w)
    {
        var zw = new GoZipWriter { W = w };
        zw.Z = new ZipArchive(zw.Mem, ZipArchiveMode.Create, leaveOpen: true);
        return zw;
    }

    // (*Writer).Create(name) (io.Writer, error): a fresh bytes.Buffer the caller writes into.
    public static object?[] Writer_Create(object zwo, GoString name)
    {
        var zw = (GoZipWriter)zwo;
        Finalize(zw);
        zw.CurName = name.ToDotNetString();
        zw.CurBuf = new GoBuffer();
        return new object?[] { zw.CurBuf, null };
    }

    public static object? Writer_Close(object zwo)
    {
        var zw = (GoZipWriter)zwo;
        Finalize(zw);
        zw.Z!.Dispose();            // writes the central directory into Mem
        Compress.WriteRaw(zw.W, zw.Mem.ToArray());
        return null;
    }

    public static object? Writer_Flush(object zwo) => null;

    private static void Finalize(GoZipWriter zw)
    {
        if (zw.CurName == null || zw.CurBuf == null) return;
        var entry = zw.Z!.CreateEntry(zw.CurName, CompressionLevel.Optimal);
        using var s = entry.Open();
        var bytes = zw.CurBuf.B.ToArray();
        s.Write(bytes, 0, bytes.Length);
        zw.CurName = null; zw.CurBuf = null;
    }

    // zip.NewReader(r io.ReaderAt, size int64) (*Reader, error).
    public static object?[] NewReader(object? r, long size)
    {
        try
        {
            byte[] data = Readers.Drain(r);
            var z = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read);
            var files = new object?[z.Entries.Count];
            for (int i = 0; i < z.Entries.Count; i++)
                files[i] = new GoZipFile { E = z.Entries[i], Name = z.Entries[i].FullName };
            var slice = new GoSlice { Data = files, Off = 0, Len = files.Length, Cap = files.Length };
            return new object?[] { new GoZipReader { Z = z, File = slice }, null };
        }
        catch (System.Exception e) { return new object?[] { null, new GoError(GoString.FromDotNetString(e.Message)) }; }
    }

    public static GoSlice Reader_File(object zro) => ((GoZipReader)zro).File;

    public static GoString File_Name(object fo) => GoString.FromDotNetString(((GoZipFile)fo).Name);

    // (*File).Open() (io.ReadCloser, error): the decompressed entry as a bytes.Reader.
    public static object?[] File_Open(object fo)
    {
        try
        {
            var f = (GoZipFile)fo;
            using var s = f.E.Open();
            var ms = new MemoryStream();
            s.CopyTo(ms);
            return new object?[] { new GoReader { Data = ms.ToArray() }, null };
        }
        catch (System.Exception e) { return new object?[] { null, new GoError(GoString.FromDotNetString(e.Message)) }; }
    }
}
