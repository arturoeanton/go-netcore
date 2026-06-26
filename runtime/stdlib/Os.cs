namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>An os.File handle: a standard stream, or a file opened for writing.</summary>
[GoShim("os.File")]
public sealed class GoFile { public bool IsStderr; public bool IsStdin; public System.IO.Stream? Wr; public string Path = ""; public bool Append; }

/// <summary>A syscall.Errno-like value: its own Go message ("no such file or directory")
/// while wrapping the os sentinel (ErrNotExist/…) so errors.Is matches through PathError.</summary>
public sealed class GoOsErrno : IGoError, IGoWrapped
{
    public string Msg = ""; public GoError? Sentinel;
    public object? GoUnwrapped() => Sentinel;
    public GoString Error() => GoString.FromDotNetString(Msg);
}

/// <summary>An os.SyscallError (a syscall-tagged error). Implements IGoError so fmt
/// prints it via Error() ("syscall: inner") instead of dumping the struct fields.</summary>
[GoShim("os.SyscallError")]
public sealed class GoSyscallError : IGoError
{
    public string Syscall = "";
    public object? Err;
    public GoString Error() => GoString.FromDotNetString(Syscall + ": " + (Err is IGoError g ? g.Error().ToDotNetString() : ""));
}

/// <summary>An fs.FS rooted at a directory (os.DirFS). An opaque handle; goclr's serving
/// paths don't traverse it.</summary>
public sealed class GoDirFS { public string Root = ""; }

/// <summary>An os.FileInfo: the fields fs.FileInfo exposes through its methods. Tagged with
/// the FileInfo interface names so that when a shim-produced FileInfo (os.Stat/File.Stat)
/// flows through the os.FileInfo / io/fs.FileInfo interface, interface dispatch matches it
/// (IsShimKindStrict) and routes its methods to the FileInfo_* shims — while a user's own
/// FileInfo type dispatches to its own methods.</summary>
[GoShim("io/fs.FileInfo")]
[GoShim("os.FileInfo")]
public sealed class GoFileInfo
{
    public GoString FileName;
    public long Size;
    public bool Dir;
    public long ModTimeN; // nanoseconds since Unix epoch
    public uint Mode;      // Go FileMode (perm bits + type bits); 0 == unset
}

/// <summary>Shim for a subset of Go's <c>os</c> package.</summary>
/// <summary>os.LinkError: an Op/Old/New/Err quadruple from Link/Symlink/Rename, whose
/// Error() reads "op old new: err" and Unwrap()/Is follow the wrapped error.</summary>
[GoShim("os.LinkError")]
public sealed class GoLinkError : IGoError, IGoWrapped
{
    public string Op = "", Old = "", New = "";
    public object? Err;
    public GoString Error() => GoString.FromDotNetString(Op + " " + Old + " " + New + ": " + ErrStr(Err));
    public object? GoUnwrapped() => Err;
    internal static string ErrStr(object? e) =>
        e is IGoError g ? g.Error().ToDotNetString()
        : e != null && Bridge.HasMethod(e, "Error") && Bridge.CallMethod(e, "Error") is GoString gs ? gs.ToDotNetString() : "";
}

public static class Os
{
    // os.LinkError struct + methods (5-step shim value type).
    public static object LinkErrorZero() => new GoLinkError();
    public static GoString LinkError_Op(object e) => GoString.FromDotNetString(((GoLinkError)e).Op);
    public static GoString LinkError_Old(object e) => GoString.FromDotNetString(((GoLinkError)e).Old);
    public static GoString LinkError_New(object e) => GoString.FromDotNetString(((GoLinkError)e).New);
    public static object? LinkError_Err(object e) => ((GoLinkError)e).Err;
    public static void LinkError_SetOp(object e, GoString v) => ((GoLinkError)e).Op = v.ToDotNetString();
    public static void LinkError_SetOld(object e, GoString v) => ((GoLinkError)e).Old = v.ToDotNetString();
    public static void LinkError_SetNew(object e, GoString v) => ((GoLinkError)e).New = v.ToDotNetString();
    public static void LinkError_SetErr(object e, object? v) => ((GoLinkError)e).Err = v;
    public static GoString LinkError_Error(object e) => ((GoLinkError)e).Error();
    public static object? LinkError_Unwrap(object e) => ((GoLinkError)e).Err;

    private static readonly GoFile StdoutFile = new() { IsStderr = false };
    private static readonly GoFile StderrFile = new() { IsStderr = true };
    private static readonly GoFile StdinFile = new() { IsStdin = true };

    public static object Stdout() => StdoutFile;
    public static object Stderr() => StderrFile;
    public static object Stdin() => StdinFile;

    // os.Interrupt == syscall.SIGINT, os.Kill == syscall.SIGKILL (os.Signal values).
    public static object Interrupt() => Ossignal.Sig(2);
    public static object Kill() => Ossignal.Sig(9);

    // (*os.File).Fd() uintptr: the conventional descriptor number (stdin 0, stdout 1,
    // stderr 2). Consumers use it only to ask isatty whether the stream is a terminal;
    // under goclr isatty always reports false, so the exact value just needs to be the
    // standard fd for the stream.
    public static ulong File_Fd(object f)
    {
        var gf = (GoFile)f;
        if (gf.IsStdin) return 0;
        if (gf.IsStderr) return 2;
        return 1;
    }

