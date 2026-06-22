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

**✅ DONE — type-id unification.** Struct ids (`structFor`) and typed-box named ids
(`namedIdentity`) now draw from one shared counter (`lowerCtx.typeIdSeq` /
`nextTypeId`), so every runtime-dispatched type has a globally-unique id and the bridge
keys one table without a struct-id ↔ named-id collision. `&CompositeLit` of a named
non-struct type tags its `GoPtr` with that type's id (`ptrNewId`), and
`collectBridgeMethods` registers adapters for named non-struct implementers (iterating
`c.namedIds`) as well as structs. Result: the idiomatic `type IntHeap []int` heap works
(fixture 395_heap_named_slice, byte-exact). `struct.Id` is a dispatch key only (the
metadata `TypeDefRow` is assigned separately in emit), so the change is invisible to the
emitter; conformance 194 + typed-box/stringer/interface-dispatch all stay green.
**By-value implementers — DONE** (fixture 401_bridge_byvalue_writer). An implementer
reached BY VALUE (a value-receiver struct passed by value, a named non-struct value)
now dispatches: (1) `Bridge.TypeIdOf` recovers a struct value's id from its CLR type via
a `Bridge.LinkClrId(clrName, id)` map the compiler emits for every struct with bridge
adapters (a `GoPtr`/`GoNamed` value still carries its id directly); (2) value-receiver
adapters use the `valBridge` receiver source, which calls `Bridge.RecvValue` to normalize
whatever form the value arrives in — a `GoPtr` (`&v` stored) is dereferenced, a `GoNamed`
is unwrapped, a bare struct value is used as-is — before unboxing to the receiver type.
So the same value-receiver type dispatches whether stored as a value or as `&v`.

## After heap: remove the real hacks (each its own slice)
- ✅ **DONE** — `Fmt.WriteTo`: `io.Writer` is now a `bridgeInterface`, so when `w` is a
  user wrapper (not a direct sink) `Bridge.HasMethod(w,"Write")` is true and
  `Bridge.CallMethod(w,"Write",goBytes)` drives the wrapper's own `Write` — echo.Response
  fires WriteHeader-on-first-write (a non-200 status now commits correctly), gin's
  responseWriter and any gzip/bufio writer run their real logic. The `Status`/`Code` field
  heuristic in `ResolveSink` is deleted; `ResolveSink` remains only as a fallback for a
  writer with no generated adapter (e.g. a named non-struct writer). Required fixing
  `lookupNamedType` to scan from the **root** package (`c.root`), not `c.pkg` (stale after
  the lowering loop) — otherwise `io.Writer` didn't resolve in a multi-package program and
  no adapters were generated. Verified: echo `/missing` → 404, gin `/nope` → 404.
- ✅ **DONE** — `io/fs.Stat`: `io/fs.FS` + `io/fs.File` + `io/fs.StatFS` are
  `bridgeInterfaces`. `fs.Stat` handles `os.DirFS` directly, takes the `StatFS` fast path
  (`fsys.Stat`) when present, else calls `fsys.Open(name)` + `file.Stat()` through the
  bridge. A struct/pointer `fs.FS` whose `Open` returns an `*os.File` stats via `File_Stat`
  → `GoFileInfo`; a **user `fs.FS` returning the program's own `fs.File`/`fs.FileInfo`
  types now works too** — the returned `fs.FileInfo` dispatches to its own methods (fixtures
  394_io_fs_stat, 403_fs_fileinfo_dispatch). The `net.Listener`-style anti-pattern is fixed:
  an interface-typed receiver (`fi.Name()` on `fs.FileInfo`) **with a user implementer** no
  longer short-circuits to the `GoFileInfo` cast — `shimMethodExtern` returns false so the
  call routes through `interfaceDispatch`, which enumerates both the user implementers and
  the shim handle. `GoFileInfo` stays reachable as the shim's implementer because it is
  tagged `[GoShim("io/fs.FileInfo")]` / `[GoShim("os.FileInfo")]` and matched by the precise
  `IsShimKindStrict` path. (When the only implementers are shim handles — `hash.Hash`,
  `context.Context`, … — the short-circuit is kept, so no regression / no extra dispatch.)
  Out of scope: `testing/fstest.MapFS` is unlowered stdlib, so its methods can't be bridged.
- `(*http.Server).Serve`: optional — drive `CallMethod(l, "Accept")` in a loop and speak
  HTTP/1.1 on the conn (removes the `Bound`-port-release bridge); larger, do last.
- `(*http.Server).Serve`: optional — drive `CallMethod(l, "Accept")` in a loop and speak
  HTTP/1.1 on the conn (removes the `Bound`-port-release bridge); larger, do last.

## Guardrails
- Extend the Stage-A shim-signature validator (`shim_signatures_test.go`) with a check
  that every `bridgeInterfaces` entry resolves and that `Heap.cs` etc. only reach user
  methods via `CallMethod` (no name-only dispatch).
- Conformance + the gin/echo demos stay green at every slice.
```
