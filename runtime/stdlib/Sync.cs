namespace GoCLR.Stdlib;

using System.Threading;
using GoCLR.Runtime;

[GoShim("sync.Mutex")]
public sealed class GoMutex { public readonly SemaphoreSlim Sem = new(1, 1); }

[GoShim("sync.Cond")]
public sealed class GoCond { public object? L; public readonly object Mon = new(); }
[GoShim("sync.RWMutex")]
public sealed class GoRWMutex { public readonly SemaphoreSlim Sem = new(1, 1); }
[GoShim("sync.WaitGroup")]
public sealed class GoWaitGroup { public int Count; public readonly object Gate = new(); }
[GoShim("sync.Once")]
public sealed class GoOnce { public int Done; public readonly object Gate = new(); }
[GoShim("sync.Map")]
public sealed class GoSyncMap { public readonly System.Collections.Concurrent.ConcurrentDictionary<object, object?> D = new(); }

/// <summary>sync.Pool: a free-list of reusable objects with an optional New factory.</summary>
[GoShim("sync.Pool")]
public sealed class GoPool
{
    public GoClosure? New;
    public readonly System.Collections.Concurrent.ConcurrentStack<object?> Items = new();
}

/// <summary>Shim for Go's <c>sync</c> package over .NET synchronization.</summary>
public static class Sync
{
    public static object NewPool() => new GoPool();
    public static object NewPoolWith(GoClosure newFn) => new GoPool { New = newFn };
    public static object? Pool_Get(object p)
    {
        var pool = (GoPool)p;
        if (pool.Items.TryPop(out var v)) return v;
        return pool.New != null ? GoRuntime.InvokeArgs(pool.New) : null;
    }
    public static void Pool_Put(object p, object? v)
    {
        if (v != null) ((GoPool)p).Items.Push(v);
    }
    // sync.Pool.New is a field of func() any type; gin assigns it after construction.
    public static void Pool_SetNew(object p, GoClosure? fn) => ((GoPool)p).New = fn;
    public static GoClosure? Pool_New(object p) => ((GoPool)p).New;

    public static object NewMutex() => new GoMutex();
    public static object NewRWMutex() => new GoRWMutex();
    public static object NewWaitGroup() => new GoWaitGroup();
    public static object NewOnce() => new GoOnce();
    public static object NewMap() => new GoSyncMap();

    public static void Mutex_Lock(object m) => ((GoMutex)m).Sem.Wait();
    public static void Mutex_Unlock(object m) => ((GoMutex)m).Sem.Release();
    public static bool Mutex_TryLock(object m) => ((GoMutex)m).Sem.Wait(0);

    public static void RWMutex_Lock(object m) => ((GoRWMutex)m).Sem.Wait();
    public static void RWMutex_Unlock(object m) => ((GoRWMutex)m).Sem.Release();
    public static void RWMutex_RLock(object m) => ((GoRWMutex)m).Sem.Wait();
    public static void RWMutex_RUnlock(object m) => ((GoRWMutex)m).Sem.Release();
    public static bool RWMutex_TryLock(object m) => ((GoRWMutex)m).Sem.Wait(0);
    public static bool RWMutex_TryRLock(object m) => ((GoRWMutex)m).Sem.Wait(0);
    // RLocker() sync.Locker: a Locker view whose Lock/Unlock take the read lock. goclr's
    // RWMutex is a single semaphore, so the view is the mutex itself (using the write
    // lock for reads is conservative but correct).
    public static object RWMutex_RLocker(object m) => m;

    public static void WaitGroup_Add(object w, long delta)
    {
        var g = (GoWaitGroup)w;
        lock (g.Gate) { g.Count += (int)delta; if (g.Count <= 0) Monitor.PulseAll(g.Gate); }
    }
    public static void WaitGroup_Done(object w) => WaitGroup_Add(w, -1);
    public static void WaitGroup_Wait(object w)
    {
        var g = (GoWaitGroup)w;
        lock (g.Gate) { while (g.Count > 0) Monitor.Wait(g.Gate); }
    }

    public static void Once_Do(object o, GoClosure f)
    {
        var once = (GoOnce)o;
        lock (once.Gate) { if (once.Done == 0) { once.Done = 1; GoRuntime.Invoke(f); } }
    }

    public static void Map_Store(object m, object key, object? val) => ((GoSyncMap)m).D[key] = val;
    public static object?[] Map_Load(object m, object key) =>
        ((GoSyncMap)m).D.TryGetValue(key, out var v) ? new object?[] { v, true } : new object?[] { null, false };
    public static void Map_Delete(object m, object key) => ((GoSyncMap)m).D.TryRemove(key, out _);
    public static object?[] Map_LoadOrStore(object m, object key, object? val)
    {
        var d = ((GoSyncMap)m).D;
        bool loaded = true;
        var actual = d.GetOrAdd(key, _ => { loaded = false; return val; });
        return new object?[] { actual, loaded };
    }
    public static object?[] Map_LoadAndDelete(object m, object key) =>
        ((GoSyncMap)m).D.TryRemove(key, out var v) ? new object?[] { v, true } : new object?[] { null, false };

    // sync.Cond: condition variable. L is the associated Locker. goclr's Wait/Signal
    // use a .NET monitor; the associated lock is not released across Wait (that would
    // require an interface back-call), so this is a best-effort Cond — adequate for the
    // x/net/http2 pipe, which is dead weight under the shimmed HttpListener server.
    public static object NewCond(object? l) => new GoCond { L = l };
    public static object NewCondZero() => new GoCond();
    public static object? Cond_L(object c) => ((GoCond)c).L;
    public static void Cond_SetL(object c, object? l) => ((GoCond)c).L = l;
    public static void Cond_Wait(object co) { var c = (GoCond)co; lock (c.Mon) System.Threading.Monitor.Wait(c.Mon, 50); }
    public static void Cond_Signal(object co) { var c = (GoCond)co; lock (c.Mon) System.Threading.Monitor.Pulse(c.Mon); }
    public static void Cond_Broadcast(object co) { var c = (GoCond)co; lock (c.Mon) System.Threading.Monitor.PulseAll(c.Mon); }
}
