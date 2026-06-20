# goclr

> goclr compiles pure-Go applications to .NET assemblies (ECMA-335 IL → a `.dll`
> that runs on `dotnet`), targeting business apps, headless games, and SaaS.

`goclr` is an **experimental** compiler that takes pure-Go projects and compiles
them to executable .NET assemblies. It is **not** a general Go compiler and is
**not** 100% compatible with the official toolchain. It does **not** support cgo.

The compiler is **application-agnostic**: it implements general Go language and
standard-library semantics, and projects supply their own overlays for any
hard-to-lower vendored dependency (see [`goclr.overlays/`](goclr.overlays/)).

[goja](https://github.com/dop251/goja) (a pure-Go JavaScript engine) and
[Echo](https://github.com/labstack/echo) are used as **validation targets** — real,
large, demanding dependencies that force goclr to implement general Go features
correctly — not as products of goclr. They drive the roadmap; they do not earn
special cases in the compiler.

To show the same generality on the everyday target classes, [`tests/validation/`](tests/validation/)
holds whole idiomatic apps — a business/JSON service, a CLI/ETL job, an
interface-driven rules engine, an HTTP service — each byte-exact under `go run`
vs `goclr run`. goja stays the hard target: it is blocked on the **typed-box
keystone** (per-value runtime type identity), designed in
[`docs/DESIGN-typed-box.md`](docs/DESIGN-typed-box.md) — the single foundation
that also delivers precise `%T`, reflect, and named-primitive `Stringer`s.

## Status

The compiler runs end-to-end: front half + the ECMA-335 emitter + the .NET runtime
+ a stdlib overlay. **139 conformance fixtures pass byte-for-byte vs `go run`.**
See [ROADMAP.md](ROADMAP.md) / [ROADMAP-M2.5.md](ROADMAP-M2.5.md) for the milestone
breakdown and [LIMITATIONS.md](LIMITATIONS.md) for the tracked gaps.

| Area | State |
| --- | --- |
| CLI (`build`/`run`/`analyze`/`test`/`doctor`/`clean`) | ✅ wired |
| `analyze` (cgo/asm/unsafe + echo-goja profile, human + JSON) | ✅ functional |
| Frontend loader (`go/packages`, type info, build tags) | ✅ functional |
| .NET runtime core (GoString, slices, maps, pointers, interfaces, defer/panic, goroutines, channels, closures) | ✅ runs on `net8.0` |
| **M1 + M2 language** (control flow, funcs/methods, structs, slices, maps, pointers, multi-return, interfaces, defer/panic/recover, closures, generics, goroutines/channels/select, complex) | ✅ **closed** |
| **Language hardening** — embedded-struct field/method promotion (value + pointer embeds), Go 1.22 per-iteration loop vars (`for` + `range`), **cross-package generics**, fixed arrays/keyed literals, `&slice[i]`, `&^`, `clear`, long-form local opcodes (256+ locals) | ✅ |
| **M2.5 overlay** — multi-package (incl. modules with no dot in the path), globals/`init`, C# shim/extern mechanism, source overlays (`unicode`/`sort`) | ✅ |
| **P0 stdlib** (20 pkgs, hardened) — fmt/strconv/strings/bytes/unicode/utf8/sort/math/errors/reflect(r+w)/encoding-json(M+U)/time/sync/math-rand/context/io/os | ✅ byte-exact |
| **P1 stdlib** — net/http **client + server**, net TCP, crypto (sha/md5/hmac/rand/subtle), regexp, path/filepath, net/url, bufio/io, log, math/big, container/list, os/exec, mime | ✅ |
| **P2 stdlib** — encoding (csv/hex/base64/base32/binary), compress (gzip/zlib/flate), crypto/aes-GCM | ✅ |
| **P3 stdlib** — hash (fnv/crc32/adler32) | 🟡 started |
| goja (M3), Echo (M5), AOT/perf (P4) | 🚧 see [GOJA-STRATEGY.md](GOJA-STRATEGY.md) |

`goclr build`/`goclr run` emit and execute a real `.dll`. The implemented surface
is most of Go plus a growing stdlib; anything beyond it is rejected with an
actionable `GCLR0301`. Try it:

```bash
go build -o bin/goclr ./cmd/goclr
bin/goclr run ./tests/conformance/015_fib            # fib(0..9), matches `go run`
bin/goclr run ./tests/conformance/286_json_unmarshal # struct/slice/map decode
go test ./tests/conformance/                         # all 139 fixtures vs `go run`
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
