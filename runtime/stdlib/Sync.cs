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

    // sync.OnceFunc/OnceValue/OnceValues: return a closure that runs f at most once,
    // caching its result for OnceValue(s).
    public static GoClosure OnceFunc(GoClosure f)
    {
        var done = new bool[1];
        var gate = new object();
        return NativeClosures.Make(_ => { lock (gate) { if (!done[0]) { done[0] = true; GoRuntime.Invoke(f); } } return null; });
    }
    public static GoClosure OnceValue(GoClosure f)
    {
        var done = new bool[1];
        var cache = new object?[1];
        var gate = new object();
        return NativeClosures.Make(_ => { lock (gate) { if (!done[0]) { done[0] = true; cache[0] = GoRuntime.Invoke(f); } } return cache[0]; });
    }
    public static GoClosure OnceValues(GoClosure f)
    {
        var done = new bool[1];
        var cache = new object?[1];
        var gate = new object();
        return NativeClosures.Make(_ => { lock (gate) { if (!done[0]) { done[0] = true; cache[0] = GoRuntime.Invoke(f); } } return cache[0]; });
    }

    // sync.WaitGroup.Go(f) (Go 1.25): Add(1), run f in a goroutine, Done on return.
    public static void WaitGroup_Go(object w, GoClosure f)
    {
        WaitGroup_Add(w, 1);
        GoRuntime.Go(() => { try { GoRuntime.Invoke(f); } finally { WaitGroup_Done(w); } });
    }

    public static void Map_Store(object m, object key, object? val) => ((GoSyncMap)m).D[key] = val;
    public static object?[] Map_Load(object m, object key) =>
        ((GoSyncMap)m).D.TryGetValue(key, out var v) ? new object?[] { v, true } : new object?[] { null, false };
    public static void Map_Delete(object m, object key) => ((GoSyncMap)m).D.TryRemove(key, out _);
    // sync.Map.Range(f func(key, value any) bool): call f for each entry; stop if it
    // returns false. Iterate a snapshot so f may Store/Delete during the walk (as Go allows).
    public static void Map_Range(object m, GoClosure f)
    {
        foreach (var kv in System.Linq.Enumerable.ToArray(((GoSyncMap)m).D))
        {
            var cont = GoRuntime.InvokeArgs(f, kv.Key, kv.Value);
            if (cont is bool b && !b) break;
        }
    }
    public static object?[] Map_LoadOrStore(object m, object key, object? val)
    {
        var d = ((GoSyncMap)m).D;
        bool loaded = true;
        var actual = d.GetOrAdd(key, _ => { loaded = false; return val; });
        return new object?[] { actual, loaded };
    }
    public static object?[] Map_LoadAndDelete(object m, object key) =>
        ((GoSyncMap)m).D.TryRemove(key, out var v) ? new object?[] { v, true } : new object?[] { null, false };
    // sync.Map.Swap(key, value) (previous any, loaded bool).
    public static object?[] Map_Swap(object m, object key, object? val)
    {
        var d = ((GoSyncMap)m).D;
        bool loaded = d.TryGetValue(key, out var prev);
        d[key] = val;
        return new object?[] { loaded ? prev : null, loaded };
    }
    // sync.Map.CompareAndSwap(key, old, new) bool — value equality via the default comparer.
    public static bool Map_CompareAndSwap(object m, object key, object? old, object? nw) =>
        ((GoSyncMap)m).D.TryUpdate(key, nw, old);
    // sync.Map.CompareAndDelete(key, old) bool.
    public static bool Map_CompareAndDelete(object m, object key, object? old) =>
        ((System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<object, object?>>)((GoSyncMap)m).D)
            .Remove(new System.Collections.Generic.KeyValuePair<object, object?>(key, old));
    public static void Map_Clear(object m) => ((GoSyncMap)m).D.Clear();

    // sync.Cond: condition variable. L is the associated Locker. goclr's Wait/Signal
    // use a .NET monitor; the associated lock is not released across Wait (that would
    // require an interface back-call), so this is a best-effort Cond — adequate for the
    // x/net/http2 pipe, which is dead weight under the shimmed HttpListener server.
    public static object NewCond(object? l) => new GoCond { L = l };
    public static object NewCondZero() => new GoCond();
    public static object? Cond_L(object c) => ((GoCond)c).L;
    public static void Cond_SetL(object c, object? l) => ((GoCond)c).L = l;
    // sync.Cond.Wait: atomically release the associated Locker (c.L) and block until a
    // Signal/Broadcast, then re-acquire it — exactly as Go does. The previous version waited
    // on a private monitor WITHOUT releasing c.L, so any goroutine needing c.L deadlocked.
    // Holding c.Mon across the unlock closes the lost-wakeup window: a concurrent Signal must
    // take c.Mon to Pulse, so it can't fire between the unlock and Monitor.Wait (which
    // atomically releases c.Mon while blocking).
    public static void Cond_Wait(object co)
    {
        var c = (GoCond)co;
        System.Threading.Monitor.Enter(c.Mon);
        try
        {
            CondUnlock(c.L);
            System.Threading.Monitor.Wait(c.Mon);
        }
        finally
        {
            System.Threading.Monitor.Exit(c.Mon);
            CondLock(c.L);
        }
    }

    // Drive Lock()/Unlock() on a sync.Cond's Locker (a *sync.Mutex / *sync.RWMutex, possibly
    // behind a GoPtr, or a user Locker reached through the callback bridge).
    private static void CondLock(object? l)
    {
        switch (l is GoPtr p ? GoPtrs.Get(p) : l)
        {
            case GoMutex m: m.Sem.Wait(); break;
            case GoRWMutex rw: rw.Sem.Wait(); break;
            default: if (l != null && Bridge.HasMethod(l, "Lock")) Bridge.CallMethod(l, "Lock"); break;
        }
    }
    private static void CondUnlock(object? l)
    {
        switch (l is GoPtr p ? GoPtrs.Get(p) : l)
        {
            case GoMutex m: m.Sem.Release(); break;
            case GoRWMutex rw: rw.Sem.Release(); break;
            default: if (l != null && Bridge.HasMethod(l, "Unlock")) Bridge.CallMethod(l, "Unlock"); break;
        }
    }
    public static void Cond_Signal(object co) { var c = (GoCond)co; lock (c.Mon) System.Threading.Monitor.Pulse(c.Mon); }
    public static void Cond_Broadcast(object co) { var c = (GoCond)co; lock (c.Mon) System.Threading.Monitor.PulseAll(c.Mon); }
}
