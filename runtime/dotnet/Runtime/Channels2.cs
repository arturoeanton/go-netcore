namespace GoCLR.Runtime;

/// <summary>
/// GoChan is the non-generic channel the M2 backend emits (a reference type, like
/// Go channels). Elements are stored boxed in a queue guarded by a monitor, so one
/// representation serves every element type. Buffered channels block the sender
/// when full; unbuffered channels (cap 0) block the sender until a receiver takes
/// the value (a simple rendezvous).
/// </summary>
public sealed class GoChan
{
    private readonly System.Collections.Generic.Queue<object?> buf = new();
    private readonly object gate = new();
    private bool closed;
    private readonly int cap;

    public GoChan(int capacity) { cap = capacity; }

    public void Send(object? v)
    {
        lock (gate)
        {
            int effective = cap == 0 ? 1 : cap;
            while (true)
            {
                if (closed) throw new GoPanicException(GoString.FromDotNetString("send on closed channel"));
                if (buf.Count < effective) break;
                System.Threading.Monitor.Wait(gate);
            }
            buf.Enqueue(v);
            System.Threading.Monitor.PulseAll(gate);
            if (cap == 0)
            {
                // Unbuffered: wait until the value has been received.
                while (buf.Count > 0 && !closed) System.Threading.Monitor.Wait(gate);
            }
        }
    }

    public (object? value, bool ok) Recv2()
    {
        lock (gate)
        {
            while (buf.Count == 0)
            {
                if (closed) return (null, false);
                System.Threading.Monitor.Wait(gate);
            }
            var v = buf.Dequeue();
            System.Threading.Monitor.PulseAll(gate);
            return (v, true);
        }
    }

    public object? Recv() => Recv2().value;

    public void Close()
    {
        lock (gate)
        {
            if (closed) throw new GoPanicException(GoString.FromDotNetString("close of closed channel"));
            closed = true;
            System.Threading.Monitor.PulseAll(gate);
        }
    }

    /// <summary>Non-blocking receive for select: ready when a value is buffered or the channel is closed.</summary>
    public bool TryRecv(out object? value, out bool ok)
    {
        lock (gate)
        {
            if (buf.Count > 0)
            {
                value = buf.Dequeue();
                ok = true;
                System.Threading.Monitor.PulseAll(gate);
                return true;
            }
            if (closed) { value = null; ok = false; return true; }
            value = null;
            ok = false;
            return false;
        }
    }

    /// <summary>Non-blocking send for select: ready when the buffer has room (one slot for unbuffered).</summary>
    public bool TrySend(object? v)
    {
        lock (gate)
        {
            if (closed) throw new GoPanicException(GoString.FromDotNetString("send on closed channel"));
            int effective = cap == 0 ? 1 : cap;
            if (buf.Count < effective)
            {
                buf.Enqueue(v);
                System.Threading.Monitor.PulseAll(gate);
                return true;
            }
            return false;
        }
    }

    public long Length() { lock (gate) { return buf.Count; } }
    public long Capacity() => cap;
}

/// <summary>
/// GoSelect implements the <c>select</c> statement. It polls every case in
/// rotating order (to spread choices fairly without an RNG); the first ready case
/// wins. With a default clause, an all-blocked poll returns immediately; otherwise
/// it spins until a case becomes ready.
/// </summary>
public static class GoSelect
{
    /// <returns>object[]{ chosen index (long, -1 = default), recv value, recv ok }.</returns>
    public static object?[] Select(object?[] chans, object?[] ops, object?[] sendVals, bool hasDefault)
    {
        int n = chans.Length;
        int start = 0;
        var spin = new System.Threading.SpinWait();
        while (true)
        {
            for (int k = 0; k < n; k++)
            {
                int i = (start + k) % n;
                var ch = (GoChan?)chans[i];
                if (ch == null) continue; // nil channel: never ready
                int op = System.Convert.ToInt32(ops[i]);
                if (op == 0)
                {
                    if (ch.TryRecv(out var val, out var ok))
                        return new object?[] { (long)i, val, ok };
                }
                else
                {
                    if (ch.TrySend(sendVals[i]))
                        return new object?[] { (long)i, null, false };
                }
            }
            if (hasDefault) return new object?[] { (long)-1, null, false };
            start = n == 0 ? 0 : (start + 1) % n;
            spin.SpinOnce();
        }
    }
}

/// <summary>Channel operations the compiler calls into.</summary>
public static class GoChans
{
    public static GoChan Make(long cap) => new((int)cap);

    public static void Send(GoChan ch, object? v)
    {
        if (ch == null) BlockForever();
        ch!.Send(v);
    }

    public static object? Recv(GoChan ch)
    {
        if (ch == null) BlockForever();
        return ch!.Recv();
    }

    /// <summary>v, ok := &lt;-ch — returns a boxed tuple [value, ok].</summary>
    public static object?[] Recv2(GoChan ch)
    {
        if (ch == null) BlockForever();
        var (v, ok) = ch!.Recv2();
        return new object?[] { v, ok };
    }

    public static void Close(GoChan ch) => ch.Close();
    public static long Len(GoChan? ch) => ch?.Length() ?? 0;
    public static long Cap(GoChan? ch) => ch?.Capacity() ?? 0;

    private static void BlockForever() =>
        System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite); // Go: ops on a nil channel block forever
}
