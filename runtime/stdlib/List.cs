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

    public static object? Element_Next(object e) => ((GoElement)e).Nxt;
    public static object? Element_Prev(object e) => ((GoElement)e).Prv;
    public static object? Element_Value(object e) => ((GoElement)e).Value;
}
