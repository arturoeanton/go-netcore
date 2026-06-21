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

## Highlight — goja evaluates JavaScript as a .NET assembly

goja (a JavaScript interpreter written in pure Go) **compiles** to a ~15 MB
ECMA-335 assembly, **loads and JITs cleanly** on the CLR, runs its full package
init, and **evaluates a large subset of JavaScript** — with output identical to
`go run`:

```text
1 + 2 * 3                                => 7
"Hello, " + "goclr" + "!"                => Hello, goclr!
"saas platform".toUpperCase()            => SAAS PLATFORM
"abcdef".slice(1, 4)                     => bcd
Math.max(3, 9, 4) + Math.floor(2.7)      => 11
var o = { x: 10, y: 32 }; o.x + o.y      => 42
var s = 0; for (var i = 1; i <= 100; i++) s += i; s   => 5050
var n = 6, f = 1; while (n > 1) { f *= n; n--; } f    => 720   (factorial)
(function (n){ var a=0,b=1; for(var i=0;i<n;i++){var t=a+b;a=b;b=t;} return a; })(15)  => 610
```

No C# host, no JS runtime: a Go JS engine running as managed .NET code. See
[`examples/demo_goja`](examples/demo_goja/). Getting a dependency this heavy to run
exercises a very large surface of the compiler and runtime, so most lighter
libraries work. Working today: arithmetic, strings and string methods, `Math`,
objects and property access, function calls/closures, `for`/`while` loops, array
callbacks (`map`/`filter`/`reduce`/`sort`), `Object.keys`, and `JSON.stringify`/
`JSON.parse` round-trips — all byte-identical to `go run`.

A second validation target, **Gin** ([`examples/demo_gin`](examples/demo_gin/)), is
under way: its closure compiles through the `go-playground/validator` (which exercises
`reflect` heavily), `yaml.v3`, and Gin's form/header/query/JSON binding and rendering
layers; the remaining frontier is the `x/net/http2` stack.

## Status

The compiler runs end-to-end: front half + the ECMA-335 emitter + the .NET runtime
+ a stdlib overlay. **179 conformance fixtures pass byte-for-byte vs `go run`.**
See [ROADMAP.md](ROADMAP.md) / [ROADMAP-M2.5.md](ROADMAP-M2.5.md) for the milestone
breakdown and [LIMITATIONS.md](LIMITATIONS.md) / [GAPS.md](GAPS.md) for the tracked
gaps.

| Area | State |
| --- | --- |
| CLI (`build`/`run`/`analyze`/`test`/`doctor`/`clean`) | ✅ wired |
| `analyze` (cgo/asm/unsafe + echo-goja profile, human + JSON) | ✅ functional |
| Frontend loader (`go/packages`, type info, build tags) | ✅ functional |
| .NET runtime core (GoString, slices, maps, pointers, interfaces, defer/panic, goroutines, channels, closures) | ✅ runs on `net8.0`+ |
| **M1 + M2 language** (control flow, funcs/methods, structs, slices, maps, pointers, multi-return, interfaces, defer/panic/recover, closures, generics, goroutines/channels/select, complex) | ✅ closed |
| **Language hardening** — embedded-struct field/method promotion (value + pointer embeds, incl. pointer-receiver methods promoted from a value field), per-iteration loop vars, cross-package generics, fixed arrays (incl. as struct fields) + keyed literals, `&slice[i]`, `&s.field`, identical-layout struct conversion (`type A B`), typed-box across slices/interfaces | ✅ |
| **Large-program emitter** — 4-byte metadata heap indices (`HeapSizes=0x07`), `InitLocals`, fat-method headers — required once heaps/method tables exceed the small-program limits (goja) | ✅ |
| **M2.5 overlay** — multi-package, globals/`init`, C# shim/extern mechanism, stdlib source overlays (`unicode`/`sort`/`slices`) | ✅ |
| **P0 stdlib** (hardened) — fmt/strconv/strings/bytes/unicode/utf8/sort/math/errors/reflect(r+w)/encoding-json(M+U+streaming)/time/sync/math-rand/context/io/os | ✅ byte-exact |
| **P1 stdlib** — net/http client+server, net TCP (+ParseIP/ParseMAC/ParseCIDR), crypto (sha/sha3/md5/hmac/rand/subtle), regexp, path/filepath, net/url, bufio/io, log, math/big, container/list, os/exec, mime, net/mail, net/textproto, io/fs | ✅ |
| **P2 stdlib** — encoding (csv/hex/base64/base32/binary), compress (gzip/zlib/flate), crypto/aes-GCM, crypto/sha3 (+SHAKE) | ✅ |
| **reflect — runtime type descriptors** ([REFLECT.md](REFLECT.md)) — precise `Kind`/`Name`/`String`/fields/tags (static + dynamic), `MapOf`/`SliceOf`/`PtrTo`/`ArrayOf`, `Implements`/`AssignableTo`, `Zero`/`New` | ✅ descriptor-backed |
| **goja** — evaluates a large JS subset (arithmetic, strings, `Math`, objects, closures, loops, array callbacks, `JSON.stringify`/`parse`) byte-identical to `go run` | ✅ |
| **Gin** — compiles through validator/yaml/binding/render; `x/net/http2` stack pending | 🟡 |
| Echo (autocert/acme pending), AOT/perf (P4) | 🚧 |

