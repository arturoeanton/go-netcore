namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>An os.File handle: a standard stream, or a file opened for writing.</summary>
public sealed class GoFile { public bool IsStderr; public bool IsStdin; public System.IO.Stream? Wr; public string Path = ""; }

/// <summary>An os.SyscallError (a syscall-tagged error).</summary>
public sealed class GoSyscallError { public string Syscall = ""; public object? Err; }

/// <summary>An os.FileInfo: the fields fs.FileInfo exposes through its methods.</summary>
public sealed class GoFileInfo
{
    public GoString FileName;
    public long Size;
    public bool Dir;
    public long ModTimeN; // nanoseconds since Unix epoch
}

/// <summary>Shim for a subset of Go's <c>os</c> package.</summary>
public static class Os
{
    private static readonly GoFile StdoutFile = new() { IsStderr = false };
    private static readonly GoFile StderrFile = new() { IsStderr = true };
    private static readonly GoFile StdinFile = new() { IsStdin = true };

    public static object Stdout() => StdoutFile;
    public static object Stderr() => StderrFile;
    public static object Stdin() => StdinFile;

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
            return new object?[] { null, new GoError("open " + name.ToDotNetString() + ": " + e.Message) };
        }
    }

    // os.Create(name) (*os.File, error): truncate/create the file for writing.
    public static object?[] Create(GoString name)
    {
        try { return new object?[] { new GoFile { Wr = System.IO.File.Create(name.ToDotNetString()), Path = name.ToDotNetString() }, null }; }
        catch (System.Exception e) { return new object?[] { null, new GoError("open " + name.ToDotNetString() + ": " + e.Message) }; }
    }

    // os.OpenFile(name, flag, perm) (*os.File, error): create-or-append for writing,
    // otherwise read the file fully (matching os.Open).
    public static object?[] OpenFile(GoString name, long flag, uint perm)
    {
        const long O_WRONLY = 1, O_RDWR = 2, O_APPEND = 0x400, O_CREATE = 0x40, O_TRUNC = 0x200;
        bool write = (flag & (O_WRONLY | O_RDWR)) != 0 || (flag & (O_APPEND | O_CREATE | O_TRUNC)) != 0;
        if (!write) return Open(name);
        try
        {
            string p = name.ToDotNetString();
            var mode = (flag & O_APPEND) != 0 ? System.IO.FileMode.Append
                     : (flag & O_TRUNC) != 0 ? System.IO.FileMode.Create
                     : (flag & O_CREATE) != 0 ? System.IO.FileMode.OpenOrCreate
                     : System.IO.FileMode.Open;
            return new object?[] { new GoFile { Wr = new System.IO.FileStream(p, mode, System.IO.FileAccess.Write), Path = p }, null };
        }
        catch (System.Exception e) { return new object?[] { null, new GoError("open " + name.ToDotNetString() + ": " + e.Message) }; }
    }

    // (*os.File).Write(p) (n int, err error): write bytes to the underlying stream
    // (or the standard stream for stdout/stderr).
    public static object?[] File_Write(object f, GoSlice p)
    {
        var gf = (GoFile)f;
        byte[] buf = new byte[p.Len];
        for (int i = 0; i < p.Len; i++) buf[i] = (byte)System.Convert.ToInt64(p.Data![p.Off + i]);
        if (gf.Wr != null) gf.Wr.Write(buf, 0, buf.Length);
        else { var s = System.Text.Encoding.UTF8.GetString(buf); if (gf.IsStderr) System.Console.Error.Write(s); else System.Console.Out.Write(s); }
        return new object?[] { (long)p.Len, null };
    }
    public static object?[] File_WriteString(object f, GoString s) => File_Write(f, BytesOf(s));
    private static GoSlice BytesOf(GoString s)
    {
        var by = s.Bytes; var d = new object?[by.Length];
        for (int i = 0; i < by.Length; i++) d[i] = (int)by[i];
        return new GoSlice { Data = d, Off = 0, Len = by.Length, Cap = by.Length };
    }

    // (*os.File).Read(p): read from a file opened with os.Open (a buffered GoReader).
    public static object?[] File_Read(object f, GoSlice p) => Io.ReadFull(f, p);
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

    // os.Stat(name) (FileInfo, error).
    public static object?[] Stat(GoString name)
    {
        string p = name.ToDotNetString();
        try
        {
            if (System.IO.Directory.Exists(p))
                return new object?[] { new GoFileInfo { FileName = GoString.FromDotNetString(System.IO.Path.GetFileName(p.TrimEnd('/')) is var d && d.Length > 0 ? d : p), Dir = true, Size = 0 }, null };
            if (System.IO.File.Exists(p))
            {
                var fi = new System.IO.FileInfo(p);
                return new object?[] { new GoFileInfo { FileName = GoString.FromDotNetString(fi.Name), Size = fi.Length, Dir = false }, null };
            }
            return new object?[] { null, NotExist(p) };
        }
        catch (System.Exception e)
        {
            return new object?[] { null, new GoError("stat " + p + ": " + e.Message) };
        }
    }

    private static GoError NotExist(string p) => new("stat " + p + ": no such file or directory");

    // os.IsNotExist(err): does err report a missing file? (matched by message suffix).
    public static bool IsNotExist(object? err) =>
        err is GoError g && g.Error().ToDotNetString().EndsWith("no such file or directory", System.StringComparison.Ordinal);

    // os.NewFile(fd, name) *os.File: a file handle for a raw descriptor. goclr has no fd
    // table, so this is an opaque handle carrying the name (used by RunFd).
    public static object NewFile(ulong fd, GoString name) => new GoFile { Path = name.ToDotNetString() };

    // os.Remove(name) error / os.RemoveAll(path) error.
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

    // os.FileInfo method set.
    public static GoString FileInfo_Name(object fi) => ((GoFileInfo)fi).FileName;
    public static long FileInfo_Size(object fi) => ((GoFileInfo)fi).Size;
    public static bool FileInfo_IsDir(object fi) => ((GoFileInfo)fi).Dir;
    // Mode() io/fs.FileMode: goclr does not read OS file modes, so this reports the
    // directory bit (1<<31) for a directory and 0 otherwise — enough for the common
    // IsDir()/Type() checks; socket/device bits are not represented.
    public static uint FileInfo_Mode(object fi) => ((GoFileInfo)fi).Dir ? (uint)(1u << 31) : 0u;

    public static GoString Getenv(GoString key) =>
        GoString.FromDotNetString(System.Environment.GetEnvironmentVariable(key.ToDotNetString()) ?? "");

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
    public static long Getpid() => System.Environment.ProcessId;

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
            return new object?[] { new GoSlice { Data = System.Array.Empty<object?>(), Off = 0, Len = 0, Cap = 0 }, new GoError(GoString.FromDotNetString(ex.Message)) };
        }
    }

    public static object? WriteFile(GoString name, GoSlice data, uint perm)
    {
        try
        {
            var bytes = new byte[data.Len];
            for (int i = 0; i < data.Len; i++) bytes[i] = (byte)(System.Convert.ToInt64(data.Data![data.Off + i]) & 0xff);
            System.IO.File.WriteAllBytes(name.ToDotNetString(), bytes);
            return null;
        }
        catch (System.Exception ex)
        {
            return new GoError(GoString.FromDotNetString(ex.Message));
        }
    }
}
