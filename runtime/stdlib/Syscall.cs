namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>An opaque syscall.Flock_t (its fields are not observed by the no-op lock).</summary>
public sealed class GoFlockT { }

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
