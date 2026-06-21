# DESIGN — interface method-callback bridge (Stage B of the hardening track)

## Problem

Several shims need to call **back** into Go code through an interface, but goclr has
no general mechanism for it: the runtime can invoke a *closure* (`GoRuntime.InvokeArgs`)
but not "call method M on this interface value". Today that gap forces hacks:

- `Fmt.WriteTo`/`ResolveSink` writes straight to the underlying `GoRespWriter`,
  bypassing a wrapper's `Write` (so `echo.Response`'s WriteHeader-on-first-write never
  runs) — patched with a `Status`/`Code` field-name heuristic.
- `io/fs.Stat`/`Sub` are honest stubs because `fs.Stat` must call `fsys.Open(name)`.
- `container/heap` is unimplemented (it calls back into `Len/Less/Swap/Push/Pop`).
- `(*http.Server).Serve` can't drive the caller's `net.Listener.Accept` loop, so it
  bridges via a `Bound`-port release instead.

`collectHandlers` already solves *one* instance: it generates a per-type adapter
closure for `http.Handler.ServeHTTP` and registers it by type id (`Http.RegisterHandler`
/ `HandlerFor`). Stage B generalizes that into a reusable bridge.

## Mechanism

### Runtime (`GoCLR.Runtime`, so every shim can use it)
```
static Dictionary<(long typeId, string method), GoClosure> _methods;
public static void RegisterMethod(long typeId, string name, GoClosure fn);
public static object? CallMethod(object? value, string name, params object?[] args) {
    long id = TypeIdOf(value);                 // GoPtr.TypeId / GoNamed.TypeId
    var fn  = _methods[(id, name)];            // (precise; no name-only guessing)
    var all = new object?[args.Length + 1];    // receiver-first, like the ServeHTTP adapter
    all[0] = value; Array.Copy(args, 0, all, 1, args.Length);
    return GoRuntime.InvokeArgs(fn, all);
}
```

### Compiler
1. Factor `buildHandlerClosure` into a **general** `buildMethodAdapter(m *goir.Method,
   recvSrc) int` that works for ANY signature: unpack `args[0]`=receiver (per `recvSrc`),
   `args[1..n]`=params (unbox to `m.Params[i]`), `OpCallMethod`, then box-and-return the
   result (or `OpLdNull` for void). `collectHandlers` becomes a caller of it.
2. `collectBridgeMethods()`: for each curated bridge interface present in the import
   closure (`bridgeInterfaces = ["container/heap.Interface", ...]`, resolved via the
   existing `shimNamedType` BFS), find every concrete implementer (`types.Implements`,
   same as `shimIfaceImplementers`), and for each method in the interface's method set
   generate an adapter and emit a startup `GoRuntime.RegisterMethod(typeId, name,
   closure)`.

### Stdlib consumer (first demonstrator: `container/heap`)
`Heap.cs` ports Go's `container/heap` verbatim, reaching the user type via the bridge:
`Len/Less/Swap/Push/Pop` → `GoRuntime.CallMethod(h, "Len"|"Less"|…, args)`. Register
`Init/Push/Pop/Remove/Fix` in `shimRegistry["container/heap"]`. Fixture + conformance.

## THE WRINKLE — type-id of the receiver value

`CallMethod` resolves `value → typeId`, which must equal the id the adapters were
registered under. For `*StructT` (a `GoPtr`), `TypeId == structReg[StructT].Id` — clean,
exactly what `collectHandlers` relies on. But the idiomatic `heap.Interface` implementer
is a **named slice**: `type IntHeap []int`, used as `*IntHeap`. A `*namedSlice` GoPtr's
`TypeId` comes from the `namedIdentity` (typed-box) path, NOT a struct id — so
`collectBridgeMethods` must register adapters under the SAME id the `&namedSliceVar`
lowering stamps onto the GoPtr. Verify both:

- struct receiver (`type PQ struct{ a []int }`, methods on `*PQ`) — clean id path,
- named-slice receiver (`type IntHeap []int`, methods on `*IntHeap`) — typed-box id path.

Plan: land the **struct-receiver** heap fixture first (proves the bridge end-to-end on
the clean id path), then extend to the named-slice id and add that fixture. If the two
id sources don't already coincide, unify them in `namedIdentity`/`structReg` so a value
has ONE id regardless of how it's reached.

## After heap: remove the real hacks (each its own slice)
- `Fmt.WriteTo`: when `w` is a user wrapper (not a direct sink), `CallMethod(w, "Write",
  goBytes)` so the wrapper's real `Write` runs → delete the `Status` field heuristic in
  `ResolveSink`. (Hot path — keep the heuristic as a fallback until the bridge is proven,
  then remove.)
- `io/fs.Stat`: `CallMethod(fsys, "Open", name)` → `CallMethod(file, "Stat")` → real
  FileInfo; drop the stub.
- `(*http.Server).Serve`: optional — drive `CallMethod(l, "Accept")` in a loop and speak
  HTTP/1.1 on the conn (removes the `Bound`-port-release bridge); larger, do last.

## Guardrails
- Extend the Stage-A shim-signature validator (`shim_signatures_test.go`) with a check
  that every `bridgeInterfaces` entry resolves and that `Heap.cs` etc. only reach user
  methods via `CallMethod` (no name-only dispatch).
- Conformance + the gin/echo demos stay green at every slice.
```
