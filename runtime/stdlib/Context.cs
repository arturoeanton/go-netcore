namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>A context.Context value. WithValue links a key/value onto a parent;
/// WithCancel/WithTimeout attach a closeable Done channel + a sticky error.</summary>
public sealed class GoContext
{
    public GoContext? Parent;
    public bool HasValue;
    public object? Key, Val;
    public GoChan? DoneCh;     // non-null only for cancelable contexts
    public object? ErrVal;     // set once on cancel/timeout

    public GoChan? Done() => DoneCh ?? Parent?.Done();
    public object? Err() => ErrVal ?? Parent?.Err();

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

    public static object?[] WithTimeout(object parent, long timeout)
    {
        var ctx = new GoContext { Parent = (GoContext)parent, DoneCh = GoChans.Make(0) };
        var cancel = NativeClosures.Make(_ => { ctx.Cancel(CanceledErr); return null; });
        // timeout is a time.Duration (nanoseconds).
        double ms = timeout / 1_000_000.0;
        if (ms > 0)
            System.Threading.Tasks.Task.Delay((int)ms).ContinueWith(_ => ctx.Cancel(DeadlineErr));
        else
            ctx.Cancel(DeadlineErr);
        return new object?[] { ctx, cancel };
    }

    // context.Context method shims (receiver as first arg).
    public static object? Context_Value(object ctx, object? key) => ((GoContext)ctx).Value(key);
    public static object? Context_Err(object ctx) => ((GoContext)ctx).Err();
    public static GoChan? Context_Done(object ctx) => ((GoContext)ctx).Done();
}
