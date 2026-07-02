# Cel.GoCLR — Google CEL from C#

*Use [Google CEL](https://github.com/google/cel-go) (the Common Expression
Language — the language behind Kubernetes admission policies, Envoy RBAC, and
IAM conditions) directly from C#, evaluated **in-process on the CLR**. The real
production `cel-go` library is compiled to a .NET assembly with **goclr** — no
sidecar, no gRPC, no P/Invoke.*

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

## What this is

CEL is a small, **non-Turing-complete**, safe expression language for
authorization rules, feature flags, filters and dynamic configuration. A
`cel.Program` is stateless, thread-safe and cacheable — compile once, evaluate
millions of times.

This package is a thin C# facade over the **actual Go `cel-go` implementation**,
compiled to IL by goclr. Everything crosses the C#↔Go boundary as JSON strings
(`GoString ↔ System.String`), so no cel-go type ever leaks into your C# code.

## API

```csharp
CelProgram Cel.Compile(string expression, params string[] variables);

object  program.Evaluate(object variables);      // natural .NET type
bool    program.EvaluateBool(object variables);  // for policy checks
string  program.EvaluateJson(object variables);  // raw result JSON
```

`variables` is any object serializable to a JSON object (anonymous types, POCOs,
dictionaries). Results deserialize to `bool` / `long` / `double` / `string` /
`object[]` / `Dictionary<string,object>`.

## Build & run

```bash
# from examples/cel_nuget (needs a built ../../bin/goclr and the .NET SDK)
./build.sh
```

`build.sh` compiles the Go host (`host/main.go`, which imports `cel-go`) to
`host.dll` with goclr, builds the C# facade + demo against it, and runs the demo.

To produce the NuGet: `dotnet pack -c Release CelGoClr.csproj` (after the host
DLLs exist under `bin/goclr/`).

## How it works

```
C#  Cel.Compile("...")           CelGoClr.Cel  (Cel.cs)
      │  JSON strings
      ▼
Program.CelCompile / CelEval     host/main.go  → compiled by goclr → host.dll
      │                          (cel.NewEnv · env.Compile · env.Program · Eval)
      ▼
GoCLR.Runtime + GoCLR.Stdlib     the goclr runtime the compiled Go runs on
```

The Go host keeps a registry of compiled `cel.Program`s keyed by an integer
handle; `CelCompile` returns the handle, `CelEval` runs it against a JSON
variable bag.

## Notes & limits

- **First call is slow.** The whole cel-go + ANTLR + protobuf method set
  cold-JITs on the first `Compile`. Subsequent calls are fast. `goclr build
  -r2r` (ReadyToRun) removes the cold-JIT cost for production.
- **protobuf is pinned to v1.34.2** (the last release with a `purego` build path
  goclr can lower; v1.36 dropped it).
- The facade binds all variables as CEL's dynamic type, so any JSON value works
  without declaring a schema. A typed-schema overload is a natural extension.

## Why goclr

goclr compiles pure Go to .NET assemblies. This package is a demonstration that
a real, reflection-heavy, ANTLR-and-protobuf-based Go library runs unmodified on
the CLR — packaged as an idiomatic NuGet a .NET developer consumes without
knowing Go is underneath.
