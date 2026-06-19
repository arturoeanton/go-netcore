using System.Threading.Channels;

namespace GoCLR.Runtime;

/// <summary>
/// GoChan models a Go channel over System.Threading.Channels. Buffered channels
/// use a bounded channel of the given capacity; unbuffered channels use a
/// rendezvous (capacity 0 is emulated with a bounded(1) + handshake is not
/// strictly synchronous in the MVP, which is acceptable per spec §17).
/// </summary>
public sealed class GoChan<T>
{
    private readonly Channel<T> _ch;
    private readonly object _gate = new();
    private bool _closed;

    public int Cap { get; }

    public GoChan(int capacity)
    {
        Cap = capacity;
        _ch = capacity > 0
            ? System.Threading.Channels.Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false,
            })
            : System.Threading.Channels.Channel.CreateBounded<T>(new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false,
            });
    }

    /// <summary>make(chan T, cap).</summary>
    public static GoChan<T> Make(int capacity = 0) => new(capacity);

    /// <summary>ch &lt;- v. Panics if the channel is closed, like Go.</summary>
    public void Send(T value)
    {
        lock (_gate)
        {
            if (_closed)
                throw new GoPanicException(GoString.FromDotNetString("send on closed channel"));
        }
        // BoundedChannelFullMode.Wait blocks the writer until space is available.
        _ch.Writer.WriteAsync(value).AsTask().GetAwaiter().GetResult();
    }

    /// <summary>v := &lt;-ch.</summary>
    public T Receive()
    {
        var (v, _) = Receive2();
        return v;
    }

    /// <summary>v, ok := &lt;-ch. ok is false once the channel is closed and drained.</summary>
    public (T value, bool ok) Receive2()
    {
        try
        {
            if (_ch.Reader.TryRead(out var fast)) return (fast, true);
            var t = _ch.Reader.ReadAsync().AsTask();
            return (t.GetAwaiter().GetResult(), true);
        }
        catch (ChannelClosedException)
        {
            return (default!, false);
        }
    }

    /// <summary>close(ch). Panics on double close, like Go.</summary>
    public void Close()
    {
        lock (_gate)
        {
            if (_closed)
                throw new GoPanicException(GoString.FromDotNetString("close of closed channel"));
            _closed = true;
        }
        _ch.Writer.TryComplete();
    }

    /// <summary>for v := range ch.</summary>
    public IEnumerable<T> Range()
    {
        while (true)
        {
            var (v, ok) = Receive2();
            if (!ok) yield break;
            yield return v;
        }
    }
}
