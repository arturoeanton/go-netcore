namespace GoCLR.Runtime;

/// <summary>
/// GoRoutineContext is the per-goroutine runtime state: the in-flight panic and
/// (optionally) the active defer stack. It is stored in an AsyncLocal so each
/// goroutine — whether the main one or a Task-backed <c>go</c> call — sees its own.
/// </summary>
public sealed class GoRoutineContext
{
    public GoPanicException? CurrentPanic;

    /// <summary>
    /// The goroutine's defer stack: thunks pushed by `defer` statements (at any
    /// nesting), run LIFO down to a function's entry mark on return or panic.
    /// </summary>
    public readonly System.Collections.Generic.List<GoClosure> Defers = new();

    /// <summary>
    /// recover(): if called while a panic is in flight (i.e. from within a
    /// deferred call), consumes and returns the panic value; otherwise returns
    /// null. Lowering routes the builtin <c>recover()</c> here.
    /// </summary>
    public object? Recover()
    {
        if (CurrentPanic is { Recovered: false } p)
        {
            p.Recovered = true;
            return p.Value ?? NilPanic.Instance;
        }
        return null;
    }

    /// <summary>Sentinel for panic(nil) so recover() can distinguish it from "no panic".</summary>
    private sealed class NilPanic { public static readonly NilPanic Instance = new(); }
}

/// <summary>
/// GoRuntime is the entry point for goroutine management. <c>go f()</c> lowers to
/// <see cref="Go"/>, which runs f on the thread pool with its own context and an
/// isolated panic boundary so a crashing goroutine does not take down the process
/// unexpectedly (it prints, as Go would for an unrecovered panic).
/// </summary>
public static class GoRuntime
{
    private static readonly AsyncLocal<GoRoutineContext?> _current = new();

    /// <summary>The calling goroutine's context, created on first access.</summary>
    public static GoRoutineContext Current()
    {
        var ctx = _current.Value;
        if (ctx == null)
        {
            ctx = new GoRoutineContext();
            _current.Value = ctx;
        }
        return ctx;
    }

    /// <summary>go f() — schedule f on a new goroutine.</summary>
    public static void Go(Action fn)
    {
        _ = Task.Run(() =>
        {
            _current.Value = new GoRoutineContext();
            try
            {
                fn();
            }
            catch (GoPanicException p)
            {
                // An unrecovered panic in a goroutine terminates the program in Go.
                Console.Error.WriteLine(p.Message);
                Environment.Exit(2);
            }
        });
    }

    /// <summary>
    /// The closure dispatcher (Program.__invoke) registered at startup. Goroutines
    /// reuse the closure machinery: <c>go f(args)</c> lowers to a GoClosure that
    /// captures the evaluated args, and <see cref="Go(GoClosure)"/> runs it.
    /// </summary>
    private static GoInvoker? _invoker;

    public static void SetInvoker(GoInvoker inv) => _invoker = inv;

    /// <summary>The registered closure dispatcher (used by goroutines and defers).</summary>
    internal static GoInvoker? Invoker => _invoker;

    /// <summary>Invoke a closure value synchronously (for stdlib shims like sync.Once.Do).</summary>
    public static object? Invoke(GoClosure c) => _invoker?.Invoke(c, System.Array.Empty<object?>());

    /// <summary>Invoke a closure with arguments (for func-taking shims like
    /// sort.Search, strings.Map, strings.IndexFunc).</summary>
    public static object? InvokeArgs(GoClosure c, params object?[] args) => _invoker?.Invoke(c, args);

    /// <summary>go f() where the goroutine body is a closure value.</summary>
    public static void Go(GoClosure c)
    {
        var inv = _invoker;
        _ = Task.Run(() =>
        {
            _current.Value = new GoRoutineContext();
            try
            {
                inv?.Invoke(c, System.Array.Empty<object?>());
            }
            catch (GoPanicException p)
            {
                Console.Error.WriteLine(p.Message);
                Environment.Exit(2);
            }
        });
    }
}

/// <summary>Delegate type for the generated closure dispatcher Program.__invoke.</summary>
public delegate object? GoInvoker(GoClosure closure, object?[] args);

/// <summary>
/// GoDefers is the runtime defer stack. A `defer` statement (at any nesting,
/// including inside loops and conditionals) pushes a thunk closure; a function
/// using defer marks the stack on entry and runs everything above that mark, in
/// LIFO order, on both the normal return and the panic-unwind paths.
/// </summary>
public static class GoDefers
{
    public static long Mark() => GoRuntime.Current().Defers.Count;

    public static void Push(GoClosure thunk) => GoRuntime.Current().Defers.Add(thunk);

    public static void Run(long mark)
    {
        var defers = GoRuntime.Current().Defers;
        var inv = GoRuntime.Invoker;
        while (defers.Count > mark)
        {
            var thunk = defers[defers.Count - 1];
            defers.RemoveAt(defers.Count - 1);
            inv?.Invoke(thunk, System.Array.Empty<object?>());
        }
    }
}
