namespace GoCLR.Stdlib;

using System.Collections.Generic;
using System.Runtime.InteropServices;
using GoCLR.Runtime;

/// <summary>An os.Signal (a syscall.Signal): a number and its Unix name.</summary>
public sealed class GoSignal { public int Num; public string Name = ""; }

/// <summary>Shim for os/signal: register channels to receive process signals. SIGINT,
/// SIGTERM, SIGHUP and SIGQUIT are delivered through .NET's PosixSignalRegistration;
/// others are accepted but only delivered if the platform raises them.</summary>
public static class Ossignal
{
    private static readonly object Lock = new();
    // channel -> the signal numbers it wants (empty set = every signal).
    private static readonly Dictionary<GoChan, HashSet<int>> Registry = new(ReferenceEqualityComparer.Instance);
    private static readonly Dictionary<int, PosixSignalRegistration> Hooks = new();

    private static readonly (int num, string name, PosixSignal sig)[] Known =
    {
        (1, "hangup", PosixSignal.SIGHUP),
        (2, "interrupt", PosixSignal.SIGINT),
        (3, "quit", PosixSignal.SIGQUIT),
        (15, "terminated", PosixSignal.SIGTERM),
    };

    private static readonly Dictionary<int, string> Names = new()
    {
        [1] = "hangup", [2] = "interrupt", [3] = "quit", [9] = "killed",
        [10] = "user defined signal 1", [12] = "user defined signal 2",
        [13] = "broken pipe", [15] = "terminated",
    };

    internal static GoSignal Sig(int num) => new GoSignal { Num = num, Name = NameOf(num) };
    private static string NameOf(int num) => Names.TryGetValue(num, out var n) ? n : "signal " + num;

    private static int NumOf(object? s) => s switch
    {
        GoSignal g => g.Num,
        GoNamed n when n.Value is long l => (int)l,
        long l => (int)l,
        _ => 0,
    };

    private static void EnsureHook(int num)
    {
        if (Hooks.ContainsKey(num)) return;
        foreach (var k in Known)
        {
            if (k.num != num) continue;
            try
            {
                Hooks[num] = PosixSignalRegistration.Create(k.sig, ctx =>
                {
                    ctx.Cancel = true; // a registered handler suppresses the default action
                    Deliver(k.num);
                });
            }
            catch { /* platform without this signal */ }
            return;
        }
    }

    private static void Deliver(int num)
    {
        List<GoChan> targets = new();
        lock (Lock)
            foreach (var (ch, set) in Registry)
                if (set.Count == 0 || set.Contains(num)) targets.Add(ch);
        var sig = Sig(num);
        foreach (var ch in targets) ch.TrySend(sig); // non-blocking, like Go's delivery
    }

    public static void Notify(GoChan c, GoSlice sigs)
    {
        lock (Lock)
        {
            if (!Registry.TryGetValue(c, out var set)) Registry[c] = set = new();
            if (sigs.Len == 0)
            {
                set.Clear(); // Notify(c) with no signals means all signals
                foreach (var k in Known) EnsureHook(k.num);
            }
            else
                for (int i = 0; i < sigs.Len; i++)
                {
                    int num = NumOf(sigs.Data![sigs.Off + i]);
                    set.Add(num);
                    EnsureHook(num);
                }
        }
    }

    public static void Stop(GoChan c)
    {
        lock (Lock) Registry.Remove(c);
    }

    public static void Reset(GoSlice sigs)
    {
        lock (Lock)
        {
            if (sigs.Len == 0) { Registry.Clear(); return; }
            for (int i = 0; i < sigs.Len; i++)
            {
                int num = NumOf(sigs.Data![sigs.Off + i]);
                foreach (var set in Registry.Values) set.Remove(num);
            }
        }
    }

    public static void Ignore(GoSlice sigs) { /* accepted; the runtime does not re-raise */ }

    // syscall.Signal methods.
    public static GoString Signal_String(object s) => GoString.FromDotNetString(((GoSignal)s).Name);
    public static void Signal_Signal(object s) { } // the os.Signal marker method
}
