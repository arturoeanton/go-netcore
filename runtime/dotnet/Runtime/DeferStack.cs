namespace GoCLR.Runtime;

/// <summary>
/// DeferStack holds the deferred calls registered within a single function
/// activation (and, per Go semantics, is unwound in LIFO order on return or
/// panic). Lowering emits, for each function that uses <c>defer</c>, a try/finally
/// where the finally block calls <see cref="RunAll"/>.
/// </summary>
public sealed class DeferStack
{
    private readonly List<Action> _calls = new();

    /// <summary>Registers a deferred call. Arguments are captured by the closure at defer time, as in Go.</summary>
    public void Push(Action call) => _calls.Add(call);

    public int Count => _calls.Count;

    /// <summary>
    /// Runs deferred calls in LIFO order. If a deferred call panics, the new
    /// panic replaces the in-flight one but the remaining defers still run —
    /// matching Go. Re-throws the surviving panic if it was not recovered.
    /// </summary>
    public void RunAll(GoRoutineContext ctx)
    {
        for (int i = _calls.Count - 1; i >= 0; i--)
        {
            try
            {
                _calls[i]();
            }
            catch (GoPanicException p)
            {
                ctx.CurrentPanic = p;
            }
        }
        _calls.Clear();

        if (ctx.CurrentPanic is { Recovered: false } surviving)
        {
            ctx.CurrentPanic = null;
            throw surviving;
        }
        ctx.CurrentPanic = null;
    }
}
