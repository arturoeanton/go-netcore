# Milestone — Open Policy Agent (Rego) runs on the CLR, and as a C# NuGet

*An honest account of getting [`open-policy-agent/opa`](https://github.com/open-policy-agent/opa)
— the CNCF policy engine behind Kubernetes admission control, Envoy authorization
and Terraform/CI guardrails — to compile with goclr and evaluate Rego policies from
C#. Tag `v0.6.0_opa`.*

## What actually works

OPA compiles to a .NET assembly with goclr and **evaluates Rego policies
byte-identically to `go run`**. Verified against the real library (not a
reimplementation) — the full stack: the Rego scanner and parser, the compiler, and
the topdown evaluator, plus OPA's dependency tree (jwx, logrus, go-metrics,
fastjson, xxhash, glob, yaml, …).

Given this authorization module:

```rego
package example

default allow = false
allow { input.role == "admin" }
allow { input.role == "user"; input.action == "read" }
```

both `go run` and the goclr-compiled assembly produce, byte-for-byte, OPA's own
result-set JSON:

```
input={"role":"admin","action":"delete"} => [{"expressions":[{"value":true,"text":"data.example.allow","location":{"row":1,"col":1}}]}]
input={"role":"user","action":"read"}    => [{"expressions":[{"value":true,"text":"data.example.allow","location":{"row":1,"col":1}}]}]
input={"role":"user","action":"write"}   => [{"expressions":[{"value":false,"text":"data.example.allow","location":{"row":1,"col":1}}]}]
```

This exercises `rego.New(...).PrepareForEval` (parse + compile) and `Eval` (topdown)
end to end. A second policy in the demo computes a *value* rather than a boolean
(a billing tier from a seat count), confirming the evaluator handles ordinary rule
bodies, not just a single `allow`.

## Consumed from C# — the `Opa.GoCLR` NuGet

[`examples/opa_nuget`](../examples/opa_nuget/) packages this for a .NET developer:

```csharp
using OpaGoClr;

var opa = new OpaPolicy(module);
bool allow = opa.EvalBool("data.example.allow",
                          new { role = "admin", action = "delete" });   // true

using var tier = opa.EvalValue("data.billing.tier", new { seats = 42 });
string t = tier.Value.GetString();                                      // "team"
```

The pieces:
- **`host/main.go`** — a Go host that imports `opa/rego` and exposes
  `OpaEval(module, query, inputJSON)` over a JSON bridge: it builds a
  `rego.New(Query, Module)`, runs `PrepareForEval`, evaluates with the input
  document, and marshals the `rego.ResultSet` to JSON. Compiled by goclr to
  `host.dll` (~15 MB).
- **`Opa.cs`** — a C# facade (`OpaPolicy` with `Eval` / `EvalBool` / `EvalValue`)
  that marshals `GoString ↔ System.String`, runs the package init once
  (`__goclr_init`), and parses the result JSON.
- **`build.sh` + `OpaGoClr.csproj`** — produce `Opa.GoCLR.<version>.nupkg`
  bundling the facade, the compiled `host.dll`, and the goclr runtime.

Nothing from OPA (no `ast.Term`, no `rego.ResultSet`) leaks into the C# API — the
boundary is string-only.

## How it was reached — the fixes that mattered

Each was reduced to a minimal repro and verified against `go run`. All are
**general** goclr improvements, not OPA-specific hacks; the full conformance suite
stays green with them, and each is locked by a new conformance fixture.

**Named-scalar identity in map/slice key comparison** (fixture 782)
- OPA's parser detects operators with `slices.Contains([]tokens.Token, tok)`.
  `tokens.Token` is a named integer type; the compiled slice held boxed `GoNamed`
  values while the needle was a raw underlying `int` (or vice-versa), so the key
  comparer never matched and the parser reported "unexpected equal token" on every
  `==`. `GoKeyComparer.Equals` now unwraps a `GoNamed` against a raw underlying
  value on either side. Any `map[NamedInt]…` or `slices.Contains` over a named
  scalar depended on this.

