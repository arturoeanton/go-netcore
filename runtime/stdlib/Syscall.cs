namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>An opaque syscall.Flock_t (its fields are not observed by the no-op lock).</summary>
public sealed class GoFlockT { }

/// <summary>Opaque syscall.SockaddrInet4/6 (used only by the raw-socket listener path,
/// which is dead code under goclr — serving goes through System.Net.HttpListener).</summary>
public sealed class GoSockaddr { }

/// <summary>Shim for the small slice of <c>syscall</c> used for advisory file locking.
/// goclr runs as a single .NET process and serializes access through the engine's own
/// in-process locks, so fcntl-based inter-process file locks are no-ops that succeed.</summary>
public static class Syscall
{
    public static object NewFlockT() => new GoFlockT();

    // syscall.FcntlFlock(fd, cmd, *Flock_t) error: advisory locking is a no-op here.
    public static object? FcntlFlock(ulong fd, long cmd, object? lk) => null;
    // syscall.Fsync(fd) error: durability is a no-op — within a single .NET process the
    // backing FileStream already observes its own writes (read-after-write is coherent).
    public static object? Fsync(long fd) => null;

    // Raw-socket listener surface (valyala/tcplisten, fasthttp prefork). Dead code under
    // goclr — serving is over System.Net.HttpListener — so socket creation reports failure
    // and the option/teardown calls are no-ops. ForkLock is a real RWMutex for correctness.
    public static object ForkLock() => Sync.NewRWMutex();
    private static readonly GoError ENotSupported = new(GoString.FromDotNetString("operation not supported"));
    public static object?[] Socket(long domain, long typ, long proto) => new object?[] { (long)-1, ENotSupported };
    public static object? SetsockoptInt(long fd, long level, long opt, long value) => null;
    public static object? SetNonblock(long fd, bool nonblocking) => null;
    public static void CloseOnExec(long fd) { }
    public static object? Close(long fd) => null;
    public static object? Bind(long fd, object? sa) => null;
    public static object? Listen(long fd, long backlog) => null;
    public static object NewSockaddrInet4() => new GoSockaddr();
    public static object NewSockaddrInet6() => new GoSockaddr();
    public static void Sockaddr_SetPort(object sa, long port) { }
    public static void Sockaddr_SetZoneId(object sa, uint z) { }
    // Addr field ([4]byte / [16]byte): return a fresh 16-byte slice — `addr[:]` then copy
    // into it is harmless (the value is never read back on goclr's serving path).
    public static GoSlice Sockaddr_Addr(object sa)
    {
        var d = new object?[16];
        for (int i = 0; i < 16; i++) d[i] = 0;
        return new GoSlice { Data = d, Off = 0, Len = 16, Cap = 16 };
    }

    // syscall.Signal constants (as os.Signal values).
    public static object SIGHUP() => Ossignal.Sig(1);
    public static object SIGINT() => Ossignal.Sig(2);
    public static object SIGQUIT() => Ossignal.Sig(3);
    public static object SIGKILL() => Ossignal.Sig(9);
    public static object SIGUSR1() => Ossignal.Sig(10);
    public static object SIGUSR2() => Ossignal.Sig(12);
    public static object SIGPIPE() => Ossignal.Sig(13);
    public static object SIGTERM() => Ossignal.Sig(15);
}
