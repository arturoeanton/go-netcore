namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>A context.Context value. WithValue links a key/value onto a parent;
/// WithCancel/WithTimeout attach a closeable Done channel + a sticky error.</summary>
[GoShim("context.Context")]
public sealed class GoContext
{
    public GoContext? Parent;
    public bool HasValue;
    public object? Key, Val;
    public GoChan? DoneCh;     // non-null only for cancelable contexts
    public object? ErrVal;     // set once on cancel/timeout
    public object? CauseVal;   // the cancel cause (WithCancelCause); == ErrVal for a plain cancel
    public bool NoCancel;      // WithoutCancel: never done, never errs, but parent's values remain
    public bool HasDeadline;   // set by WithTimeout/WithDeadline
    public long DeadlineNs;    // the deadline (ns since Unix epoch) when HasDeadline

    public GoChan? Done() => NoCancel ? null : (DoneCh ?? Parent?.Done());

    // Deadline() (time, ok): the nearest ancestor with a deadline wins (WithCancel/WithValue
    // forward to their parent, as in Go). WithoutCancel severs inherited deadlines.
    public (long ns, bool ok) DeadlineOf()
    {
        for (var c = this; c != null; c = c.Parent)
        {
            if (c.NoCancel) return (0, false);
            if (c.HasDeadline) return (c.DeadlineNs, true);
        }
        return (0, false);
    }
    public object? Err() => NoCancel ? null : (ErrVal ?? Parent?.Err());

    public object? Cause()
    {
        for (var c = this; c != null; c = c.Parent)
            if (c.CauseVal != null) return c.CauseVal;
        return Err();
    }

    public object? Value(object? key)
    {
        for (var c = this; c != null; c = c.Parent)
            if (c.HasValue && KeysEqual(c.Key, key)) return c.Val;
        return null;
    }

    private static bool KeysEqual(object? a, object? b)
    {
        if (a is GoString ga && b is GoString gb) return ga.ToDotNetString() == gb.ToDotNetString();
        return object.Equals(a, b);
    }

    public void Cancel(object err)
    {
        lock (this)
        {
            if (ErrVal != null) return;
            ErrVal = err;
            CauseVal = err;
            DoneCh?.Close();
        }
    }

    // CancelErrCause: set a specific Err() (e.g. DeadlineExceeded) while Cause() reports the
    // supplied cause (or Err itself when the cause is nil). Used by With*Cause timeouts.
    public void CancelErrCause(object err, object? cause)
    {
        lock (this)
        {
            if (ErrVal != null) return;
            ErrVal = err;
            CauseVal = cause ?? err;
            DoneCh?.Close();
        }
    }

    // CancelCause: Err() stays Canceled, but Cause() reports the supplied cause (or Canceled
    // when the cause is nil), as Go's context.WithCancelCause does.
    public void CancelCause(object? cause)
    {
        lock (this)
        {
            if (ErrVal != null) return;
            ErrVal = Context.Canceled();
            CauseVal = cause ?? Context.Canceled();
            DoneCh?.Close();
        }
    }
}

/// <summary>Shim for a subset of Go's <c>context</c> package.</summary>
public static class Context
{
    // Sentinel errors (singletons so `ctx.Err() == context.Canceled` works).
    private static readonly object CanceledErr = new GoError(GoString.FromDotNetString("context canceled"));
    private static readonly object DeadlineErr = new GoError(GoString.FromDotNetString("context deadline exceeded"));
    public static object Canceled() => CanceledErr;
    public static object DeadlineExceeded() => DeadlineErr;

    public static object Background() => new GoContext();
    public static object TODO() => new GoContext();

    public static object WithValue(object parent, object? key, object? val) =>
        new GoContext { Parent = (GoContext)parent, HasValue = true, Key = key, Val = val };

    public static object?[] WithCancel(object parent)
    {
        var ctx = new GoContext { Parent = (GoContext)parent, DoneCh = GoChans.Make(0) };
        var cancel = NativeClosures.Make(_ => { ctx.Cancel(CanceledErr); return null; });
        return new object?[] { ctx, cancel };
    }

    // WithCancelCause(parent) (ctx, CancelCauseFunc): the cancel func takes a cause error.
    public static object?[] WithCancelCause(object parent)
    {
        var ctx = new GoContext { Parent = (GoContext)parent, DoneCh = GoChans.Make(0) };
        var cancel = NativeClosures.Make(a => { ctx.CancelCause(a != null && a.Length > 0 ? a[0] : null); return null; });
        return new object?[] { ctx, cancel };
    }

    // context.Cause(ctx): the cancellation cause, or nil if not cancelled.
    public static object? Cause(object ctx) => ((GoContext)ctx).Cause();