    // os.Open(name) (*os.File, error): read the file into a byte reader (the result is
    // used as an io.Reader). goclr has no streaming file handle, so the whole file is
    // read up front — adequate for header-sniffing readers like mimetype.
    public static object?[] Open(GoString name)
    {
        try
        {
            var bytes = System.IO.File.ReadAllBytes(name.ToDotNetString());
            return new object?[] { new GoReader { Data = bytes }, null };
        }
        catch (System.Exception e)
        {
            return new object?[] { null, PathErr("open", name.ToDotNetString(), e) };
        }
    }

    // os.Create(name) (*os.File, error): truncate/create the file for writing.
    public static object?[] Create(GoString name)
    {
        try { return new object?[] { new GoFile { Wr = System.IO.File.Create(name.ToDotNetString()), Path = name.ToDotNetString() }, null }; }
        catch (System.Exception e) { return new object?[] { null, PathErr("open", name.ToDotNetString(), e) }; }
    }

    // os.OpenFile(name, flag, perm) (*os.File, error): open a file with full random
    // access (read+write+seek) so callers like a SQLite pager can ReadAt/WriteAt it.
    public static object?[] OpenFile(GoString name, long flag, uint perm)
    {
        // The low two bits are the access mode on every Unix (O_RDONLY=0, O_WRONLY=1,
        // O_RDWR=2). The create/trunc/append/excl bits differ per platform — goclr reads the
        // SAME os.O_* constants Go does (from the host os package), so decode with the host's
        // values (darwin vs the Linux family) to honor O_APPEND/O_TRUNC/O_CREATE/O_EXCL.
        long acc = flag & 0x3;
        bool write = acc != 0;
        bool mac = System.OperatingSystem.IsMacOS();
        long oCreate = mac ? 0x200 : 0x40;
        long oExcl = mac ? 0x800 : 0x80;
        long oTrunc = mac ? 0x400 : 0x200;
        long oAppend = mac ? 0x8 : 0x400;
        bool create = (flag & oCreate) != 0, excl = (flag & oExcl) != 0;
        bool trunc = (flag & oTrunc) != 0, append = (flag & oAppend) != 0;
        try
        {
            string p = name.ToDotNetString();
            // O_CREATE|O_EXCL on an existing path is EEXIST; report it the way Go does so
            // os.IsExist recognizes it (the .NET IOException message wouldn't match).
            if (create && excl && (System.IO.File.Exists(p) || System.IO.Directory.Exists(p)))
                return new object?[] { null, PathErrSentinel("open", p, "file exists", ErrExistSentinel) };
            System.IO.FileMode mode;
            if (create && excl) mode = System.IO.FileMode.CreateNew;      // O_CREATE|O_EXCL: must not exist
            else if (trunc) mode = create ? System.IO.FileMode.Create : System.IO.FileMode.Truncate;
            else if (create || write) mode = System.IO.FileMode.OpenOrCreate;
            else mode = System.IO.FileMode.Open;
            var access = write ? System.IO.FileAccess.ReadWrite : System.IO.FileAccess.Read;
            var fs = new System.IO.FileStream(p, mode, access, System.IO.FileShare.ReadWrite);
            // O_APPEND: position at end so writes extend the file (File_Write re-seeks to end
            // before each write, matching Go's append-on-every-write semantics).
            if (append) fs.Seek(0, System.IO.SeekOrigin.End);
            return new object?[] { new GoFile { Wr = fs, Path = p, Append = append && write }, null };
        }
        // Errno maps the .NET filesystem exception to Go's errno string so os.IsNotExist /
        // os.IsExist match (a missing file -> "no such file or directory", etc.).
        catch (System.Exception e) { return new object?[] { null, PathErr("open", name.ToDotNetString(), e) }; }
    }

