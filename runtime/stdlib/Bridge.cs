namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>The interface method-callback bridge: lets a stdlib shim call a Go method on
/// an arbitrary interface value (container/heap calling Less/Swap/Push on the user's
/// heap.Interface). The compiler generates one receiver-first adapter closure per
/// (implementing type, method) and registers it here keyed by the value's runtime type
/// id; CallMethod resolves the value to that id and invokes precisely — no method-name
/// guessing. The actual invocation reuses the closure dispatcher in GoCLR.Runtime.
/// See docs/DESIGN-callback-bridge.md.</summary>
public static class Bridge
{
    private static readonly System.Collections.Generic.Dictionary<(long, string), GoClosure> Methods = new();

    public static void RegisterMethod(long typeId, GoString name, GoClosure fn)
    {
        lock (Methods) Methods[(typeId, name.ToDotNetString())] = fn;
    }

    private static long TypeIdOf(object? value) => value switch
    {
        GoPtr p => p.TypeId,
        GoNamed n => n.TypeId,
        _ => 0,
    };

    /// <summary>Call Go method <paramref name="name"/> on an interface value, passing it
    /// as the (receiver-first) argument list. Throws if no adapter is registered — a hard
    /// failure surfacing a missing bridge registration rather than a silent no-op.</summary>
    public static object? CallMethod(object? value, string name, params object?[] args)
    {
        long id = TypeIdOf(value);
        GoClosure? fn;
        lock (Methods)
        {
            if (!Methods.TryGetValue((id, name), out fn))
                throw new System.MissingMethodException(
                    $"goclr: no callback-bridge adapter for method '{name}' on type id {id}. " +
                    "The callback bridge (container/heap, …) currently supports struct receiver " +
                    "types only; a named non-struct receiver (e.g. `type H []int`) carries no type " +
                    "id on its pointer yet — see docs/DESIGN-callback-bridge.md.");
        }
        var all = new object?[args.Length + 1];
        all[0] = value;
        System.Array.Copy(args, 0, all, 1, args.Length);
        return GoRuntime.InvokeArgs(fn, all);
    }
}
