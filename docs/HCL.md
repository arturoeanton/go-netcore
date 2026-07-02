# Milestone — HashiCorp HCL runs on the CLR, and as a C# NuGet

*An honest account of getting [`hashicorp/hcl/v2`](https://github.com/hashicorp/hcl)
— the configuration language behind Terraform, Consul, Nomad and Packer — to
compile with goclr and run from C#.*

## What actually works

`hcl/v2` compiles to a .NET assembly with goclr and **parses HCL
byte-identically to `go run`**. Verified against the real library (not a
reimplementation) — the full stack: the `hclsyntax` scanner/parser, expression
evaluation, and the `cty` value model rendered to JSON via `cty/json`:

```hcl
io_mode = "async"
retries = 3
timeout = 1.5
service "http" "web_proxy" {
  listen_addr = "127.0.0.1:8080"
  enabled     = true
}
service "https" "web_proxy" {
  listen_addr = "127.0.0.1:8443"
}
```

both `go run` and the goclr-compiled assembly produce, byte-for-byte:

```json
{"io_mode":"async","retries":3,"service":[{"http":{"web_proxy":{"enabled":true,"listen_addr":"127.0.0.1:8080"}}},{"https":{"web_proxy":{"listen_addr":"127.0.0.1:8443"}}}],"timeout":1.5}
```

This exercises attribute evaluation, nested labelled blocks, repeated-block
lists, and every scalar kind (string, bool, integer, float) round-tripping
through `cty`'s big.Float-backed number model.

## Consumed from C# — the `Hcl.GoCLR` NuGet

[`examples/hcl_nuget`](../examples/hcl_nuget/) packages this for a .NET developer:

```csharp
using HclGoClr;

var cfg = Hcl.Parse("config.hcl", source);
string mode = (string)cfg["io_mode"];              // "async"
var services = (object[])cfg["service"];           // two blocks
```

The pieces:
- **`host/main.go`** — a Go host that imports `hcl/v2` and exposes `HclToJson`
  over a JSON bridge: it parses with `hclsyntax.ParseConfig`, walks the body
  (attributes + nested blocks with labels) generically, and renders the document
  to JSON with `cty/json` — the same shape as the `hcl2json` tool. Compiled by
  goclr to `host.dll`.
- **`Hcl.cs`** — a C# facade that marshals `GoString ↔ System.String`, runs the
  package init once (`__goclr_init`), and deserializes the JSON to natural .NET
  types (`Dictionary<string,object>` / `object[]` / `long` / `double` / `bool` /
  `string`).
- **`build.sh` + `HclGoClr.csproj`** — produce `Hcl.GoCLR.<version>.nupkg`
  bundling the facade, the compiled `host.dll` (~5 MB), and the goclr runtime.

Nothing from `hcl/v2` (no `hcl.Body`, no `cty.Value`) leaks into the C# API — the
boundary is string-only.

## How it was reached — the fixes that mattered

Each was reduced to a minimal repro and verified against `go run`. All are
**general** goclr improvements, not HCL-specific hacks; the full conformance
suite (572 fixtures) stays green with them.

**reflect write-back through a struct-field pointer**
- `reflect.ValueOf(&s.field).Elem().Set*(…)` was silently dropping the write.
  `reflect.Value.Elem` on a `GoPtr` read and wrote `p.Value` directly, bypassing
  the field-alias (`FGet`/`FSet`) and slice-element (`Arr`/`Idx`) accessors a
  `&struct.field` or `&slice[i]` pointer carries. It now routes through
  `GoPtrs.Get`/`GoPtrs.Set`, so a reflect-driven decode into a struct field
  actually lands. This is exactly what `gocty.FromCtyValue` (and therefore
  `hclsimple.Decode`) does for every field — before the fix, string attributes
  decoded empty and numbers decoded to zero.

**`math/big.Accuracy` width at the shim boundary**
- `big.Accuracy` is `int8` → goclr `Int32`, but the `(*Float).Int`, `.Int64`,
  `.Uint64`, `.Float32`, `.Rat` and `.Copy` shims returned the accuracy as a
  64-bit `long`. Comparing it against `big.Exact` (`accuracy != big.Exact`)
  unboxed an `Int64` where `Int32` was expected and threw `InvalidCastException`.
  Fixed to return the accuracy as `Int32`, with the correct Below/Exact/Above
  value (truncation direction), and `(*Float).Int` now returns `nil` with
  Below/Above for ±Inf — matching Go, which `cty/json.Marshal` relies on when it
  compares a number against the infinity sentinels.

## Honest limitations

- **Static-context evaluation.** Attributes are evaluated with a nil
  `EvalContext`, covering literals, collections and arithmetic — the common
  configuration case. Variable/function references (Terraform `var.*`,
  `${…}` with functions) need an `EvalContext`; wiring one through the host is a
  natural extension, not a compiler gap.
- **First call is slow.** The hcl/v2 + cty scanner/parser method set cold-JITs on
  the first parse; the demo config sets `TieredCompilation.QuickJitForLoops`.
  `goclr build -r2r` (ReadyToRun) removes the cold-JIT cost but wasn't applied to
  the packaged host yet.
- Pinned to `hcl/v2 v2.24.0` and `go-cty v1.16.3`. Two small overlays are used
  for the closure that `hclwrite` drags in (not on the decode path): a safe
  `go-cmp` `Pointer` (uintptr identity instead of `unsafe.Pointer`) and a stub
  `gohcl/encode.go` (drops the `hclwrite → go-cmp` import).
- The `.nupkg` is a **preview** — not published to nuget.org.

## Why this is more than a demo

The reflect-write-through-field-pointer fix is the general part: any Go library
that decodes into caller structs via `reflect` (the entire
`encoding/*`-into-struct, `mapstructure`, `gocty` family) depended on it. With it
landed and the `big.Accuracy` boundary corrected, `cty`'s number model — the core
of the whole HashiCorp configuration ecosystem — now runs unmodified on the CLR.
