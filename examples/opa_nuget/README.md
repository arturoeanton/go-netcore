# Opa.GoCLR — Open Policy Agent (Rego) from C#

*Evaluate [Open Policy Agent](https://www.openpolicyagent.org/) **Rego** policies
directly from C#, **in-process on the CLR**. The real production
`open-policy-agent/opa` engine — parser, compiler and topdown evaluator — is
compiled to a .NET assembly with **goclr**. No sidecar, no CGo, no P/Invoke,
no `opa` binary to ship.*

```csharp
using OpaGoClr;

var opa = new OpaPolicy(@"
package example

default allow = false
allow { input.role == ""admin"" }
allow { input.role == ""user""; input.action == ""read"" }
");

bool ok = opa.EvalBool("data.example.allow",
                       new { role = "admin", action = "delete" });   // true
```

## What this is

Rego is OPA's declarative policy language for authorization and configuration
rules. This package is a thin C# facade over the **actual Go
`open-policy-agent/opa` implementation**, compiled to IL by goclr. Policy, query
and input all cross the C#↔Go boundary as strings (`GoString ↔ System.String`),
and results come back as JSON — so no `ast`/`rego` type ever leaks into your C#
code.

The result JSON is exactly the shape OPA itself produces:

```json
[{"expressions":[{"value":true,"text":"data.example.allow","location":{"row":1,"col":1}}]}]
```

## API

```csharp
var opa = new OpaPolicy(string module);         // a Rego module (full `package …` text)

string       opa.Eval(string query, string inputJson);  // raw OPA result-set JSON
string       opa.Eval(string query, object input);      // input serialized to JSON
bool         opa.EvalBool(string query, object input);  // first value as a bool
ResultValue  opa.EvalValue(string query, object input); // first value as a JsonElement
```

One `OpaPolicy` is reusable across many inputs. `EvalBool` is the convenience for
boolean authorization rules (`data.pkg.allow`); `EvalValue` returns the raw
first-expression value (a disposable `JsonElement` holder) for policies that
compute strings, numbers or objects. A parse/compile/eval failure throws
`OpaException`.

## Build & run

```bash
# from examples/opa_nuget (needs a built ../../bin/goclr and the .NET SDK)
./build.sh
```

`build.sh` compiles the Go host (`host/main.go`, which imports
`open-policy-agent/opa/rego`) to `host.dll` with goclr, builds the C# facade +
demo against it, and runs the demo. Expected output:

```
== Rego authorization policy (data.example.allow) ==
  role=admin  action=delete  => allow=True
  role=user   action=read    => allow=True
  role=user   action=write   => allow=False
...
  seats=3    => tier=solo
  seats=42   => tier=team
  seats=500  => tier=enterprise
```

To produce the NuGet: `dotnet pack -c Release OpaGoClr.csproj` (after the host
DLLs exist under `bin/goclr/`).

## How it works

```
C#  opa.EvalBool("data.example.allow", input)   OpaGoClr.OpaPolicy  (Opa.cs)
      │  JSON strings (module · query · input)
      ▼
Program.OpaEval                  host/main.go  → compiled by goclr → host.dll
      │                          (rego.New · PrepareForEval · Eval · json.Marshal)
      ▼
GoCLR.Runtime + GoCLR.Stdlib     the goclr runtime the compiled Go runs on
```

The Go host builds a `rego.New(Query, Module)`, calls `PrepareForEval` (which runs
OPA's parser and compiler), evaluates it with the input document, and renders the
`rego.ResultSet` to JSON. Every stage — the Rego scanner, the AST, the compiler and
the topdown evaluator — is compiled Go running on the CLR.

## Notes & limits

- **First call is slow.** OPA's parser/compiler/topdown method set cold-JITs on
  the first evaluation; subsequent calls are fast. `goclr build -r2r` (ReadyToRun)
  removes the cold-JIT cost for production.
- **The policy is compiled on each `Eval`.** The host rebuilds the prepared query
  per call for a simple, stateless bridge. Caching a `PreparedEvalQuery` across
  calls (keyed by module) is a natural host extension.
- **Overlays.** A handful of dependencies are lowered through small goclr overlays
  (under `host/goclr.overlays`) — logrus terminal detection, xxhash's `unsafe`
  fast path, OPA's embedded capabilities/schemas JSON, go-metrics platform sinks,
  fastjson's `unsafe` helpers, pbkdf2, and a jwx JWK parser stub — none of which
  affect policy evaluation.
- Pinned to `open-policy-agent/opa v1.18.2`.

## Why goclr

goclr compiles pure Go to .NET assemblies. OPA is a large, reflection-heavy engine
(a full language front-end plus an evaluator); this package is a demonstration that
it runs **unmodified** on the CLR and produces **byte-exact** results versus
`go run` — packaged as an idiomatic NuGet a .NET developer consumes without knowing
Go is underneath.