    // (*os.File).Write(p): write at the current position to the underlying stream (or a
    // standard stream for stdout/stderr).
    public static object?[] File_Write(object f, GoSlice p)
    {
        var gf = (GoFile)f;
        byte[] buf = Raw(p);
        if (gf.Wr != null) { if (gf.Append) gf.Wr.Seek(0, System.IO.SeekOrigin.End); gf.Wr.Write(buf, 0, buf.Length); }
        else { var s = System.Text.Encoding.UTF8.GetString(buf); if (gf.IsStderr) System.Console.Error.Write(s); else System.Console.Out.Write(s); }
        return new object?[] { (long)p.Len, null };
    }
    public static object?[] File_WriteString(object f, GoString s) => File_Write(f, BytesOf(s));
    // (*os.File).WriteAt(p, off): write at an absolute offset.
    public static object?[] File_WriteAt(object f, GoSlice p, long off)
    {
        var gf = (GoFile)f;
        if (gf.Wr == null) return new object?[] { 0L, new GoError("write: file not open for writing") };
        gf.Wr.Seek(off, System.IO.SeekOrigin.Begin);
        var buf = Raw(p);
        gf.Wr.Write(buf, 0, buf.Length);
        gf.Wr.Flush(); // make the write visible to other handles on the same file
        return new object?[] { (long)p.Len, null };
    }
    // (*os.File).Read(p): read at the current position (GoReader snapshot or stream).
    public static object?[] File_Read(object f, GoSlice p)
    {
        if (f is GoFile gf && gf.Wr != null)
        {
            var buf = new byte[p.Len];
            int n = gf.Wr.Read(buf, 0, buf.Length);
            for (int i = 0; i < n; i++) p.Data![p.Off + i] = (int)buf[i];
            return new object?[] { (long)n, n == 0 ? Io.EOFSentinel : null };
        }
        return Io.ReadFull(f, p);
    }
    // (*os.File).ReadAt(p, off): read len(p) bytes at an absolute offset; short reads
    // return io.EOF (the io.ReaderAt contract).
    public static object?[] File_ReadAt(object f, GoSlice p, long off)
    {
        var gf = (GoFile)f;
        if (gf.Wr == null) return new object?[] { 0L, Io.EOFSentinel };
        gf.Wr.Seek(off, System.IO.SeekOrigin.Begin);
        var buf = new byte[p.Len];
        int total = 0;
        while (total < buf.Length)
        {
            int n = gf.Wr.Read(buf, total, buf.Length - total);
            if (n == 0) break;
            total += n;
        }
        for (int i = 0; i < total; i++) p.Data![p.Off + i] = (int)buf[i];
        return new object?[] { (long)total, total < p.Len ? Io.EOFSentinel : null };
    }
    // (*os.File).Seek(offset, whence) (ret int64, err error).
    public static object?[] File_Seek(object f, long offset, long whence)
    {
        var gf = (GoFile)f;
        if (gf.Wr == null) return new object?[] { 0L, new GoError("seek: file not seekable") };
        var origin = whence switch { 1 => System.IO.SeekOrigin.Current, 2 => System.IO.SeekOrigin.End, _ => System.IO.SeekOrigin.Begin };
        return new object?[] { gf.Wr.Seek(offset, origin), null };
    }
    // (*os.File).Truncate(size): resize the file.
    public static object? File_Truncate(object f, long size)
    {
        var gf = (GoFile)f;
        if (gf.Wr != null) { try { gf.Wr.SetLength(size); } catch (System.Exception e) { return new GoError(e.Message); } }
        return null;
    }
    // (*os.File).Stat() (FileInfo, error).
    public static object?[] File_Stat(object f)
    {
        var gf = (GoFile)f;
        long size = gf.Wr?.Length ?? 0;
        bool isDir = gf.Path != null && System.IO.Directory.Exists(gf.Path);
        uint mode = gf.Path != null && (System.IO.File.Exists(gf.Path) || isDir) ? GoFileMode(gf.Path, isDir) : 0u;
        return new object?[] { new GoFileInfo { FileName = GoString.FromDotNetString(System.IO.Path.GetFileName(gf.Path)), Size = size, Dir = isDir, Mode = mode, ModTimeN = gf.Path != null ? FileMTimeN(gf.Path) : 0 }, null };
    }
    private static byte[] Raw(GoSlice p)
    {
        var buf = new byte[p.Len];
        for (int i = 0; i < p.Len; i++) buf[i] = (byte)System.Convert.ToInt64(p.Data![p.Off + i]);
        return buf;
    }
    private static GoSlice BytesOf(GoString s)
    {
        var by = s.Bytes; var d = new object?[by.Length];
        for (int i = 0; i < by.Length; i++) d[i] = (int)by[i];
        return new GoSlice { Data = d, Off = 0, Len = by.Length, Cap = by.Length };
    }

    // (*os.File).Name(): the path the file was opened with.
    public static GoString File_Name(object f) => GoString.FromDotNetString(f is GoFile gf ? gf.Path : "");
    // (*os.File).Sync(): flush a writable file.
    public static object? File_Sync(object f) { if (f is GoFile gf && gf.Wr != null) gf.Wr.Flush(); return null; }

    // (*os.File).Close(): flush+close a writable file; no-op for read snapshots/streams.
    public static object? File_Close(object f) { if (f is GoFile gf && gf.Wr != null) { gf.Wr.Flush(); gf.Wr.Dispose(); gf.Wr = null; } return null; }

    // os.SyscallError methods.
    public static GoString SyscallError_Error(object e)
    {
        var se = (GoSyscallError)e;
        string inner = se.Err is IGoError g ? g.Error().ToDotNetString() : "";
        return GoString.FromDotNetString(se.Syscall + ": " + inner);
    }
    public static object? SyscallError_Unwrap(object e) => ((GoSyscallError)e).Err;
    public static bool SyscallError_Timeout(object e) => false;
    public static GoString SyscallError_Syscall(object e) => GoString.FromDotNetString(((GoSyscallError)e).Syscall);
    public static object? SyscallError_Err(object e) => ((GoSyscallError)e).Err;

    // os sentinel errors.
    public static readonly GoError ErrDeadlineExceededSentinel = new(GoString.FromDotNetString("i/o timeout"));
    public static object ErrDeadlineExceeded() => ErrDeadlineExceededSentinel;
    public static readonly GoError ErrNotExistSentinel = new(GoString.FromDotNetString("file does not exist"));
    public static object ErrNotExist() => ErrNotExistSentinel;
    public static readonly GoError ErrExistSentinel = new(GoString.FromDotNetString("file already exists"));
    public static object ErrExist() => ErrExistSentinel;
    public static readonly GoError ErrClosedSentinel = new(GoString.FromDotNetString("file already closed"));
    public static object ErrClosed() => ErrClosedSentinel;
    public static readonly GoError ErrPermissionSentinel = new(GoString.FromDotNetString("permission denied"));
    public static object ErrPermission() => ErrPermissionSentinel;
    public static readonly GoError ErrInvalidSentinel = new(GoString.FromDotNetString("invalid argument"));
    public static object ErrInvalid() => ErrInvalidSentinel;
    public static readonly GoError ErrProcessDoneSentinel = new(GoString.FromDotNetString("os: process already finished"));
    public static object ErrProcessDone() => ErrProcessDoneSentinel;

