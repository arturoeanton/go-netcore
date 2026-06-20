namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>A *runtime.Func handle. The runtime does not retain a PC→function map,
/// so a Func recovered from a program counter reports an empty name/location; the
/// goja paths that build one are reflect-bridge diagnostics, not core evaluation.</summary>
public sealed class GoRuntimeFunc { public long Pc; }

/// <summary>Shim for the subset of Go's <c>runtime</c> package that goja references.</summary>
public static class Goruntime
{
    public static object FuncForPC(long pc) => new GoRuntimeFunc { Pc = pc };
    public static GoString Func_Name(object f) => GoString.FromDotNetString("");
    public static object?[] Func_FileLine(object f, long pc) =>
        new object?[] { GoString.FromDotNetString(""), 0L };
    public static long Func_Entry(object f) => ((GoRuntimeFunc)f).Pc;

    public static long GOMAXPROCS(long n) => System.Environment.ProcessorCount;
    public static long NumCPU() => System.Environment.ProcessorCount;
    public static long NumGoroutine() => 1;
    public static void GC() => System.GC.Collect();
    public static void Gosched() => System.Threading.Thread.Yield();
}
