namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>A minimal os.File handle (currently the standard streams).</summary>
public sealed class GoFile { public bool IsStderr; public bool IsStdin; }

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

    // (*os.File).Close(): no-op — os.Open already read the file fully.
    public static object? File_Close(object f) => null;

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