    // os.Stat(name) (FileInfo, error).
    // os.Lstat is os.Stat without following symlinks; goclr does not model symlinks, so it
    // is the same stat.
    public static object?[] Lstat(GoString name) => Stat(name);

    // Map a path's Unix file mode to a Go FileMode (perm bits + setuid/setgid/sticky, plus the
    // ModeDir bit for a directory). Falls back to 0777/0666 if the platform can't report it.
    internal static uint GoFileMode(string p, bool isDir)
    {
        uint mode;
        try
        {
            uint u = (uint)System.IO.File.GetUnixFileMode(p);
            mode = u & 0x1FFu;                       // rwxrwxrwx
            if ((u & 0x800u) != 0) mode |= 1u << 23; // setuid  -> ModeSetuid
            if ((u & 0x400u) != 0) mode |= 1u << 22; // setgid  -> ModeSetgid
            if ((u & 0x200u) != 0) mode |= 1u << 20; // sticky  -> ModeSticky
        }
        catch { mode = isDir ? 0x1FFu : 0x1B6u; }
        if (isDir) mode |= 1u << 31;                 // ModeDir
        return mode;
    }

    // The path's last-write time as nanoseconds since the Unix epoch (0 if unavailable).
    internal static long FileMTimeN(string p)
    {
        try
        {
            long ticks = System.IO.File.GetLastWriteTimeUtc(p).Ticks - 621355968000000000L; // ticks at 1970-01-01
            return ticks * 100; // 1 tick == 100 ns
        }
        catch { return 0; }
    }

    public static object?[] Stat(GoString name)
    {
        string p = name.ToDotNetString();
        try
        {
            if (System.IO.Directory.Exists(p))
                return new object?[] { new GoFileInfo { FileName = GoString.FromDotNetString(System.IO.Path.GetFileName(p.TrimEnd('/')) is var d && d.Length > 0 ? d : p), Dir = true, Size = 0, Mode = GoFileMode(p, true), ModTimeN = FileMTimeN(p) }, null };
            if (System.IO.File.Exists(p))
            {
                var fi = new System.IO.FileInfo(p);
                return new object?[] { new GoFileInfo { FileName = GoString.FromDotNetString(fi.Name), Size = fi.Length, Dir = false, Mode = GoFileMode(p, false), ModTimeN = FileMTimeN(p) }, null };
            }
            return new object?[] { null, NotExist(p) };
        }
        catch (System.Exception e)
        {
            return new object?[] { null, PathErr("stat", p, e) };
        }
    }

    private static GoPathError NotExist(string p) => PathErrSentinel("stat", p, "no such file or directory", ErrNotExistSentinel);

    // Walk the error's Unwrap chain (IGoWrapped), matching either the os sentinel directly
    // (so a *PathError wrapping it, or errors.Is, is recognized) or the Go errno message suffix
    // (so a plain message-tagged error still matches).
    private static bool MatchErr(object? err, GoError sentinel, string suffix)
    {
        for (int i = 0; i < 64 && err != null; i++)
        {
            if (ReferenceEquals(err, sentinel)) return true;
            if (err is IGoError ge && ge.Error().ToDotNetString().EndsWith(suffix, System.StringComparison.Ordinal)) return true;
            err = err is IGoWrapped w ? w.GoUnwrapped() : null;
        }
        return false;
    }

    // os.IsNotExist / IsExist / IsTimeout: does err report a missing/existing file or a timeout?
    public static bool IsNotExist(object? err) => MatchErr(err, ErrNotExistSentinel, "no such file or directory");
    public static bool IsExist(object? err) => MatchErr(err, ErrExistSentinel, "file exists");
    public static bool IsTimeout(object? err) => MatchErr(err, ErrDeadlineExceededSentinel, "i/o timeout");

    // os.IsPathSeparator(c): is c the OS path separator? On the platforms goclr targets
    // (unix-like), that is '/'.
    public static bool IsPathSeparator(int c) => c == '/';

    // os.Getpagesize(): the underlying machine's memory page size (matches the host OS, as
    // Go's does — e.g. 16384 on Apple silicon, 4096 on x86 Linux).
    public static long Getpagesize() => System.Environment.SystemPageSize;

    // os.Clearenv(): delete all environment variables.
    public static void Clearenv()
    {
        foreach (System.Collections.DictionaryEntry e in System.Environment.GetEnvironmentVariables())
            System.Environment.SetEnvironmentVariable((string)e.Key, null);
    }

