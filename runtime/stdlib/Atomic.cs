namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>Shim for Go's <c>sync/atomic</c> (the func API over *intN). Each target
/// is a GoPtr cell; atomicity is provided by locking the cell.</summary>
public static class Atomic
{
    public static long AddInt64(GoPtr p, long d) { lock (p) { long v = System.Convert.ToInt64(p.Value ?? 0L) + d; p.Value = v; return v; } }
    public static long AddInt32(GoPtr p, long d) { lock (p) { int v = (int)(System.Convert.ToInt32(p.Value ?? 0) + d); p.Value = v; return v; } }
    public static ulong AddUint64(GoPtr p, ulong d) { lock (p) { ulong v = unchecked(System.Convert.ToUInt64(p.Value ?? (ulong)0) + d); p.Value = v; return v; } }

    public static long LoadInt64(GoPtr p) { lock (p) return System.Convert.ToInt64(p.Value ?? 0L); }
    public static long LoadInt32(GoPtr p) { lock (p) return System.Convert.ToInt32(p.Value ?? 0); }
    public static ulong LoadUint64(GoPtr p) { lock (p) return System.Convert.ToUInt64(p.Value ?? (ulong)0); }

    public static void StoreInt64(GoPtr p, long v) { lock (p) p.Value = v; }
    public static void StoreInt32(GoPtr p, long v) { lock (p) p.Value = (int)v; }
    public static void StoreUint64(GoPtr p, ulong v) { lock (p) p.Value = v; }

    public static long SwapInt64(GoPtr p, long nv) { lock (p) { long old = System.Convert.ToInt64(p.Value ?? 0L); p.Value = nv; return old; } }
    public static long SwapInt32(GoPtr p, long nv) { lock (p) { int old = System.Convert.ToInt32(p.Value ?? 0); p.Value = (int)nv; return old; } }

    public static bool CompareAndSwapInt64(GoPtr p, long old, long nv) { lock (p) { if (System.Convert.ToInt64(p.Value ?? 0L) == old) { p.Value = nv; return true; } return false; } }
    public static bool CompareAndSwapInt32(GoPtr p, long old, long nv) { lock (p) { if (System.Convert.ToInt32(p.Value ?? 0) == old) { p.Value = (int)nv; return true; } return false; } }
}
