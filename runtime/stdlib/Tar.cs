namespace GoCLR.Stdlib;

using System.Text;
using GoCLR.Runtime;

// archive/tar — a focused USTAR implementation (regular files): WriteHeader/Write/Close and
// Next/Read round-trip the common Header fields (Name, Size, Mode, Typeflag, ModTime, Linkname,
// Uid/Gid, Uname/Gname). PAX/GNU extensions (names > 100 bytes, sub-second times) are not
// emitted — see LIMITATIONS.

[GoShim("archive/tar.Header")]
public sealed class GoTarHeader
{
    public long Typeflag;
    public string Name = "", Linkname = "", Uname = "", Gname = "";
    public long Size, Mode, Uid, Gid;
    public object? ModTime; // time.Time (GoTime)
}

[GoShim("archive/tar.Writer")]
public sealed class GoTarWriter { public object? W; public long PadOwed; }

[GoShim("archive/tar.Reader")]
public sealed class GoTarReader
{
    public byte[] Data = System.Array.Empty<byte>();
    public int NextPos, CurDataPos;
    public long CurRemaining;
}

public static class Tar
{
    public static object NewWriter(object? w) => new GoTarWriter { W = w };
    public static object NewReader(object? r) => new GoTarReader { Data = Readers.Drain(r) };
    public static object NewHeaderZero() => new GoTarHeader();

    // --- Header field get/set (byte Typeflag -> int per goclr's uint8 lowering) ---
    public static GoString Header_Name(object h) => GoString.FromDotNetString(((GoTarHeader)h).Name);
    public static void Header_SetName(object h, GoString v) => ((GoTarHeader)h).Name = v.ToDotNetString();
    public static GoString Header_Linkname(object h) => GoString.FromDotNetString(((GoTarHeader)h).Linkname);
    public static void Header_SetLinkname(object h, GoString v) => ((GoTarHeader)h).Linkname = v.ToDotNetString();
    public static GoString Header_Uname(object h) => GoString.FromDotNetString(((GoTarHeader)h).Uname);
    public static void Header_SetUname(object h, GoString v) => ((GoTarHeader)h).Uname = v.ToDotNetString();
    public static GoString Header_Gname(object h) => GoString.FromDotNetString(((GoTarHeader)h).Gname);
    public static void Header_SetGname(object h, GoString v) => ((GoTarHeader)h).Gname = v.ToDotNetString();
    public static long Header_Size(object h) => ((GoTarHeader)h).Size;
    public static void Header_SetSize(object h, long v) => ((GoTarHeader)h).Size = v;
    public static long Header_Mode(object h) => ((GoTarHeader)h).Mode;
    public static void Header_SetMode(object h, long v) => ((GoTarHeader)h).Mode = v;
    public static long Header_Uid(object h) => ((GoTarHeader)h).Uid;
    public static void Header_SetUid(object h, long v) => ((GoTarHeader)h).Uid = v;
    public static long Header_Gid(object h) => ((GoTarHeader)h).Gid;
    public static void Header_SetGid(object h, long v) => ((GoTarHeader)h).Gid = v;
    public static int Header_Typeflag(object h) => (int)((GoTarHeader)h).Typeflag;
    public static void Header_SetTypeflag(object h, int v) => ((GoTarHeader)h).Typeflag = v;
    public static object Header_ModTime(object h) => ((GoTarHeader)h).ModTime ?? Time.TimeZero();
    public static void Header_SetModTime(object h, object? v) => ((GoTarHeader)h).ModTime = v;

    // --- Writer ---
    public static object? Writer_WriteHeader(object tw, object? hdr)
    {
        var w = (GoTarWriter)tw;
        FlushPad(w);
        var h = (GoTarHeader)hdr!;
        var b = new byte[512];
        char tf = h.Typeflag != 0 ? (char)h.Typeflag : (h.Name.EndsWith("/") ? '5' : '0');
        PutString(b, 0, 100, h.Name);
        PutOctal(b, 100, 8, h.Mode);
        PutOctal(b, 108, 8, h.Uid);
        PutOctal(b, 116, 8, h.Gid);
        PutOctal(b, 124, 12, h.Size);
        PutOctal(b, 136, 12, ModUnix(h.ModTime));
        b[156] = (byte)tf;
        PutString(b, 157, 100, h.Linkname);
        var magic = Encoding.ASCII.GetBytes("ustar\0");
        System.Array.Copy(magic, 0, b, 257, 6);
        b[263] = (byte)'0'; b[264] = (byte)'0';
        PutString(b, 265, 32, h.Uname);
        PutString(b, 297, 32, h.Gname);
        PutOctal(b, 329, 8, 0); // devmajor — USTAR writes octal zero, not raw NULs
        PutOctal(b, 337, 8, 0); // devminor
        // checksum: signed bytes summed with the chksum field treated as spaces.
        for (int i = 148; i < 156; i++) b[i] = (byte)' ';
        long sum = 0; foreach (byte x in b) sum += x;
        string cs = System.Convert.ToString(sum, 8).PadLeft(6, '0');
        for (int i = 0; i < 6; i++) b[148 + i] = (byte)cs[i];
        b[154] = 0; b[155] = (byte)' ';
        Compress.WriteRaw(w.W, b);
        w.PadOwed = (512 - (h.Size % 512)) % 512;
        return null;
    }