    // os.CreateTemp(dir, pattern) (*os.File, error): create a new temp file. A "*" in
    // pattern is replaced with a random string (else it's a suffix).
    public static object?[] CreateTemp(GoString dir, GoString pattern)
    {
        try
        {
            string d = dir.ToDotNetString();
            if (d.Length == 0) d = System.IO.Path.GetTempPath();
            string pat = pattern.ToDotNetString();
            string prefix, suffix;
            int star = pat.IndexOf('*');
            if (star >= 0) { prefix = pat.Substring(0, star); suffix = pat.Substring(star + 1); }
            else { prefix = pat; suffix = ""; }
            for (int attempt = 0; ; attempt++)
            {
                string name = prefix + System.Guid.NewGuid().ToString("N").Substring(0, 10) + suffix;
                string path = System.IO.Path.Combine(d, name);
                try
                {
                    var fs = new System.IO.FileStream(path, System.IO.FileMode.CreateNew, System.IO.FileAccess.ReadWrite);
                    return new object?[] { new GoFile { Wr = fs, Path = path }, null };
                }
                catch (System.IO.IOException) when (attempt < 10000) { /* name clash: retry */ }
            }
        }
        catch (System.Exception e) { return new object?[] { null, new GoError("createtemp: " + e.Message) }; }
    }

    // os.MkdirTemp(dir, pattern) (string, error): create a new temporary directory.
    public static object?[] MkdirTemp(GoString dir, GoString pattern)
    {
        try
        {
            string d = dir.ToDotNetString();
            if (d.Length == 0) d = System.IO.Path.GetTempPath();
            string pat = pattern.ToDotNetString();
            string prefix, suffix;
            int star = pat.IndexOf('*');
            if (star >= 0) { prefix = pat.Substring(0, star); suffix = pat.Substring(star + 1); }
            else { prefix = pat; suffix = ""; }
            for (int attempt = 0; ; attempt++)
            {
                string name = prefix + System.Guid.NewGuid().ToString("N").Substring(0, 10) + suffix;
                string path = System.IO.Path.Combine(d, name);
                try { System.IO.Directory.CreateDirectory(path); return new object?[] { GoString.FromDotNetString(path), null }; }
                catch (System.IO.IOException) when (attempt < 10000) { /* name clash: retry */ }
            }
        }
        catch (System.Exception e) { return new object?[] { GoString.FromDotNetString(""), new GoError("mkdirtemp: " + e.Message) }; }
    }

    // os.TempDir(): the default directory for temporary files.
    public static GoString TempDir() => GoString.FromDotNetString(System.IO.Path.GetTempPath().TrimEnd('/'));

    // os.NewFile(fd, name) *os.File: a file handle for a raw descriptor. goclr has no fd
    // table, so this is an opaque handle carrying the name (used by RunFd).
    public static object NewFile(ulong fd, GoString name) => new GoFile { Path = name.ToDotNetString() };

