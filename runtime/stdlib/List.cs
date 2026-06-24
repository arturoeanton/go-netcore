namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>A container/list.Element (Value field + Next/Prev links).</summary>
public sealed class GoElement { public object? Value; public GoElement? Nxt, Prv; public GoList? List; }

/// <summary>A container/list.List (doubly-linked, with a sentinel-free impl).</summary>
public sealed class GoList { public GoElement? Head, Tail; public int N; }

/// <summary>Shim for Go's <c>container/list</c>.</summary>
public static class List
{
    public static object New() => new GoList();

    public static long List_Len(object l) => ((GoList)l).N;
    public static object? List_Front(object l) => ((GoList)l).Head;
    public static object? List_Back(object l) => ((GoList)l).Tail;

    public static object List_PushBack(object lo, object? v)
    {
        var l = (GoList)lo;
        var e = new GoElement { Value = v, List = l, Prv = l.Tail };
        if (l.Tail != null) l.Tail.Nxt = e; else l.Head = e;
        l.Tail = e; l.N++;
        return e;
    }
    public static object List_PushFront(object lo, object? v)
    {
        var l = (GoList)lo;
        var e = new GoElement { Value = v, List = l, Nxt = l.Head };
        if (l.Head != null) l.Head.Prv = e; else l.Tail = e;
        l.Head = e; l.N++;
        return e;
    }
    public static object? List_Remove(object lo, object eo)
    {
        var l = (GoList)lo; var e = (GoElement)eo;
        if (e.Prv != null) e.Prv.Nxt = e.Nxt; else l.Head = e.Nxt;
        if (e.Nxt != null) e.Nxt.Prv = e.Prv; else l.Tail = e.Prv;
        l.N--; e.List = null;
        return e.Value;
    }

    // unlink removes e from its list's chain without touching N (for moves).
    private static void Unlink(GoList l, GoElement e)
    {
        if (e.Prv != null) e.Prv.Nxt = e.Nxt; else l.Head = e.Nxt;
        if (e.Nxt != null) e.Nxt.Prv = e.Prv; else l.Tail = e.Prv;
        e.Nxt = e.Prv = null;
    }

    public static void List_MoveToFront(object lo, object eo)
    {
        var l = (GoList)lo; var e = (GoElement)eo;
        if (e.List != l || l.Head == e) return;
        Unlink(l, e);
        e.Nxt = l.Head;
        if (l.Head != null) l.Head.Prv = e; else l.Tail = e;
        l.Head = e;
    }
    public static void List_MoveToBack(object lo, object eo)
    {
        var l = (GoList)lo; var e = (GoElement)eo;
        if (e.List != l || l.Tail == e) return;
        Unlink(l, e);
        e.Prv = l.Tail;
        if (l.Tail != null) l.Tail.Nxt = e; else l.Head = e;
        l.Tail = e;
    }
    public static object List_Init(object lo)
    {
        var l = (GoList)lo; l.Head = l.Tail = null; l.N = 0;
        return l;
    }
    public static object List_InsertBefore(object lo, object? v, object mark)
    {
        var l = (GoList)lo; var m = (GoElement)mark;
        var e = new GoElement { Value = v, List = l, Prv = m.Prv, Nxt = m };
        if (m.Prv != null) m.Prv.Nxt = e; else l.Head = e;
        m.Prv = e; l.N++;
        return e;
    }
    public static object List_InsertAfter(object lo, object? v, object mark)
    {
        var l = (GoList)lo; var m = (GoElement)mark;
        var e = new GoElement { Value = v, List = l, Nxt = m.Nxt, Prv = m };
        if (m.Nxt != null) m.Nxt.Prv = e; else l.Tail = e;
        m.Nxt = e; l.N++;
        return e;
    }

    // MoveBefore/MoveAfter: relocate e (already in l) adjacent to mark. No-op if e isn't in
    // l, mark isn't in l, or e == mark — matching Go. N is unchanged (a move, not insert).
    public static void List_MoveBefore(object lo, object eo, object marko)
    {
        var l = (GoList)lo; var e = (GoElement)eo; var m = (GoElement)marko;
        if (e.List != l || e == m || m.List != l) return;
        Unlink(l, e);
        e.Nxt = m; e.Prv = m.Prv;
        if (m.Prv != null) m.Prv.Nxt = e; else l.Head = e;
        m.Prv = e;
    }
    public static void List_MoveAfter(object lo, object eo, object marko)
    {
        var l = (GoList)lo; var e = (GoElement)eo; var m = (GoElement)marko;
        if (e.List != l || e == m || m.List != l) return;
        Unlink(l, e);
        e.Prv = m; e.Nxt = m.Nxt;
        if (m.Nxt != null) m.Nxt.Prv = e; else l.Tail = e;
        m.Nxt = e;
    }

    // PushBackList/PushFrontList: append copies of other's values. The element count is
    // snapshotted first so other == l (self-append) terminates, as in Go.
    public static void List_PushBackList(object lo, object oo)
    {
        var l = (GoList)lo; var other = (GoList)oo;
        int n = other.N; var e = other.Head;
        for (int i = 0; i < n && e != null; i++) { List_PushBack(l, e.Value); e = e.Nxt; }
    }
    public static void List_PushFrontList(object lo, object oo)
    {
        var l = (GoList)lo; var other = (GoList)oo;
        int n = other.N; var e = other.Tail;
        for (int i = 0; i < n && e != null; i++) { List_PushFront(l, e.Value); e = e.Prv; }
    }

    public static object? Element_Next(object e) => ((GoElement)e).Nxt;
    public static object? Element_Prev(object e) => ((GoElement)e).Prv;
    public static object? Element_Value(object e) => ((GoElement)e).Value;
}