    public static object?[] Writer_Write(object tw, GoSlice p)
    {
        var w = (GoTarWriter)tw;
        byte[] data = Raw(p);
        Compress.WriteRaw(w.W, data);
        return new object?[] { (long)data.Length, null };
    }

    public static object? Writer_Flush(object tw) { FlushPad((GoTarWriter)tw); return null; }

    public static object? Writer_Close(object tw)
    {
        var w = (GoTarWriter)tw;
        FlushPad(w);
        Compress.WriteRaw(w.W, new byte[1024]); // two zero blocks mark end-of-archive
        return null;
    }

    private static void FlushPad(GoTarWriter w)
    {
        if (w.PadOwed > 0) { Compress.WriteRaw(w.W, new byte[w.PadOwed]); w.PadOwed = 0; }
    }

    private static long ModUnix(object? mt) =>
        mt is GoTime gt && !gt.IsZero ? Time.Time_Unix(gt) : 0;

    // --- Reader ---
    public static object?[] Reader_Next(object tr)
    {
        var r = (GoTarReader)tr;
        if (r.NextPos + 512 > r.Data.Length) return new object?[] { null, Io.EOF() };
        bool allZero = true;
        for (int i = r.NextPos; i < r.NextPos + 512; i++) if (r.Data[i] != 0) { allZero = false; break; }
        if (allZero) return new object?[] { null, Io.EOF() };
        int p = r.NextPos;
        var h = new GoTarHeader
        {
            Name = GetString(r.Data, p, 100),
            Mode = GetOctal(r.Data, p + 100, 8),
            Uid = GetOctal(r.Data, p + 108, 8),
            Gid = GetOctal(r.Data, p + 116, 8),
            Size = GetOctal(r.Data, p + 124, 12),
            ModTime = Time.Unix(GetOctal(r.Data, p + 136, 12), 0),
            Typeflag = r.Data[p + 156] == 0 ? '0' : r.Data[p + 156],
            Linkname = GetString(r.Data, p + 157, 100),
            Uname = GetString(r.Data, p + 265, 32),
            Gname = GetString(r.Data, p + 297, 32),
        };
        r.CurDataPos = p + 512;
        r.CurRemaining = h.Size;
        r.NextPos = r.CurDataPos + (int)(((h.Size + 511) / 512) * 512);
        return new object?[] { h, null };
    }

    public static object?[] Reader_Read(object tr, GoSlice p)
    {
        var r = (GoTarReader)tr;
        if (r.CurRemaining <= 0) return new object?[] { 0L, Io.EOF() };
        int n = (int)System.Math.Min(p.Len, r.CurRemaining);
        for (int i = 0; i < n; i++) p.Data![p.Off + i] = (int)r.Data[r.CurDataPos + i];
        r.CurDataPos += n; r.CurRemaining -= n;
        object? err = r.CurRemaining <= 0 ? Io.EOF() : null;
        return new object?[] { (long)n, err };
    }

    // io.ReadAll(tr) drains the current entry's remaining bytes.
    internal static byte[] DrainEntry(GoTarReader r)
    {
        int n = (int)r.CurRemaining;
        if (n <= 0) return System.Array.Empty<byte>();
        var b = new byte[n];
        System.Array.Copy(r.Data, r.CurDataPos, b, 0, n);
        r.CurDataPos += n; r.CurRemaining = 0;
        return b;
    }

    // --- encoding helpers ---
    private static void PutString(byte[] b, int off, int n, string s)
    {
        var by = Encoding.UTF8.GetBytes(s);
        System.Array.Copy(by, 0, b, off, System.Math.Min(by.Length, n));
    }
    private static void PutOctal(byte[] b, int off, int n, long val)
    {
        string s = System.Convert.ToString(val, 8);
        if (s.Length > n - 1) s = s.Substring(s.Length - (n - 1));
        s = s.PadLeft(n - 1, '0');
        for (int i = 0; i < n - 1; i++) b[off + i] = (byte)s[i];
        b[off + n - 1] = 0;
    }
    private static string GetString(byte[] d, int off, int n)
    {
        int end = off;
        while (end < off + n && d[end] != 0) end++;
        return Encoding.UTF8.GetString(d, off, end - off);
    }
    private static long GetOctal(byte[] d, int off, int n)
    {
        long v = 0; int i = off, end = off + n;
        while (i < end && (d[i] == ' ' || d[i] == 0)) i++;
        for (; i < end && d[i] >= '0' && d[i] <= '7'; i++) v = v * 8 + (d[i] - '0');
        return v;
    }
    private static byte[] Raw(GoSlice s)
    {
        var b = new byte[s.Len];
        for (int i = 0; i < s.Len; i++) b[i] = (byte)System.Convert.ToInt64(s.Data![s.Off + i]);
        return b;
    }
}