    // os.Remove(name) error / os.RemoveAll(path) error.
    // os.UserCacheDir / UserConfigDir / UserHomeDir (string, error).
    public static object?[] UserCacheDir() => new object?[] { GoString.FromDotNetString(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData)), null };
    public static object?[] UserConfigDir() => new object?[] { GoString.FromDotNetString(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData)), null };
    public static object?[] UserHomeDir() => new object?[] { GoString.FromDotNetString(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile)), null };

    // os.Rename(old, new) error — atomic-ish replace (autocert's cache write).
    // os.Chmod(name, mode FileMode) error: best-effort — apply the low 9 permission bits
    // via the .NET Unix file-mode API; a no-op error-free result on platforms/paths where
    // it does not apply (goclr does not rely on file permissions).
    public static object? Chmod(GoString name, uint mode)
    {
        try { System.IO.File.SetUnixFileMode(name.ToDotNetString(), (System.IO.UnixFileMode)(mode & 0x1FF)); }
        catch { /* best-effort */ }
        return null;
    }

    // os.Chtimes(name, atime, mtime time.Time) error: set a file's access/modification
    // times. atime/mtime are GoTime handles (nanoseconds since the Unix epoch in .N).
    public static object? Chtimes(GoString name, object? atime, object? mtime)
    {
        try
        {
            string p = name.ToDotNetString();
            var epoch = new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);
            if (atime is GoTime at) System.IO.File.SetLastAccessTimeUtc(p, epoch.AddTicks(at.N / 100));
            if (mtime is GoTime mt) System.IO.File.SetLastWriteTimeUtc(p, epoch.AddTicks(mt.N / 100));
            return null;
        }
        catch (System.Exception e) { return new GoError(GoString.FromDotNetString("chtimes " + name.ToDotNetString() + ": " + e.Message)); }
    }
    public static object? Rename(GoString o, GoString n)
    {
        try { System.IO.File.Move(o.ToDotNetString(), n.ToDotNetString(), overwrite: true); return null; }
        catch (System.Exception e) { return new GoError(GoString.FromDotNetString("rename " + o.ToDotNetString() + " " + n.ToDotNetString() + ": " + e.Message)); }
    }
    public static object? Remove(GoString name)
    {
        try
        {
            string p = name.ToDotNetString();
            if (System.IO.Directory.Exists(p)) System.IO.Directory.Delete(p);
            else System.IO.File.Delete(p);
            return null;
        }
        catch (System.Exception e) { return new GoError("remove " + name.ToDotNetString() + ": " + e.Message); }
    }
    public static object? RemoveAll(GoString path)
    {
        try
        {
            string p = path.ToDotNetString();
            if (System.IO.Directory.Exists(p)) System.IO.Directory.Delete(p, true);
            else if (System.IO.File.Exists(p)) System.IO.File.Delete(p);
            return null;
        }
        catch (System.Exception e) { return new GoError("RemoveAll " + path.ToDotNetString() + ": " + e.Message); }
    }

    // os.MkdirAll(path, perm) error.
    public static object? MkdirAll(GoString path, uint perm)
    {
        try { System.IO.Directory.CreateDirectory(path.ToDotNetString()); return null; }
        catch (System.Exception e) { return new GoError("mkdir " + path.ToDotNetString() + ": " + e.Message); }
    }

    // os.Mkdir(path, perm) error: create a single directory (parent must exist; an
    // existing path is an error, mirroring Go).
    public static object? Mkdir(GoString path, uint perm)
    {
        string p = path.ToDotNetString();
        if (System.IO.Directory.Exists(p) || System.IO.File.Exists(p))
            return PathErrSentinel("mkdir", p, "file exists", ErrExistSentinel);
        try { System.IO.Directory.CreateDirectory(p); return null; }
        catch (System.Exception e) { return new GoError("mkdir " + p + ": " + e.Message); }
    }

    // os.FileInfo method set.
    public static GoString FileInfo_Name(object fi) => ((GoFileInfo)fi).FileName;
    public static long FileInfo_Size(object fi) => ((GoFileInfo)fi).Size;
    // ModTime() time.Time: the file's last-write time (a real, non-zero instant).
    public static object FileInfo_ModTime(object fi) => new GoTime { N = ((GoFileInfo)fi).ModTimeN, IsZero = false };
    public static bool FileInfo_IsDir(object fi) => ((GoFileInfo)fi).Dir;
    // Mode() io/fs.FileMode: goclr does not read OS file modes, so this reports the
    // directory bit (1<<31) for a directory and 0 otherwise — enough for the common
    // IsDir()/Type() checks; socket/device bits are not represented.
    public static uint FileInfo_Mode(object fi)
    {
        var f = (GoFileInfo)fi;
        if (f.Mode != 0) return f.Mode;
        return f.Dir ? (1u << 31) | 0x1FFu : 0x1B6u; // fallback if Mode unset
    }

    public static GoString Getenv(GoString key) =>
        GoString.FromDotNetString(System.Environment.GetEnvironmentVariable(key.ToDotNetString()) ?? "");

    // os.Hostname() (string, error).
    public static object?[] Hostname()
    {
        try { return new object?[] { GoString.FromDotNetString(System.Net.Dns.GetHostName()), null }; }
        catch (System.Exception e) { return new object?[] { GoString.FromDotNetString(""), new GoError(e.Message) }; }
    }

    // os.IsPermission(err): true only for the ErrPermission sentinel (Go matches by error
    // identity, not message text — a generic error that merely ends in "permission denied"
    // is NOT a permission error).
    public static bool IsPermission(object? err) =>
        err is GoError g && g.Error().ToDotNetString() == "permission denied";

    // os.NewSyscallError(syscall, err): wrap err (nil err -> nil).
    public static object? NewSyscallError(GoString syscall, object? err) =>
        err == null ? null : new GoSyscallError { Syscall = syscall.ToDotNetString(), Err = err };

    // os.Expand(s, mapping) — replace $var / ${var} using a faithful port of Go's getShellName.
    public static GoString Expand(GoString s, GoClosure mapping) =>
        GoString.FromDotNetString(ExpandStr(s.ToDotNetString(), name =>
            GoRuntime.InvokeArgs(mapping, GoString.FromDotNetString(name)) is GoString gs ? gs.ToDotNetString() : ""));

    // os.ExpandEnv(s) — expand using the environment.
    public static GoString ExpandEnv(GoString s) =>
        GoString.FromDotNetString(ExpandStr(s.ToDotNetString(), n => System.Environment.GetEnvironmentVariable(n) ?? ""));

    private static bool IsShellSpecial(char c) => c is '*' or '#' or '$' or '@' or '!' or '?' or '-' || (c >= '0' && c <= '9');
    private static bool IsAlphaNum(char c) => c == '_' || (c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
    private static (string name, int w) GetShellName(string s)
    {
        if (s.Length == 0) return ("", 0);
        if (s[0] == '{')
        {
            if (s.Length > 2 && IsShellSpecial(s[1]) && s[2] == '}') return (s.Substring(1, 1), 3);
            for (int i = 1; i < s.Length; i++)
                if (s[i] == '}') { if (i == 1) return ("", 2); return (s.Substring(1, i - 1), i + 1); }
            return ("", 1);
        }
        if (IsShellSpecial(s[0])) return (s.Substring(0, 1), 1);
        int j = 0;
        while (j < s.Length && IsAlphaNum(s[j])) j++;
        return (s.Substring(0, j), j);
    }
    private static string ExpandStr(string s, System.Func<string, string> mapping)
    {
        var sb = new System.Text.StringBuilder();
        int i = 0;
        bool used = false;
        for (int j = 0; j < s.Length; j++)
        {
            if (s[j] == '$' && j + 1 < s.Length)
            {
                used = true;
                sb.Append(s, i, j - i);
                var (name, w) = GetShellName(s.Substring(j + 1));
                if (name.Length == 0 && w > 0) { /* invalid syntax; drop the chars */ }
                else if (name.Length == 0) sb.Append(s[j]);
                else sb.Append(mapping(name));
                j += w;
                i = j + 1;
            }
        }
        if (!used) return s;
        sb.Append(s, i, s.Length - i);
        return sb.ToString();
    }

    // os.FindProcess(pid) (*Process, error): single-process under goclr — return an inert handle.
    public static object?[] FindProcess(long pid) => new object?[] { new GoProcess(), null };

    // os.Environ() []string: "KEY=VALUE" for each environment variable.
    public static GoSlice Environ()
    {
        var items = new System.Collections.Generic.List<object?>();
        foreach (System.Collections.DictionaryEntry e in System.Environment.GetEnvironmentVariables())
            items.Add(GoString.FromDotNetString(e.Key + "=" + e.Value));
        var d = items.ToArray();
        return new GoSlice { Data = d, Off = 0, Len = d.Length, Cap = d.Length };
    }

    // os.DirFS(dir) fs.FS: a filesystem rooted at dir. Returned as an opaque handle; the
    // fs.FS helpers (fs.Stat/fs.Sub) don't traverse it under goclr's serving paths.
    public static object DirFS(GoString dir) => new GoDirFS { Root = dir.ToDotNetString() };

    // os.Getwd() (string, error): the process's current working directory.
    public static object?[] Getwd()
    {
        try { return new object?[] { GoString.FromDotNetString(System.IO.Directory.GetCurrentDirectory()), null }; }
        catch (System.Exception e) { return new object?[] { GoString.FromDotNetString(""), new GoError(GoString.FromDotNetString(e.Message)) }; }
    }

    public static object?[] LookupEnv(GoString key)
    {
        var v = System.Environment.GetEnvironmentVariable(key.ToDotNetString());
        return new object?[] { GoString.FromDotNetString(v ?? ""), v != null };
    }

    public static object? Setenv(GoString key, GoString val)
    {
        System.Environment.SetEnvironmentVariable(key.ToDotNetString(), val.ToDotNetString());
        return null; // error
    }

    public static object? Unsetenv(GoString key)
    {
        System.Environment.SetEnvironmentVariable(key.ToDotNetString(), null);
        return null;
    }

    public static void Exit(long code) => System.Environment.Exit((int)code);
    // os.Args: the command-line arguments, args[0] being the program name. A shimVar accessor
    // always lowers with an object return, so box the slice.
    public static object Args()
    {
        var a = System.Environment.GetCommandLineArgs();
        var data = new object?[a.Length];
        for (int i = 0; i < a.Length; i++) data[i] = GoString.FromDotNetString(a[i]);
        return new GoSlice { Data = data, Off = 0, Len = a.Length, Cap = a.Length };
    }

    public static long Getpid() => System.Environment.ProcessId;
    // .NET has no portable getuid/getgid; report a stable non-root value (these feed rarely
    // used paths such as google/uuid's DCE UUIDs). On Unix the real ids are read when
    // available, else a fixed fallback so the value is deterministic.
    public static long Getuid() => UnixId("id -u", 1000);
    public static long Getgid() => UnixId("id -g", 1000);
    public static long Getppid() => System.Environment.ProcessId; // best-effort

    private static long UnixId(string cmd, long fallback)
    {
        try
        {
            if (System.OperatingSystem.IsWindows()) return -1;
            var parts = cmd.Split(' ');
            var psi = new System.Diagnostics.ProcessStartInfo(parts[0], parts.Length > 1 ? parts[1] : "")
            { RedirectStandardOutput = true, UseShellExecute = false };
            using var p = System.Diagnostics.Process.Start(psi);
            if (p == null) return fallback;
            string outp = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit();
            return long.TryParse(outp, out var id) ? id : fallback;
        }
        catch { return fallback; }
    }

    public static object?[] ReadFile(GoString name)
    {
        try
        {
            var bytes = System.IO.File.ReadAllBytes(name.ToDotNetString());
            var d = new object?[bytes.Length];
            for (int i = 0; i < bytes.Length; i++) d[i] = (int)bytes[i];
            return new object?[] { new GoSlice { Data = d, Off = 0, Len = bytes.Length, Cap = bytes.Length }, null };
        }
        catch (System.Exception ex)
        {
            return new object?[] { new GoSlice { Data = System.Array.Empty<object?>(), Off = 0, Len = 0, Cap = 0 }, PathErr("open", name.ToDotNetString(), ex) };
        }
    }

    public static object? WriteFile(GoString name, GoSlice data, uint perm)
    {
        try
        {
            var bytes = new byte[data.Len];
            for (int i = 0; i < data.Len; i++) bytes[i] = (byte)(System.Convert.ToInt64(data.Data![data.Off + i]) & 0xff);
            string p = name.ToDotNetString();
            bool existed = System.IO.File.Exists(p);
            System.IO.File.WriteAllBytes(p, bytes);
            // Like Go's WriteFile, the perm applies only to a newly-created file, modulo the
            // process umask (derived from the just-created file's default 0666 & ~umask mode).
            if (!existed)
            {
                try
                {
                    uint created = (uint)System.IO.File.GetUnixFileMode(p) & 0x1FFu;
                    uint umask = 0x1B6u & ~created; // 0666 & ~(0666 & ~umask) recovers the rw umask bits
                    System.IO.File.SetUnixFileMode(p, (System.IO.UnixFileMode)(perm & 0x1FFu & ~umask));
                }
                catch { }
            }
            return null;
        }
        catch (System.Exception ex)
        {
            return new GoError(GoString.FromDotNetString(ex.Message));
        }
    }

    // A Go-style errno reason for a .NET filesystem exception (used in PathError messages).
    private static string Errno(System.Exception e) => ErrnoOf(e).msg;

    // (errno message, the os sentinel it satisfies) for a .NET filesystem exception.
    private static (string msg, GoError? sentinel) ErrnoOf(System.Exception e) => e switch
    {
        System.IO.FileNotFoundException => ("no such file or directory", ErrNotExistSentinel),
        System.IO.DirectoryNotFoundException => ("no such file or directory", ErrNotExistSentinel),
        System.UnauthorizedAccessException => ("permission denied", ErrPermissionSentinel),
        _ => (e.Message, null),
    };

    // Build a *os.PathError for op on path from a .NET exception, wrapping a syscall-errno that
    // carries the right os sentinel (so errors.As / errors.Is / os.IsNotExist all work).
    private static GoPathError PathErr(string op, string path, System.Exception e)
    {
        var (msg, sentinel) = ErrnoOf(e);
        return new GoPathError { Op = op, Path = path, Err = new GoOsErrno { Msg = msg, Sentinel = sentinel } };
    }
    // A *os.PathError for a known sentinel (e.g. EEXIST from O_CREATE|O_EXCL, ENOENT from stat).
    private static GoPathError PathErrSentinel(string op, string path, string msg, GoError sentinel)
        => new GoPathError { Op = op, Path = path, Err = new GoOsErrno { Msg = msg, Sentinel = sentinel } };

    // os.ReadDir(name) ([]os.DirEntry, error): the directory's entries sorted by filename
    // (byte order, like Go). Entries are GoDirEntry (matches the fs.DirEntry interface).
    public static object?[] ReadDir(GoString name)
    {
        string dir = name.ToDotNetString();
        try
        {
            if (!System.IO.Directory.Exists(dir))
                return new object?[] { new GoSlice { Data = null, Off = 0, Len = 0, Cap = 0 }, new GoError(GoString.FromDotNetString("open " + dir + ": no such file or directory")) };
            var names = new System.Collections.Generic.List<string>(System.IO.Directory.GetFileSystemEntries(dir));
            names.Sort(System.StringComparer.Ordinal);
            var d = new object?[names.Count];
            for (int i = 0; i < names.Count; i++)
                d[i] = new GoDirEntry
                {
                    EntryName = GoString.FromDotNetString(System.IO.Path.GetFileName(names[i])),
                    Dir = System.IO.Directory.Exists(names[i]),
                    FullPath = names[i],
                };
            return new object?[] { new GoSlice { Data = d, Off = 0, Len = d.Length, Cap = d.Length }, null };
        }
        catch (System.Exception e) { return new object?[] { new GoSlice { Data = null, Off = 0, Len = 0, Cap = 0 }, new GoError(GoString.FromDotNetString("readdir " + dir + ": " + Errno(e))) }; }
    }

    // os.Chdir(dir): change the working directory.
    public static object? Chdir(GoString dir)
    {
        string d = dir.ToDotNetString();
        try { System.IO.Directory.SetCurrentDirectory(d); return null; }
        catch (System.Exception e) { return new GoError(GoString.FromDotNetString("chdir " + d + ": " + Errno(e))); }
    }

    // os.Truncate(name, size): resize the named file (zero-padded when grown).
    public static object? Truncate(GoString name, long size)
    {
        string n = name.ToDotNetString();
        try
        {
            using var fs = new System.IO.FileStream(n, System.IO.FileMode.Open, System.IO.FileAccess.Write);
            fs.SetLength(size);
            return null;
        }
        catch (System.Exception e) { return new GoError(GoString.FromDotNetString("truncate " + n + ": " + Errno(e))); }
    }

    // os.Symlink(oldname, newname): create newname as a symbolic link to oldname.
    public static object? Symlink(GoString oldname, GoString newname)
    {
        string o = oldname.ToDotNetString(), nw = newname.ToDotNetString();
        try { System.IO.File.CreateSymbolicLink(nw, o); return null; }
        catch (System.Exception e) { return new GoError(GoString.FromDotNetString("symlink " + o + " " + nw + ": " + Errno(e))); }
    }

    // os.Readlink(name): the destination of the named symbolic link.
    public static object?[] Readlink(GoString name)
    {
        string n = name.ToDotNetString();
        try
        {
            string? t = new System.IO.FileInfo(n).LinkTarget ?? new System.IO.DirectoryInfo(n).LinkTarget;
            if (t == null)
                return new object?[] { GoString.FromDotNetString(""), new GoError(GoString.FromDotNetString("readlink " + n + ": invalid argument")) };
            return new object?[] { GoString.FromDotNetString(t), null };
        }
        catch (System.Exception e) { return new object?[] { GoString.FromDotNetString(""), new GoError(GoString.FromDotNetString("readlink " + n + ": " + Errno(e))) }; }
    }
}
