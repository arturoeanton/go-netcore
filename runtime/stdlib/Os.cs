namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>A minimal os.File handle (currently the standard streams).</summary>
public sealed class GoFile { public bool IsStderr; public bool IsStdin; }

/// <summary>Shim for a subset of Go's <c>os</c> package.</summary>
public static class Os
{
    private static readonly GoFile StdoutFile = new() { IsStderr = false };
    private static readonly GoFile StderrFile = new() { IsStderr = true };
    private static readonly GoFile StdinFile = new() { IsStdin = true };

    public static object Stdout() => StdoutFile;
    public static object Stderr() => StderrFile;
    public static object Stdin() => StdinFile;

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
