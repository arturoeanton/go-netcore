namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>A *runtime.Func handle. The runtime does not retain a PC→function map,
/// so a Func recovered from a program counter reports an empty name/location; the
/// goja paths that build one are reflect-bridge diagnostics, not core evaluation.</summary>
public sealed class GoRuntimeFunc { public long Pc; }

/// <summary>A runtime.Frame: goclr has no Go-format stack metadata, so every field reports
/// its zero (File="", Line=0, PC=0). Used by gorm's caller-location helper, which falls back
/// to no location when PC is 0.</summary>
public sealed class GoFrame { }

/// <summary>A *runtime.Frames iterator over runtime.CallersFrames — always empty.</summary>
public sealed class GoFrames { }

/// <summary>Thrown by runtime.Goexit to terminate the current goroutine (coarse — does not
/// run deferred functions).</summary>
public sealed class GoExitException : System.Exception { }

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

    // runtime.Callers(skip, pc) int / CallersFrames(callers) *Frames / (*Frames).Next() ->
    // (Frame, more): no Go stack metadata, so report zero frames. gorm's CallerFrame falls
    // back to runtime.Frame{} (PC 0 => no file:line in logged SQL).
    public static long Callers(long skip, GoSlice pc) => 0;
    public static object CallersFrames(GoSlice callers) => new GoFrames();
    public static object FrameZero() => new GoFrame();
    public static object?[] Frames_Next(object frames) => new object?[] { new GoFrame(), false };
    public static GoString Frame_File(object f) => GoString.FromDotNetString("");
    public static GoString Frame_Function(object f) => GoString.FromDotNetString("");
    public static long Frame_Line(object f) => 0;
    public static ulong Frame_PC(object f) => 0;
    public static ulong Frame_Entry(object f) => 0;

    public static long GOMAXPROCS(long n) => System.Environment.ProcessorCount;
    public static long NumCPU() => System.Environment.ProcessorCount;
    public static long NumGoroutine() => 1;
    public static void GC() => System.GC.Collect();
    public static void Gosched() => System.Threading.Thread.Yield();
    // runtime.Goexit(): terminate the current goroutine. goclr models it as a coarse abort
    // (a thrown GoExitException) — it does NOT run the goroutine's deferred functions, unlike
    // Go. Used by testify's CollectT.FailNow on the Eventually path.
    public static void Goexit() => throw new GoExitException();
    // runtime.Version(): a Go version string (gin parses it to check Go >= 1.18).
    public static GoString Version() => GoString.FromDotNetString("go1.22.0");
}