**Appending a named value into an interface-element slice** (fixture 783)
- `append(stmts, body)` where `stmts` is `[]interface{}` and `body` is an
  `ast.Body` (a named slice type) lost the named identity — a later
  `case Body:` type switch failed with "expected body but got []interface {}".
  The single-element append path boxed the element without tagging its named type;
  it now uses the same named-aware boxing (`emitBoxedElemInto`) the multi-element
  path already used.

**Go 1.20 slice-to-array-pointer conversion** (fixture 784)
- OPA's `ast.InterfaceToValue` uses `*(*[2]*Term)(kvs[i:i+2])` to read key/value
  pairs. goclr had no lowering for `(*[N]T)(slice)`; it now emits a runtime helper
  that returns a pointer to an array view sharing the slice's backing store (and
  panics if the slice is shorter than `N`, like Go).

**Type identity for unchanged composites under generic substitution** (fixture 785)
- `util.NewPtrSlice[ast.Term]` does `make([]T, n)` and returns `[]*T` into the
  backing. The generic monomorphizer rebuilt every composite type
  (slice/map/pointer/chan) unconditionally, allocating a fresh `*Location` for
  `Term`'s unchanged pointer field. Callers compare types by identity, so the
  spurious new instance flagged `Term` as "substituted" and cloned it into a
  structural type — a later cast to the named `Term` then failed. `substType` now
  returns the original type when nothing changed.

**Writing an opaque-shim field inside a generic method** (fixture 786)
- httprc's `ResourceBase[T].Sync` does `res.Body = …` on a `*http.Response`. A
  pointer to an opaque value-type shim *is* that shim's handle (`*bytes.Buffer`
  and `bytes.Buffer` share one runtime object), but the generic type-resolution
  path wrapped it as pointer-to-object, so the field write failed to lower.
  `funcLowerer.goType` now resolves `*shim` to the shim handle (`KObject`) the same
  way the non-generic path does.

Earlier phases also landed value-receiver dispatch through a pointer implementer
(fixture 781, `loader.fileLoader`) and generic-instantiation coverage in the
reflect-method registry and dispatch fallback (fixtures 779/780, which unblocked
jwa's `init`).

## Honest limitations

- **The policy is compiled on each `Eval`.** The host rebuilds the prepared query
  per call for a simple, stateless bridge. Caching a `PreparedEvalQuery` across
  calls (keyed by module) is a natural host extension, not a compiler gap.
- **First call is slow.** OPA's parser/compiler/topdown method set cold-JITs on
  the first evaluation; the demo sets `TieredCompilation.QuickJitForLoops`.
  `goclr build -r2r` (ReadyToRun) removes the cold-JIT cost but wasn't applied to
  the packaged host yet.
- **A handful of overlays** (under `host/goclr.overlays`) lower some dependencies
  through small stubs — logrus terminal detection, xxhash's `unsafe` fast path,
  OPA's embedded capabilities/schemas JSON, go-metrics platform sinks, fastjson's
  `unsafe` helpers, pbkdf2, and a jwx JWK parser stub — none on the Rego evaluation
  path.
- Pinned to `open-policy-agent/opa v1.18.2`.
- The `.nupkg` is a **preview** — not published to nuget.org.

## Why this is more than a demo

OPA is a full programming-language front-end (scanner, parser, type-aware compiler)
plus a datalog-style evaluator — an order of magnitude larger than a config parser.
The fixes it forced are the general part: named-scalar identity through `map`/slice
keys and `append`, slice-to-array-pointer conversion, and correct type identity
under generic monomorphization are foundational Go semantics that any large library
leans on. With them landed, the third reflection-heavy Go engine (after `cel-go`
and `hashicorp/hcl/v2`) runs unmodified on the CLR and produces byte-exact results.
```
