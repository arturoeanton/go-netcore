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
    public static void FreeOSMemory() { }

    // Tunables: each setter returns the PREVIOUS value (Go's default on first call).
    private static long _gcPercent = 100;
    private static long _maxStack = 1000000000;     // 1e9 on 64-bit
    private static long _maxThreads = 10000;
    private static long _memLimit = long.MaxValue;  // math.MaxInt64
    private static bool _panicOnFault;

    public static long SetGCPercent(long v) { long old = _gcPercent; _gcPercent = v; return old; }
    public static long SetMaxStack(long v) { long old = _maxStack; if (v >= 0) _maxStack = v; return old; }
    public static long SetMaxThreads(long v) { long old = _maxThreads; if (v >= 0) _maxThreads = v; return old; }
    // SetMemoryLimit: a negative limit only reads the current value (Go's documented behavior).
    public static long SetMemoryLimit(long v) { long old = _memLimit; if (v >= 0) _memLimit = v; return old; }
    public static bool SetPanicOnFault(bool v) { bool old = _panicOnFault; _panicOnFault = v; return old; }

    public static void SetTraceback(GoString level) { }
    public static void WriteHeapDump(ulong fd) { }                 // no-op (do NOT write to a real fd)
    public static object? SetCrashOutput(object? f, object? opts) => null;
}
