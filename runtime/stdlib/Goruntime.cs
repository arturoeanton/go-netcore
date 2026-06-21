namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>A *runtime.Func handle. The runtime does not retain a PC→function map,
/// so a Func recovered from a program counter reports an empty name/location; the
/// goja paths that build one are reflect-bridge diagnostics, not core evaluation.</summary>
public sealed class GoRuntimeFunc { public long Pc; }

/// <summary>Shim for the subset of Go's <c>runtime</c> package that goja references.</summary>
public static class Goruntime
{
    public static object FuncForPC(ulong pc) => new GoRuntimeFunc { Pc = (long)pc };   // pc is uintptr
    public static GoString Func_Name(object f) => GoString.FromDotNetString("");
    public static object?[] Func_FileLine(object f, ulong pc) =>
        new object?[] { GoString.FromDotNetString(""), 0L };
    public static ulong Func_Entry(object f) => (ulong)((GoRuntimeFunc)f).Pc;          // returns uintptr

    // Caller(skip) (pc uintptr, file string, line int, ok bool): goclr keeps no
    // reflectable call stack (no PDB/source map), so caller info is unavailable —
    // the honest answer is ok=false with empty file/line, which callers like
    // gommon/log handle by simply omitting the source position.
    public static object?[] Caller(long skip) =>
        new object?[] { (ulong)0, GoString.FromDotNetString(""), 0L, false };

    // runtime.Stack(buf, all) int: goclr keeps no reflectable goroutine stacks, so
    // nothing is written and 0 is returned (used only by debug paths).
    public static long Stack(GoSlice buf, bool all) => 0;

    public static long GOMAXPROCS(long n) => System.Environment.ProcessorCount;
    public static long NumCPU() => System.Environment.ProcessorCount;
    public static long NumGoroutine() => 1;
    public static void GC() => System.GC.Collect();
    public static void Gosched() => System.Threading.Thread.Yield();
}
