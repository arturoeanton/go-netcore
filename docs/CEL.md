# Milestone — Google CEL runs on the CLR, and as a C# NuGet

*An honest account of getting [`cel-go`](https://github.com/google/cel-go) — the
production Go implementation of Google's Common Expression Language — to compile
with goclr and run from C#. Tag `v0.2.0_cel`.*

## What actually works

`cel-go` compiles to a .NET assembly with goclr and **evaluates every CEL
expression byte-identically to `go run`**. Verified against the real library (not a
reimplementation):

| Expression | Result |
|---|---|
| `1 + 2 * 3` | `7` |
| `"hello".size()` | `5` |
| `age > 18 && name == "ana"` | `true` / `false` |
| `group in ["admin", "mod"]` | `true` / `false` |
| `age >= 21 ? "adult" : "minor"` | `adult` |
| `[1, 2, 3].map(x, x * 2)` | `[2, 4, 6]` |
| `[1, 2, 3, 4].filter(x, x % 2 == 0)` | `[2, 4]` |
| `{"k": "v"}.k == "v"` | `true` |
| `"foobar".startsWith("foo")` | `true` |
| `name.matches("^a.*")` | `true` |

This exercises `cel-go`'s full stack: the **ANTLR-generated parser**, the
**protobuf-based type system and value model**, and the **reflection-heavy
evaluator**. Getting there meant getting *protobuf's entire dynamic-reflection
machinery* and *ANTLR's ATN-driven parser* to run — not a subset.

## Consumed from C# — the `Cel.GoCLR` NuGet

[`examples/cel_nuget`](../examples/cel_nuget/) packages this for a .NET developer:

```csharp
using CelGoClr;

var authz = Cel.Compile(
    "user.role in ['admin', 'moderator'] || user.id == doc.owner",
    "user", "doc");

bool ok = authz.EvaluateBool(new {
    user = new { role = "guest", id = 7 },
    doc  = new { owner = 7 },
});   // => true (owner edits own doc)
```

The pieces:
- **`host/main.go`** — a Go host that imports `cel-go` and exposes
  `CelCompile`/`CelEval` over a JSON bridge (a registry of compiled `cel.Program`s
  keyed by an integer handle). Compiled by goclr to `host.dll`.
- **`Cel.cs`** — a C# facade that marshals `GoString ↔ System.String`, runs the
  package init once (`__goclr_init`), and deserializes results to natural .NET
  types.
- **`build.sh` + `CelGoClr.csproj`** — produce `Cel.GoCLR.<version>.nupkg` bundling
  the facade, the compiled `host.dll` (~26 MB), and the goclr runtime.

Nothing from `cel-go` (no `ref.Val`, no `cty`) leaks into the C# API — the boundary
is string-only.

## How it was reached — the fixes that mattered

Each was reduced to a minimal repro and verified against `go run`. All are
**general** goclr improvements, not CEL-specific hacks; they harden the compiler
and runtime for any reflection-heavy Go library.

**protobuf — dynamic message reflection**
- `reflect.New(T)` stamps the allocated pointer with the pointee's dispatch id and
  its `*T` display name, and allocates a **Go-zeroed struct instance** (not `nil`),
  so a reflect-allocated value satisfies interface assertions/switches and its
  fields are addressable. Without this, protobuf's decoder produced pointers that
  failed `m.(proto.Message)` and nil-deref'd in `MessageInfo.init`.
- `reflect.Zero(PtrTo(t)).Interface()` and `Value.Interface()` of a nil pointer
  round-trip the pointer *type* (a typed-nil carrier), so `reflect.TypeOf` is exact
  for dynamically-built pointer types. Descriptor links carry the goir struct id
  separately from the reflect descriptor id (they are distinct id spaces).

**ANTLR — the parser table graph**
- A **pointer-receiver method promoted from a value embed, dispatched through an
  interface, was mutating a copy** — so `setEndState` was lost, the ATN state graph
  came out inconsistent, and deserialization panicked (`IllegalState`). The fix
  builds a field-alias into the live pointee. A *pointer* embed (`CELParser` embeds
  `*BaseParser`) is read directly as the receiver instead.

**CEL's evaluator**
- Interface satisfaction through a **pointer embed**: a `*T` whose methods are
  promoted from an embedded `*Base` now matches `x.(Iface)` in type switches and
  asserts, and a value implementer's pointer form matches in `emitInterfaceAssert`
  (`mutableList` satisfying `ref.Val` for `NativeToValue`).

**Library mode**
- A C# consumer calls `Program.CelCompile` directly, bypassing goclr's entry
  wrapper, so package initialization (globals, `sync.Once`, registries) never ran.
  The facade now calls a public `__goclr_init` once, and the goroutine/closure
  invoker is registered at the **start of init** (init bodies use closures — a
  `sync.Pool`'s `New` — before `main`).

**Scaling — the CLR method limit**
- The field-alias getter/setter closures are deduped by `(struct, field-path,
  role)`. A reflection-heavy program (goja: thousands of promoted-pointer dispatch
  sites) otherwise generated a fresh method pair per site and blew the CLR's
  **65535-methods-per-type** limit (goja went 65766 → 6724 methods). The no-match
  dispatch bridge fallback is gated to smaller programs that actually need it.

Plus a batch of smaller lowering fixes surfaced along the way: promoted field
writes and parallel/deref assignments through pointer embeds, `&pkg.Var`, `var x,
ok = y.(T)`, type-params instantiated to an interface, index/len/cap widened to
int64, `object[]` tuple casts, and evaluating the RHS **before** snapshotting a
cell-held struct (`fd.X = fd.alloc(n)`).

## Honest limitations

- **protobuf is pinned to v1.34.2** — the last release with a `purego` build path
  goclr can lower. v1.36 dropped it. A host module must pin it.
- **First call is slow.** The whole `cel-go` + ANTLR + protobuf method set
  cold-JITs on the first `Compile`; the demo config sets
  `TieredCompilation.QuickJitForLoops` so this is ~10 s rather than minutes.
  `goclr build -r2r` (ReadyToRun) removes the cold-JIT cost for production but
  wasn't applied to the packaged host yet.
- **Interface values / `gob.Register` in protobuf** and the custom
  `GobEncoder`/marshaler hooks remain the documented gaps (not on the CEL path).
- The facade binds all CEL variables as the **dynamic type**, so any JSON value
  works without a declared schema; a typed-schema overload is a natural extension.
- The `.nupkg` is a **preview** — not published to nuget.org, and not yet
  multi-RID (it ships IL, so it is AnyCPU, but R2R images would be per-RID).

## Why this is more than a demo

The reflection and interface-dispatch fixes above are the hard, general part of
running *any* real Go library on the CLR. They were the wall for a
reflection-and-codegen-heavy dependency like protobuf; with them landed, the next
reflection-heavy target (HCL's `cty`, an OPA/Rego evaluator) starts on ground that
is already won.
