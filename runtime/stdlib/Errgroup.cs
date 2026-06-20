namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>An errgroup.Group (collects the first error from its goroutines).</summary>
public sealed class GoGroup { public readonly object Lock = new(); public int Count; public object? Err; }

/// <summary>Shim for golang.org/x/sync/errgroup.</summary>
public static class Errgroup
{
    public static object NewGroup() => new GoGroup();
    public static object?[] WithContext(object ctx) => new object?[] { new GoGroup(), ctx };

    public static void Group_Go(object go, GoClosure fn)
    {
        var g = (GoGroup)go;
        lock (g.Lock) g.Count++;
        System.Threading.Tasks.Task.Run(() =>
        {
            object? err = null;
            try { err = GoRuntime.InvokeArgs(fn); } catch (GoPanicException) { }
            lock (g.Lock)
            {
                if (err != null && g.Err == null) g.Err = err;
                g.Count--;
                System.Threading.Monitor.PulseAll(g.Lock);
            }
        });
    }

    public static object? Group_Wait(object go)
    {
        var g = (GoGroup)go;
        lock (g.Lock) { while (g.Count > 0) System.Threading.Monitor.Wait(g.Lock); return g.Err; }
    }
}
