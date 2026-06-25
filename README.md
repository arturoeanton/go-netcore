# goclr

> **goclr compiles pure-Go applications to .NET assemblies** — it lowers a Go module
> to ECMA-335 IL and emits a `.dll` that runs on `dotnet`. Target: business apps,
> headless services, and SaaS back-ends that want to run Go code on the CLR.

`goclr` is an **experimental, MVP-stage** compiler. It takes a pure-Go project and
produces an executable .NET assembly. It is **not** a drop-in replacement for the
official Go toolchain, it is **not** 100% compatible, and it does **not** support
cgo or Go assembly. What it *is*: a real backend that lowers a large, honest subset
of Go — including goroutines, channels, generics, interfaces, `defer`/`panic`/
`recover`, and reflection — plus a standard-library overlay, well enough to run
demanding third-party frameworks end to end on `dotnet`.

The compiler is **application-agnostic**: it implements general Go language and
standard-library semantics. Projects supply their own *overlays* for any
hard-to-lower vendored dependency (see [`goclr.overlays/`](goclr.overlays/)). The
validation targets below — [goja](https://github.com/dop251/goja),
[Gin](https://github.com/gin-gonic/gin), [Echo](https://github.com/labstack/echo) —
drive the roadmap by forcing general Go features to be correct; they earn **no**
special cases in the compiler.

---

## At a glance

| | |
| --- | --- |
| **Version** | `0.1.0-mvp` |
| **Conformance** | **418 fixtures** pass byte-for-byte vs `go run` ([`tests/conformance`](tests/conformance/)) |
| **stdlib coverage** | ~51% of the exported standard-library API across 95 packages (`goclr coverage`, snapshot in [docs/COVERAGE.md](docs/COVERAGE.md)) |
| **CLI** | `build` · `run` · `analyze` · `coverage` · `test` · `doctor` · `clean` · `version` |
| **Runs end to end on the CLR** | goja (JS engine) · Gin · Gin + `database/sql` + pure-Go SQLite · Echo · errgroup · uuid · jwt (HS256) · testify |
| **Requires** | Go (per `go.mod`) + .NET SDK (net8.0 or newer) |

> **New here?** Jump straight to the **[Quick Start](#quick-start)** — install, build,
> and run your first Go-on-the-CLR program in a few minutes. For a longer, troubleshooting-
> oriented walkthrough see **[docs/QUICKSTART.md](docs/QUICKSTART.md)**.

---

## Quick Start

### 1. Prerequisites

- **Go** — the version in the `go` directive of [`go.mod`](go.mod) (the frontend uses
  `go/packages`, so a matching toolchain must be installed).
- **.NET SDK** — `dotnet`, **net8.0 or newer**. goclr builds its C# runtime/stdlib
  assemblies with it, and the produced `.dll` runs on it.

Verify both are visible to goclr at any point with `goclr doctor`.

### 2. Clone and build the compiler

```bash
git clone https://github.com/arturoeanton/go-netcore.git
cd go-netcore

go build -o bin/goclr ./cmd/goclr     # build the goclr CLI
bin/goclr doctor                      # confirm Go + .NET are detected
```

The two C# assemblies — `GoCLR.Runtime` (Go value/runtime semantics) and
`GoCLR.Stdlib` (the standard-library shims) — are **built automatically** the first
time you `build` or `run` a program (and rebuilt whenever a runtime `.cs`/`.csproj`
changes). The first run therefore takes longer; subsequent runs use the cache. You
can also build them by hand:

```bash
dotnet build runtime/stdlib/GoCLR.Stdlib.csproj -c Release   # builds Stdlib + Runtime
```

> The runtime `.dll`s are gitignored (built per machine), so a fresh clone builds
> them on first use. To point at prebuilt copies instead, set the `GOCLR_RUNTIME_DLL`
> and `GOCLR_STDLIB_DLL` environment variables to their paths.

### 3. Hello, world

Create `hello/main.go`:

```go
package main

import "fmt"

func main() {
	fmt.Println("Hello from Go, running on .NET!")
	for i := 1; i <= 3; i++ {
		fmt.Printf("fib(%d) = %d\n", i, fib(i))
	}
}

func fib(n int) int {
	if n < 2 {
		return n
	}
	return fib(n-1) + fib(n-2)
}
```

Compile and run it on the CLR:

```bash
bin/goclr run ./hello
```

`goclr run` lowers the package to IL, links it against the runtime, and executes the
resulting `.dll` on `dotnet` — its output is byte-for-byte identical to `go run ./hello`.

To produce a standalone assembly instead of running it:

```bash
bin/goclr build ./hello -o bin/hello.dll   # emits hello.dll + runtimeconfig + runtime DLLs
dotnet bin/hello.dll                        # run it directly
```

### 4. Run the bundled demos

```bash
bash scripts/validate_demos.sh   # smoke-test every demo (servers + run-once)
go test ./tests/conformance/     # run all 418 conformance fixtures vs `go run`
```

See **[Running the demos](#running-the-demos)** for what each demo does and which ones
need `go mod vendor` first.

---

## Highlight — goja evaluates JavaScript as a .NET assembly

[goja](https://github.com/dop251/goja), a JavaScript interpreter written in pure Go,
**compiles** to a ~15 MB ECMA-335 assembly, **loads and JITs cleanly** on the CLR,
runs its full package init, and **evaluates a large subset of JavaScript** — with
output identical to `go run`:

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
exercises a very large surface of the compiler and runtime, so most lighter libraries
work too. Working today: arithmetic, strings and string methods, `Math`, objects and
property access, function calls/closures, `for`/`while` loops, array callbacks
(`map`/`filter`/`reduce`/`sort`), `Object.keys`, and `JSON.stringify`/`JSON.parse`
round-trips — all byte-identical to `go run`.

## Highlight — a full Gin + database/sql + pure-Go SQLite stack on .NET

A second validation target, **Gin**, also **runs end to end on the CLR**.
[`examples/demo_gin_sql`](examples/demo_gin_sql/) is a Gin REST API backed by
`database/sql` and the pure-Go, zero-cgo SQLite driver
[go-r2-sqlite](https://github.com/arturoeanton/go-r2-sqlite) — **the entire stack
compiled to IL by goclr**: the Gin router/middleware, the `database/sql` pool and
`driver` layer, and the ~14k-line SQLite engine (B-tree, pager, SQL parser/VDBE)
itself. Nothing native is loaded — no cgo, no managed database. Full CRUD,
byte-accurate:

```text
GET  /notes      → [{"id":1,"text":"first note"},{"id":2,"text":"second note"}]
GET  /notes/1    → {"id":1,"text":"first note"}
POST /notes      → {"id":3,"text":"from goclr"}      (persisted)
GET  /notes/999  → 404 {"error":"not found"}
```

[`examples/demo_gin`](examples/demo_gin/) is the router on its own (`/health`,
`/ping`, `/hello/:name`). Driving this stack — Gin's binding/render, the
`go-playground/validator` (reflect-heavy), `yaml.v3`, `database/sql`, and the SQLite
engine — through goclr's backend forced a large set of general language and runtime
features to be correct.

---

## How it works

```text
Go module
  → frontend (go/packages: load + type-check, apply stdlib + project overlays)
  → analysis (cgo/asm/unsafe checks, stdlib overlay map, echo-goja profile)
  → lower (Go AST/types → GoCLR IR; language + stdlib shims)
  → emit (ECMA-335 metadata + CIL → the .dll)
  → linker (runtimeconfig.json + copy GoCLR.Runtime.dll / GoCLR.Stdlib.dll)
  → dotnet app.dll
```

Two ideas make the standard library and third-party code tractable:

- **The shim model.** Most of the standard library is provided as **C# shims**
  (`runtime/stdlib`, the `GoCLR.Stdlib` assembly) that present a Go-shaped API over
  .NET. Some packages instead **compile from their real Go source** (e.g. `unicode`,
  `sort`, `slices`, `io`, `database/sql`, `container/ring`). A project can replace any
  hard-to-lower dependency file with a goclr-safe rewrite via an *overlay*.
- **The typed box.** A named Go value (`type Money int64`, a named slice) keeps its
  runtime type identity across interfaces, so `fmt` Stringer dispatch, `%T`, `reflect`,
  and precise interface dispatch all work for representation-sharing types. See
  [docs/DESIGN-typed-box.md](docs/DESIGN-typed-box.md) and
  [docs/REFLECT.md](docs/REFLECT.md) for the deeper design.

The frontend deliberately uses only public Go tooling (`go/packages`), so the backend
can later be re-targeted at `cmd/compile/internal/ssa` without rewriting the loader.

---

## What's supported

The compiler runs end-to-end: frontend + the ECMA-335 emitter + the .NET runtime + a
standard-library overlay. **418 conformance fixtures pass byte-for-byte vs `go run`.**

| Area | State |
| --- | --- |
| CLI (`build`/`run`/`analyze`/`coverage`/`test`/`doctor`/`clean`/`version`) | ✅ wired |
| `analyze` (cgo/asm/unsafe + echo-goja profile, human + JSON + HTML) | ✅ functional |
| `coverage` (per-function stdlib coverage matrix, human + JSON) | ✅ functional |
| Frontend loader (`go/packages`, type info, build tags) | ✅ functional |
| .NET runtime core (GoString, slices, maps, pointers, interfaces, defer/panic, goroutines, channels, closures) | ✅ runs on `net8.0`+ |
| **M1 + M2 language** — control flow, funcs/methods, structs, slices, maps, pointers, multi-return, interfaces, defer/panic/recover, closures, generics, goroutines/channels/select, complex | ✅ closed |
| **Language hardening** — embedded-struct field/method promotion (value + pointer embeds, incl. pointer-receiver methods promoted from a value field), per-iteration loop vars, cross-package generics, fixed arrays (incl. as struct fields) + keyed literals, `&slice[i]`, `&s.field`, identical-layout struct conversion (`type A B`), typed-box across slices/interfaces | ✅ |
| **Generics depth** — generic types/methods satisfy interfaces (`fmt.Stringer`/`error` dispatch + implicit fmt), local helper types monomorphized correctly, `%T`/`reflect.Type.String()` named like Go (`main.Pair[string,int]`) | ✅ |
| **Range-over-func (Go 1.23/1.24 iterators)** — `for v := range seq` over `iter.Seq`/`iter.Seq2`, `iter.Pull`/`Pull2`, and direct consumption of `slices.All/Values/Backward/Sorted/Chunk`, `maps.Keys/Values/All`, `strings`/`bytes` `Lines`/`SplitSeq`/`FieldsSeq` | ✅ |
| **Large-program emitter** — 4-byte metadata heap indices (`HeapSizes=0x07`), `InitLocals`, fat-method headers — required once heaps/method tables exceed the small-program limits (goja) | ✅ |
| **M2.5 overlay** — multi-package, globals/`init`, C# shim/extern mechanism, stdlib source overlays | ✅ |
| **P0 stdlib** (hardened) — fmt/strconv/strings/bytes/unicode/utf8/sort/math/errors/reflect(r+w)/encoding-json(M+U+streaming)/time/sync/math-rand/context/io/os | ✅ byte-exact |
| **P1 stdlib** — net/http client+server, net TCP (+ParseIP/ParseMAC/ParseCIDR), crypto (sha/sha3/md5/hmac/rand/subtle), regexp, path/filepath, net/url, bufio/io, log, math/big, container/list, container/heap, container/ring, text/tabwriter, index/suffixarray, os/exec, mime, net/mail, net/textproto, io/fs | ✅ |
| **P2 stdlib** — encoding (csv/hex/base64/base32/ascii85/pem/binary), compress (gzip/zlib/flate), crypto/aes-GCM, crypto/sha3 (+SHAKE) | ✅ |
| **P3 stdlib** — net/http server (HttpListener) + httptest + cookiejar, net UDP, log/slog (text+JSON), os/signal (real SIGINT/TERM), `database/sql` + `database/sql/driver`, mime/multipart | ✅ |
| **P4 stdlib** — `crypto/x509` + `acme`/`autocert` closure (TLS path a no-op shim), `encoding/xml` (marshal-only), `text/template` + `html/template` (real parse/exec + contextual escaping) | ✅ |
| **database/sql + a pure-Go SQLite engine** — `go-r2-sqlite` (zero-cgo, ~14k LOC) compiled through goclr; CREATE/INSERT/SELECT with INTEGER/REAL/TEXT scanned into Go types | ✅ |
| **reflect — runtime type descriptors** ([docs/REFLECT.md](docs/REFLECT.md)) — precise `Kind`/`Name`/`String`/fields/tags (static + dynamic), `MapOf`/`SliceOf`/`PtrTo`/`ArrayOf`, `Implements`/`AssignableTo`, `Zero`/`New`, plus deep reflect (`Value.Call`, `MakeFunc`, `Method.Call`) for the program's own types | ✅ descriptor-backed |
| **`unsafe.Pointer` — the safe idioms** ([docs/DESIGN-unsafe-pointer.md](docs/DESIGN-unsafe-pointer.md)) — `string↔[]byte` zero-copy reinterprets and read-only `reflect.SliceHeader`/`StringHeader` offset views; pointer-arith / header *writes* rejected with a clear diagnostic | ✅ |
| **`goclr test`** — runs `TestXxx`/subtests on the CLR via a real-Go `testing` overlay (testify runs); benchmarks/fuzz/`TestMain`/flags not yet | ✅ |
| **goja** — evaluates a large JS subset byte-identical to `go run` | ✅ runs |
| **Gin** — router, middleware, JSON binding/render; full CRUD over `database/sql` + pure-Go SQLite | ✅ runs |
| **Echo v4** — router, path params, JSON, status codes serve on the CLR; the `crypto/x509` + `acme`/`autocert` TLS closure lowers (TLS is an honest no-op shim, plain HTTP fully exercised) | ✅ runs |
| AOT / performance pass (P4 perf) | 🚧 measured, not engineered |

For the full milestone breakdown and the done/pending checklist see
[docs/ROADMAP.md](docs/ROADMAP.md). For the honest list of edges and deferred
features see [docs/LIMITATIONS.md](docs/LIMITATIONS.md). For the gap analysis toward
a complete product see [docs/GAPS.md](docs/GAPS.md).

### Honest limitations

goclr is an MVP; some things are deliberately deferred and **documented to fail
predictably, not silently**. The headline ones (full detail in
[docs/LIMITATIONS.md](docs/LIMITATIONS.md)):

- **No cgo, no Go assembly, no general `unsafe.Pointer`** — goclr's value model has no
  raw memory. Pointer arithmetic and header *writes* are rejected with `GCLR0201`/
  `GCLR0301`; only the safe `string↔[]byte` and header-read idioms lower.
- **`time` is UTC-only** — use `.UTC()` / `time.UTC` for cross-runtime-deterministic output.
- **`encoding/xml` is marshal-only** — `Unmarshal`/`Decode` return an honest error.
- **`json.Marshal` of a custom `json.Marshaler`, and re-marshaling `[]json.Number` /
  `map[string]json.RawMessage`**, are deferred (the named identity is lost inside a
  type-erased container). Reading *into* those is correct.
- **Some `%T`/`%#v` precision** on dynamically-reached slices/maps, and a few `fmt`
  width edges, are not byte-exact.
- **Goroutine scheduling** is the .NET thread pool's, not Go's — keep concurrent output
  order-independent.

---

## Running the demos

All demos live under [`examples/`](examples/). Run any one with `goclr run`:

```bash
bin/goclr run ./examples/demo_gin       # Gin router on :8080  (/health, /ping, /hello/:name)
```

| Demo | What it shows | Needs `go mod vendor`? |
| --- | --- | --- |
| [`demo_goja`](examples/demo_goja/) | goja JS engine evaluating JavaScript on the CLR | yes (overlays for goja's `unsafe`) |
| [`demo_gin`](examples/demo_gin/) | Gin router serving HTTP | yes |
| [`demo_gin_sql`](examples/demo_gin_sql/) | Gin + `database/sql` + pure-Go SQLite, full CRUD | yes |
| [`demo_echo`](examples/demo_echo/) | Echo v4 router/JSON serving HTTP | yes |
| [`demo_fiber`](examples/demo_fiber/) | Fiber/fasthttp serving (staged target) | yes |
| [`demo_errgroup`](examples/demo_errgroup/) | `x/sync/errgroup` concurrent + first-error | yes |
| [`demo_uuid`](examples/demo_uuid/) | `google/uuid` v4/v5, parse/format | yes |
| [`demo_jwt`](examples/demo_jwt/) | `golang-jwt/v5` HS256 sign + verify | yes |
| [`demo_testify`](examples/demo_testify/) | `testify/assert` under `goclr test` | yes |

> **Why `go mod vendor`?** Demos that depend on third-party packages needing a goclr
> overlay must be **vendored**: the overlays in [`goclr.overlays/`](goclr.overlays/)
> are swapped into `vendor/<path>` before type-checking, and apply *only* when
> `vendor/` is present (it is gitignored). On a fresh checkout, run `go mod vendor`
> once at the repo root. Self-contained programs that use only the standard library
> (like the Quick Start hello-world) do **not** need this.

Smoke-test every demo at once, and run the conformance suite:

```bash
bash scripts/validate_demos.sh   # servers get a browser-shaped request; run-once demos must not crash
go test ./tests/conformance/     # 418 fixtures, each compared byte-for-byte vs `go run`
```

---

## CLI reference

```bash
goclr doctor                       # verify Go + .NET environment
goclr analyze ./cmd/server         # compatibility report (echo-goja profile)
goclr analyze ./... --json         # machine-readable report (stable summary + per-package)
goclr analyze ./... --html -o report.html   # self-contained HTML report
goclr coverage                     # per-function stdlib coverage matrix
goclr coverage --gap --json        # machine-readable coverage (gaps only)
goclr build ./cmd/server -o bin/server.dll
goclr run ./cmd/server
goclr test ./pkg                   # compile + run TestXxx on the CLR
goclr clean                        # remove goclr build artifacts
goclr version
```

Run `goclr <command> -h` for command-specific flags.

---

## Project layout

| Path | Responsibility |
| --- | --- |
| [`cmd/goclr`](cmd/goclr/) | CLI entry point |
| [`internal/frontend`](internal/frontend/) | Load Go packages + type info via `go/packages`; apply stdlib + project overlays |
| [`internal/analysis`](internal/analysis/) | cgo/asm/unsafe checks, stdlib overlay map, `echo-goja` profile, coverage |
| [`internal/lower`](internal/lower/) | Go AST/types → GoCLR IR lowering (language + stdlib shims) |
| [`internal/goir`](internal/goir/) | GoCLR intermediate representation |
| [`internal/emit`](internal/emit/) | ECMA-335 metadata + CIL emitter (the `.dll`) |
| [`internal/linker`](internal/linker/) | runtimeconfig.json + copy runtime/stdlib DLLs |
| [`internal/diagnostics`](internal/diagnostics/) | `GCLRxxxx` codes, human + JSON rendering |
| [`internal/cli`](internal/cli/) | command dispatch |
| [`runtime/dotnet`](runtime/dotnet/) | `GoCLR.Runtime` — Go value/runtime semantics on .NET |
| [`runtime/stdlib`](runtime/stdlib/) | `GoCLR.Stdlib` — Go standard-library shims (Go API → .NET) |
| [`goclr.overlays`](goclr.overlays/) | project-supplied goclr-safe rewrites of vendored dependency files |
| [`examples`](examples/) | runnable demos (goja, Gin, Echo, jwt, uuid, …) |
| [`tests`](tests/) | conformance (`go` vs `goclr`) + validation apps + `goclr test`/panic fixtures |
| [`docs`](docs/) | design notes, roadmap, gaps, limitations ([index](docs/README.md)) |

---

## Development workflow

The standard inner loop:

```bash
go build -o bin/goclr ./cmd/goclr     # rebuild the CLI after a frontend/lower/emit change
go test ./tests/conformance/          # all 418 fixtures vs `go run`
go test ./internal/...                # backend unit tests (emit/lower/linker/analysis)
bash scripts/validate_demos.sh        # smoke-test the demos
```

After editing any C# under `runtime/`, the next `goclr build`/`run` rebuilds the
affected assembly automatically (the cache is invalidated when a `.cs`/`.csproj` is
newer than the built DLL). Every "done" feature is closed with a **byte-exact
conformance fixture vs `go run`**, green validators, and documentation — see the
"no technical debt" principle in [docs/VISION.md](docs/VISION.md).

When contributing, please keep documentation in sync: update
[docs/ROADMAP.md](docs/ROADMAP.md) / [docs/priorizar.md](docs/priorizar.md) when you
close a roadmap item, and [docs/LIMITATIONS.md](docs/LIMITATIONS.md) when you add or
remove a tracked limitation.

---

## Documentation

All design and planning docs live under [`docs/`](docs/) — start with the
**[documentation index](docs/README.md)**, which lists every doc with a one-line
description and a recommended reading order. Highlights:

- [docs/QUICKSTART.md](docs/QUICKSTART.md) — extended Quick Start + troubleshooting
- [docs/ROADMAP.md](docs/ROADMAP.md) — milestones and the done/pending checklist
- [docs/LIMITATIONS.md](docs/LIMITATIONS.md) — tracked technical debt, each fails predictably
- [docs/GAPS.md](docs/GAPS.md) — gap analysis toward a complete product
- [docs/COVERAGE.md](docs/COVERAGE.md) — per-package stdlib coverage matrix
- [docs/REFLECT.md](docs/REFLECT.md) — reflect as compile-time type descriptors
- [docs/DESIGN-typed-box.md](docs/DESIGN-typed-box.md) · [docs/DESIGN-callback-bridge.md](docs/DESIGN-callback-bridge.md) · [docs/DESIGN-unsafe-pointer.md](docs/DESIGN-unsafe-pointer.md) — subsystem design notes

## License

See [LICENSE](LICENSE).
</content>
</invoke>
