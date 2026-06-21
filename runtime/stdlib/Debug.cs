namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>Shim for the slice of runtime/debug that libraries probe: ReadBuildInfo
/// reports "not available" (ok=false), which callers (acme's User-Agent, version
/// reporters) already handle by falling back to a default.</summary>
public static class Debug
{
    // runtime/debug.ReadBuildInfo() (*BuildInfo, ok bool).
    public static object?[] ReadBuildInfo() => new object?[] { null, false };

    // runtime/debug.Stack() []byte — goclr has no Go stack walker; return an empty trace.
    public static GoSlice Stack() => new() { Data = System.Array.Empty<object?>(), Off = 0, Len = 0, Cap = 0 };
    public static void PrintStack() { }
    public static void SetGCPercent(long v) { }
    public static void FreeOSMemory() { }
}
