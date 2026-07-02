# Hcl.GoCLR — HashiCorp HCL from C#

*Parse [HashiCorp HCL](https://github.com/hashicorp/hcl) (the configuration
language behind Terraform, Consul, Nomad and Packer) directly from C#, evaluated
**in-process on the CLR**. The real production `hashicorp/hcl/v2` library is
compiled to a .NET assembly with **goclr** — no sidecar, no CGo, no P/Invoke.*

```csharp
using HclGoClr;

var cfg = Hcl.Parse("config.hcl", @"
io_mode = ""async""
service ""http"" ""web_proxy"" {
  listen_addr = ""127.0.0.1:8080""
}
");

string mode = (string)cfg["io_mode"];   // "async"
```

## What this is

HCL is HashiCorp's declarative configuration language: attributes, nested
labelled blocks, expressions and functions. This package is a thin C# facade
over the **actual Go `hashicorp/hcl/v2` implementation**, compiled to IL by
goclr. The parsed document crosses the C#↔Go boundary as a JSON string
(`GoString ↔ System.String`), so no hcl/cty type ever leaks into your C# code.

The JSON shape matches the well-known
[`hcl2json`](https://github.com/tmccombs/hcl2json) tool: attributes become keys,
and blocks nest by their type and labels (repeated blocks of one type become a
list).

## API

```csharp
string                      Hcl.ToJson(string filename, string source);  // raw JSON
Dictionary<string,object>   Hcl.Parse (string filename, string source);  // object graph
```

`Parse` returns a `Dictionary<string,object>` whose leaves are `bool` / `long` /
`double` / `string`, with `object[]` for lists and nested dictionaries for
blocks. `filename` is used only in error messages.

## Build & run

```bash
# from examples/hcl_nuget (needs a built ../../bin/goclr and the .NET SDK)
./build.sh
```

`build.sh` compiles the Go host (`host/main.go`, which imports `hcl/v2`) to
`host.dll` with goclr, builds the C# facade + demo against it, and runs the demo.

To produce the NuGet: `dotnet pack -c Release HclGoClr.csproj` (after the host
DLLs exist under `bin/goclr/`).

## How it works

```
C#  Hcl.Parse("...")             HclGoClr.Hcl  (Hcl.cs)
      │  JSON strings
      ▼
Program.HclToJson                host/main.go  → compiled by goclr → host.dll
      │                          (hclsyntax.ParseConfig · walk body · ctyjson.Marshal)
      ▼
GoCLR.Runtime + GoCLR.Stdlib     the goclr runtime the compiled Go runs on
```

The Go host parses the source with `hclsyntax`, walks the body (attributes +
nested blocks) evaluating each attribute to a `cty.Value`, and renders the whole
document to JSON with `cty/json`.

## Notes & limits

- **First call is slow.** The hcl/v2 + cty scanner/parser method set cold-JITs on
  the first parse; subsequent calls are fast. `goclr build -r2r` (ReadyToRun)
  removes the cold-JIT cost for production.
- **Static-context evaluation.** Attributes are evaluated with a nil
  `EvalContext`, so this covers literals, collections and arithmetic — the common
  configuration case. Variable/function references (e.g. Terraform `var.*`) would
  need an evaluation context, a natural extension of the host.
- Pinned to `hcl/v2 v2.24.0` and `go-cty v1.16.3`.

## Why goclr

goclr compiles pure Go to .NET assemblies. This package is a demonstration that
a real, reflection-and-`cty`-heavy Go library runs unmodified on the CLR —
packaged as an idiomatic NuGet a .NET developer consumes without knowing Go is
underneath.