## Requirements

- **Go** (matching the `go` directive in `go.mod`) for the compiler frontend.
- **.NET SDK** (`dotnet`, net8.0 or newer) — goclr builds the C# runtime/stdlib
  assemblies on first use and the produced `.dll` runs on `dotnet`.

## Getting started

```bash
go build -o bin/goclr ./cmd/goclr
bin/goclr doctor                          # verify Go + .NET environment
bin/goclr run ./tests/conformance/015_fib # fib(0..9), matches `go run`
go test ./tests/conformance/              # all 179 fixtures vs `go run`
```

The first `build`/`run` compiles the `GoCLR.Runtime` and `GoCLR.Stdlib` C# projects
under `runtime/` automatically (cached afterwards). To point at prebuilt copies,
set `GOCLR_RUNTIME_DLL` / `GOCLR_STDLIB_DLL`.

### Running a program that uses vendored dependencies (e.g. the goja demo)

Targets that depend on third-party packages needing a goclr overlay (goja, via its
`regexp2`/`unistring`/typedarray `unsafe.Pointer` code) **must be vendored**: the
overlays in [`goclr.overlays/`](goclr.overlays/) are swapped into `vendor/<path>`
before type-checking, and apply *only* when `vendor/` is present. `vendor/` is not
committed, so on a fresh checkout you must recreate it first:

```bash
go mod vendor                                      # recreate vendor/ from go.mod
go run ./cmd/goclr/main.go run examples/demo_goja  # now the overlays apply
```

Without `vendor/`, goclr reads those dependencies from the module cache where the
overlay cannot apply, and fails with `GCLR0201: unsupported unsafe operation`.
(Self-contained programs that only use the standard library do not need this.)

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
Go module → loader (go/packages) → type checker
  → GoCLR IR → lower to CLR IR → emit CIL + metadata → .NET assembly
  → GoCLR runtime → dotnet app.dll
```

| Path | Responsibility |
| --- | --- |
| `cmd/goclr` | CLI entry point |
| `internal/frontend` | Load Go packages + type info via `go/packages`; apply stdlib + project overlays |
| `internal/analysis` | cgo/asm/unsafe checks, stdlib overlay map, `echo-goja` profile |
| `internal/lower` | AST → GoCLR IR lowering (language + stdlib shims) |
| `internal/emit` | ECMA-335 metadata + CIL emitter (the `.dll`) |
| `internal/diagnostics` | `GCLRxxxx` codes, human + JSON rendering |
| `runtime/dotnet` | `GoCLR.Runtime` — Go value/runtime semantics on .NET |
| `runtime/stdlib` | `GoCLR.Stdlib` — Go standard-library shims (Go API → .NET) |
| `goclr.overlays` | Project-supplied goclr-safe rewrites of vendored dependency files |
| `tests` | conformance (go vs goclr) + validation apps + goja/Echo integration |

The frontend deliberately uses only public Go tooling so the backend can later be
re-targeted at `cmd/compile/internal/ssa` without rewriting the loader.
