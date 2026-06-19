# goclr

> goclr compiles pure-Go applications to .NET assemblies. The MVP is focused on
> HTTP services built with [Echo](https://github.com/labstack/echo) and embedded
> JavaScript execution via [goja](https://github.com/dop251/goja).

`goclr` is an **experimental** compiler that takes pure-Go projects and compiles
them to executable .NET assemblies. It is **not** a general Go compiler and is
**not** 100% compatible with the official toolchain. It does **not** support cgo.

The target program the MVP is built around is an Echo service whose `/eval`
endpoint runs JavaScript through goja — see [`cmd/server`](cmd/server/main.go).

## Status

This repository currently implements the **front half** of the pipeline plus the
.NET runtime core. See [ROADMAP.md](ROADMAP.md) for the milestone breakdown and
exactly what works today vs. what is under construction.

| Area | State |
| --- | --- |
| CLI (`build`/`run`/`analyze`/`test`/`doctor`/`clean`) | ✅ wired |
| `doctor`, `clean` | ✅ functional |
| `analyze` (cgo/asm/unsafe + echo-goja profile, human + JSON) | ✅ functional |
| Frontend loader (`go/packages`, type info, build tags) | ✅ functional |
| Diagnostics (`GCLRxxxx`, actionable, located) | ✅ functional |
| .NET runtime core types (GoString, slices, maps, interfaces, defer/panic, goroutines, channels) | ✅ compiles (`net8.0`) |
| Lower → emit (ECMA-335 managed PE) | 🟡 **M1 closed + M2 nearly done**: control flow, funcs/methods, strings, structs, slices, maps, pointers, multi-return, interfaces, defer/panic/recover, closures — all run on `dotnet` |
| Conformance runner (`goclr run` vs `go run`) | ✅ functional |
| stdlib overlay (net/http, encoding/json, …) | 🚧 in progress |

`goclr build`/`goclr run` emit and execute a real `.dll` for the implemented
language subset: functions (with recursion), int/int32/bool variables and
constants, arithmetic/comparison/logical/bitwise operators, `if`/`for`/`switch`
with `break`/`continue`, and `println`/`print`. Anything beyond that is rejected
with an actionable `GCLR0301` until later increments. Try it:

```bash
go build -o bin/goclr ./cmd/goclr
bin/goclr run ./tests/conformance/015_fib   # -> fib(0..9), matches `go run`
```

## Install / build

```bash
go build -o bin/goclr ./cmd/goclr
```

## Usage

```bash
goclr doctor                       # verify Go + .NET environment
goclr analyze ./cmd/server         # compatibility report (echo-goja profile)
goclr analyze ./... --json         # machine-readable report
goclr build ./cmd/server -o bin/server.dll
goclr run ./cmd/server
goclr clean
```

## Architecture

```
Go module → loader (go/packages) → type checker → Go SSA
  → GoCLR IR → lower to CLR IR → emit CIL + metadata → .NET assembly
  → GoCLR runtime → dotnet app.dll
```

| Path | Responsibility |
| --- | --- |
| `cmd/goclr` | CLI entry point |
| `internal/frontend` | Load Go packages + type info via `go/packages` |
| `internal/analysis` | cgo/asm/unsafe checks, stdlib overlay map, `echo-goja` profile |
| `internal/diagnostics` | `GCLRxxxx` codes, human + JSON rendering |
| `runtime/dotnet` | `GoCLR.Runtime` — Go value/runtime semantics on .NET |
| `stdlib` | GoCLR standard-library overlay (per-package) |
| `tests` | conformance (go vs goclr) + Echo/goja integration |

The frontend deliberately uses only public Go tooling so the backend can later
be re-targeted at `cmd/compile/internal/ssa` without rewriting the loader.