    public static object?[] WithTimeout(object parent, long timeout)
    {
        var ctx = new GoContext { Parent = (GoContext)parent, DoneCh = GoChans.Make(0) };
        ctx.HasDeadline = true; ctx.DeadlineNs = NowNs() + timeout;
        var cancel = NativeClosures.Make(_ => { ctx.Cancel(CanceledErr); return null; });
        // timeout is a time.Duration (nanoseconds).
        double ms = timeout / 1_000_000.0;
        if (ms > 0)
            System.Threading.Tasks.Task.Delay((int)ms).ContinueWith(_ => ctx.Cancel(DeadlineErr));
        else
            ctx.Cancel(DeadlineErr);
        return new object?[] { ctx, cancel };
    }

    // Current time as nanoseconds since the Unix epoch (matching GoTime.N).
    private static long NowNs() => (System.DateTime.UtcNow - new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc)).Ticks * 100;

    // context.WithDeadline(parent, deadline time.Time) (Context, CancelFunc): cancel with
    // DeadlineExceeded when the deadline passes. deadline is a GoTime (nanoseconds in .N).
    public static object?[] WithDeadline(object parent, object? deadline)
    {
        var ctx = new GoContext { Parent = (GoContext)parent, DoneCh = GoChans.Make(0) };
        var cancel = NativeClosures.Make(_ => { ctx.Cancel(CanceledErr); return null; });
        long nowNs = NowNs();
        long deadlineNs = deadline is GoTime t ? t.N : nowNs;
        ctx.HasDeadline = true; ctx.DeadlineNs = deadlineNs;
        double ms = (deadlineNs - nowNs) / 1_000_000.0;
        if (ms > 0)
            System.Threading.Tasks.Task.Delay((int)ms).ContinueWith(_ => ctx.Cancel(DeadlineErr));
        else
            ctx.Cancel(DeadlineErr);
        return new object?[] { ctx, cancel };
    }

    // context.WithTimeoutCause / WithDeadlineCause: like WithTimeout/WithDeadline but the
    // deadline cancellation reports `cause` via context.Cause (Err stays DeadlineExceeded).
    public static object?[] WithTimeoutCause(object parent, long timeout, object? cause)
    {
        var ctx = new GoContext { Parent = (GoContext)parent, DoneCh = GoChans.Make(0) };
        ctx.HasDeadline = true; ctx.DeadlineNs = NowNs() + timeout;
        var cancel = NativeClosures.Make(_ => { ctx.Cancel(CanceledErr); return null; });
        double ms = timeout / 1_000_000.0;
        if (ms > 0) System.Threading.Tasks.Task.Delay((int)ms).ContinueWith(_ => ctx.CancelErrCause(DeadlineErr, cause));
        else ctx.CancelErrCause(DeadlineErr, cause);
        return new object?[] { ctx, cancel };
    }
    public static object?[] WithDeadlineCause(object parent, object? deadline, object? cause)
    {
        var ctx = new GoContext { Parent = (GoContext)parent, DoneCh = GoChans.Make(0) };
        var cancel = NativeClosures.Make(_ => { ctx.Cancel(CanceledErr); return null; });
        long nowNs = NowNs();
        long deadlineNs = deadline is GoTime t ? t.N : nowNs;
        ctx.HasDeadline = true; ctx.DeadlineNs = deadlineNs;
        double ms = (deadlineNs - nowNs) / 1_000_000.0;
        if (ms > 0) System.Threading.Tasks.Task.Delay((int)ms).ContinueWith(_ => ctx.CancelErrCause(DeadlineErr, cause));
        else ctx.CancelErrCause(DeadlineErr, cause);
        return new object?[] { ctx, cancel };
    }

    // context.WithoutCancel(parent): a context that is never cancelled (Done()==nil, Err()==nil)
    // but still resolves the parent's values.
    public static object WithoutCancel(object parent) =>
        new GoContext { Parent = (GoContext)parent, NoCancel = true };

    // context.AfterFunc(ctx, f) (stop func() bool): run f in its own goroutine once ctx is done;
    // stop() cancels that, returning true if it prevented f from running.
    public static GoClosure AfterFunc(object ctxo, GoClosure f)
    {
        var ctx = (GoContext)ctxo;
        var gate = new object();
        bool stopped = false, ran = false;
        if (ctx.Done() != null)
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                while (ctx.Err() == null) System.Threading.Thread.Sleep(1);
                lock (gate) { if (stopped) return; ran = true; }
                GoRuntime.Invoke(f);
            });
        }
        return NativeClosures.Make(_ =>
        {
            lock (gate) { if (ran || stopped) return (object)false; stopped = true; return (object)true; }
        });
    }

    // context.Context method shims (receiver as first arg).
    public static object? Context_Value(object ctx, object? key) => ((GoContext)ctx).Value(key);
    public static object? Context_Err(object ctx) => ((GoContext)ctx).Err();
    public static GoChan? Context_Done(object ctx) => ((GoContext)ctx).Done();
    // (ctx).Deadline() (time.Time, bool): the deadline + true for a timeout/deadline context
    // (or an ancestor's), else the zero time + false.
    public static object?[] Context_Deadline(object ctx)
    {
        var (ns, ok) = ((GoContext)ctx).DeadlineOf();
        return new object?[] { ok ? new GoTime { N = ns, IsZero = false } : new GoTime { N = 0, IsZero = true }, ok };
    }
}
