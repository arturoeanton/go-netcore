namespace GoCLR.Stdlib;

/// <summary>Shim for Go's <c>container/heap</c>. A faithful port of the package's
/// algorithm; the heap.Interface methods (Len/Less/Swap/Push/Pop) are reached on the
/// caller's concrete type through the interface method-callback bridge (see Bridge.cs),
/// so no part of the heap is reimplemented per type.</summary>
public static class Heap
{
    private static long Len(object h) => System.Convert.ToInt64(Bridge.CallMethod(h, "Len"));
    private static bool Less(object h, long i, long j) => (bool)Bridge.CallMethod(h, "Less", i, j)!;
    private static void Swap(object h, long i, long j) => Bridge.CallMethod(h, "Swap", i, j);
    private static void PushItem(object h, object? x) => Bridge.CallMethod(h, "Push", x);
    private static object? PopItem(object h) => Bridge.CallMethod(h, "Pop");

    // heap.Init(h): establish the heap invariants over an arbitrary order, O(n).
    public static void Init(object h)
    {
        long n = Len(h);
        for (long i = n / 2 - 1; i >= 0; i--) Down(h, i, n);
    }

    // heap.Push(h, x): append x then sift up.
    public static void Push(object h, object? x)
    {
        PushItem(h, x);
        Up(h, Len(h) - 1);
    }

    // heap.Pop(h) any: remove and return the minimum (root).
    public static object? Pop(object h)
    {
        long n = Len(h) - 1;
        Swap(h, 0, n);
        Down(h, 0, n);
        return PopItem(h);
    }

    // heap.Remove(h, i) any: remove and return element i.
    public static object? Remove(object h, long i)
    {
        long n = Len(h) - 1;
        if (n != i)
        {
            Swap(h, i, n);
            if (!Down(h, i, n)) Up(h, i);
        }
        return PopItem(h);
    }

    // heap.Fix(h, i): re-establish the ordering after element i changed.
    public static void Fix(object h, long i)
    {
        if (!Down(h, i, Len(h))) Up(h, i);
    }

    private static void Up(object h, long j)
    {
        while (true)
        {
            long i = (j - 1) / 2; // parent
            if (i == j || !Less(h, j, i)) break;
            Swap(h, i, j);
            j = i;
        }
    }

    private static bool Down(object h, long i0, long n)
    {
        long i = i0;
        while (true)
        {
            long j1 = 2 * i + 1;
            if (j1 >= n || j1 < 0) break; // j1 < 0 after long overflow
            long j = j1;
            long j2 = j1 + 1;
            if (j2 < n && Less(h, j2, j1)) j = j2;
            if (!Less(h, j, i)) break;
            Swap(h, i, j);
            i = j;
        }
        return i > i0;
    }
}
