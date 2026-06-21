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

    // CLR struct type name -> its runtime dispatch id, so an implementer reached BY VALUE
    // (a value-receiver struct boxed as its CLR struct, carrying no GoPtr/GoNamed tag) can
    // still be resolved to its adapter id. A GoPtr/GoNamed value carries its id directly.
    private static readonly System.Collections.Generic.Dictionary<string, long> ClrToId = new();

    public static void RegisterMethod(long typeId, GoString name, GoClosure fn)
    {
        lock (Methods) Methods[(typeId, name.ToDotNetString())] = fn;
    }

    /// <summary>Link a CLR struct type name to its dispatch id (emitted once per struct that
    /// has bridge adapters), so a by-value struct receiver resolves to the same id its
    /// pointer would.</summary>
    public static void LinkClrId(GoString clrName, long id)
    {
        lock (ClrToId) ClrToId[clrName.ToDotNetString()] = id;
    }

    /// <summary>Whether a bridge adapter is registered for value.method — lets a shim try
    /// the callback path and fall back when the type has no generated adapter.</summary>
    public static bool HasMethod(object? value, string name)
    {
        lock (Methods) return Methods.ContainsKey((TypeIdOf(value), name));
    }

    private static long TypeIdOf(object? value)
    {
        switch (value)
        {
            case GoPtr p: return p.TypeId;
            case GoNamed n: return n.TypeId;
            case null: return 0;
            default:
                lock (ClrToId) return ClrToId.TryGetValue(value.GetType().Name, out var id) ? id : 0;
        }
    }

    /// <summary>Normalize an interface value to the receiver payload a value-receiver method
    /// body expects: a GoPtr is dereferenced (`&v` was stored), a GoNamed is unwrapped (a
    /// named non-struct value), and a bare struct value is used as-is.</summary>
    public static object? RecvValue(object? value) => value switch
    {
        GoPtr p => GoPtrs.Get(p),
        GoNamed n => n.Value,
        _ => value,
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
                    $"goclr: no callback-bridge adapter for method '{name}' on type id {id} " +
                    $"(value of CLR type '{value?.GetType().Name ?? "null"}'). The callback bridge " +
                    "(container/heap, io.Writer, io/fs, …) generates an adapter per implementing " +
                    "type+method; a missing one means this type was not enumerated as an implementer " +
                    "— see docs/DESIGN-callback-bridge.md.");
        }
        var all = new object?[args.Length + 1];
        all[0] = value;
        System.Array.Copy(args, 0, all, 1, args.Length);
        return GoRuntime.InvokeArgs(fn, all);
    }
}
