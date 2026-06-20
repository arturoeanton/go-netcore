namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>Shim for a subset of Go's <c>os</c> package.</summary>
public static class Os
{
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
}
