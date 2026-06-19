using System.Globalization;

namespace GoCLR.Runtime;

/// <summary>
/// Builtins implements Go's predeclared functions that lowering routes to the
/// runtime: panic/recover, println/print, and a few helpers. <c>len</c>/<c>cap</c>
/// are emitted inline by the backend for concrete types, but string/interface
/// overloads live here.
/// </summary>
public static class Builtins
{
    /// <summary>panic(v).</summary>
    public static void Panic(object? value)
    {
        var ctx = GoRuntime.Current();
        var p = value as GoPanicException ?? new GoPanicException(value);
        ctx.CurrentPanic = p;
        throw p;
    }

    /// <summary>recover() — valid only inside a deferred call (Go semantics).</summary>
    public static object? Recover() => GoRuntime.Current().Recover();

    /// <summary>
    /// Records a caught panic on the current goroutine so a deferred recover() can
    /// observe it. Called by the lowered catch block (handles runtime panics too,
    /// not just panic()).
    /// </summary>
    public static void SetPanic(GoPanicException p) => GoRuntime.Current().CurrentPanic = p;

    /// <summary>
    /// After deferred calls have run, reports whether the in-flight panic was
    /// recovered. Clears the goroutine's panic when handled so a re-panic starts
    /// clean; returns false (must rethrow) otherwise.
    /// </summary>
    public static bool PanicHandled()
    {
        var ctx = GoRuntime.Current();
        if (ctx.CurrentPanic == null || ctx.CurrentPanic.Recovered)
        {
            ctx.CurrentPanic = null;
            return true;
        }
        return false;
    }

    /// <summary>println(args...) — writes to stderr with spaces between args and a trailing newline, like Go.</summary>
    public static void Println(params object?[] args)
    {
        Console.Error.WriteLine(string.Join(" ", args.Select(Format)));
    }

    /// <summary>print(args...) — writes to stderr with no separators, like Go's builtin print.</summary>
    public static void Print(params object?[] args)
    {
        Console.Error.Write(string.Concat(args.Select(Format)));
    }

    private static string Format(object? v) => v switch
    {
        null => "<nil>",
        bool b => b ? "true" : "false",
        GoString gs => gs.ToDotNetString(),
        IGoError e => e.Error().ToDotNetString(),
        double d => d.ToString("g", CultureInfo.InvariantCulture),
        float f => f.ToString("g", CultureInfo.InvariantCulture),
        _ => v.ToString() ?? "<nil>",
    };
}
