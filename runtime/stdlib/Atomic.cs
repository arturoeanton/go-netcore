namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>Shim for Go's <c>sync/atomic</c> (the func API over *intN). The target
/// is a GoPtr — either a cell (Value) or a field alias (&amp;s.field, whose getter/
/// setter re-navigate the field's stable container). Because a field alias is a
/// fresh GoPtr per &amp;-expression, per-pointer locking can't serialize accesses to
/// the same field; a single process-wide lock does, and every access goes through
/// GoPtrs.Get/Set so the field alias reads/writes the real storage. Conservative
/// (one lock) but race-free.</summary>
public static class Atomic
{
    private static readonly object Gate = new();

    private static long I64(GoPtr p) => System.Convert.ToInt64(GoPtrs.Get(p) ?? 0L);
    private static int I32(GoPtr p) => System.Convert.ToInt32(GoPtrs.Get(p) ?? 0);
    private static ulong U64(GoPtr p) => System.Convert.ToUInt64(GoPtrs.Get(p) ?? (ulong)0);
    private static uint U32(GoPtr p) => System.Convert.ToUInt32(GoPtrs.Get(p) ?? (uint)0);

    // Int32/Uint32 variants take and return int/uint to match Go's int32/uint32 (the
    // extern signature goclr derives from the Go type); 64-bit variants use long/ulong.
    public static long AddInt64(GoPtr p, long d) { lock (Gate) { long v = I64(p) + d; GoPtrs.Set(p, v); return v; } }
    public static int AddInt32(GoPtr p, int d) { lock (Gate) { int v = unchecked(I32(p) + d); GoPtrs.Set(p, v); return v; } }
    public static ulong AddUint64(GoPtr p, ulong d) { lock (Gate) { ulong v = unchecked(U64(p) + d); GoPtrs.Set(p, v); return v; } }

    public static long LoadInt64(GoPtr p) { lock (Gate) return I64(p); }
    public static int LoadInt32(GoPtr p) { lock (Gate) return I32(p); }
    public static ulong LoadUint64(GoPtr p) { lock (Gate) return U64(p); }

    public static void StoreInt64(GoPtr p, long v) { lock (Gate) GoPtrs.Set(p, v); }
    public static void StoreInt32(GoPtr p, int v) { lock (Gate) GoPtrs.Set(p, v); }
    public static void StoreUint64(GoPtr p, ulong v) { lock (Gate) GoPtrs.Set(p, v); }

    public static long SwapInt64(GoPtr p, long nv) { lock (Gate) { long old = I64(p); GoPtrs.Set(p, nv); return old; } }
    public static int SwapInt32(GoPtr p, int nv) { lock (Gate) { int old = I32(p); GoPtrs.Set(p, nv); return old; } }

    public static bool CompareAndSwapInt64(GoPtr p, long old, long nv) { lock (Gate) { if (I64(p) == old) { GoPtrs.Set(p, nv); return true; } return false; } }
    public static bool CompareAndSwapInt32(GoPtr p, int old, int nv) { lock (Gate) { if (I32(p) == old) { GoPtrs.Set(p, nv); return true; } return false; } }

    // Uint32 variants.
    public static uint AddUint32(GoPtr p, uint d) { lock (Gate) { uint v = unchecked(U32(p) + d); GoPtrs.Set(p, v); return v; } }
    public static uint LoadUint32(GoPtr p) { lock (Gate) return U32(p); }
    public static void StoreUint32(GoPtr p, uint v) { lock (Gate) GoPtrs.Set(p, v); }
    public static ulong SwapUint64(GoPtr p, ulong nv) { lock (Gate) { ulong old = U64(p); GoPtrs.Set(p, nv); return old; } }
    public static bool CompareAndSwapUint64(GoPtr p, ulong old, ulong nv) { lock (Gate) { if (U64(p) == old) { GoPtrs.Set(p, nv); return true; } return false; } }
    public static bool CompareAndSwapUint32(GoPtr p, uint old, uint nv) { lock (Gate) { if (U32(p) == old) { GoPtrs.Set(p, nv); return true; } return false; } }

    // sync/atomic.Value: atomically holds an interface{} value.
    public static object NewValue() => new GoAtomicValue();
    public static object? Value_Load(object v) { lock (Gate) return ((GoAtomicValue)v).Val; }
    public static void Value_Store(object v, object? x) { lock (Gate) ((GoAtomicValue)v).Val = x; }
    public static object? Value_Swap(object v, object? x) { lock (Gate) { var a = (GoAtomicValue)v; var old = a.Val; a.Val = x; return old; } }
    public static bool Value_CompareAndSwap(object v, object? old, object? nw) { lock (Gate) { var a = (GoAtomicValue)v; if (System.Object.Equals(a.Val, old)) { a.Val = nw; return true; } return false; } }
}

/// <summary>A sync/atomic.Value holding one interface value.</summary>
public sealed class GoAtomicValue { public object? Val; }
