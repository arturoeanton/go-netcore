# Quick Start

This is the longer, troubleshooting-oriented companion to the
[README Quick Start](../README.md#quick-start). It walks a newcomer from a clean
checkout to running real Go programs — including the framework demos — on the .NET
CLR, and explains the two things that most often trip people up: the **runtime DLL
build** and **vendoring**.

## Prerequisites

| Tool | Why | Check |
| --- | --- | --- |
| **Go** (version in [`go.mod`](../go.mod)'s `go` directive) | The frontend loads and type-checks your code with `go/packages`. | `go version` |
| **.NET SDK** (net8.0 or newer) | goclr builds its C# runtime/stdlib assemblies and runs the emitted `.dll` on `dotnet`. | `dotnet --version` |

Run `goclr doctor` at any time to confirm both are detected.

## 1. Build the compiler

```bash
git clone https://github.com/arturoeanton/go-netcore.git
cd go-netcore
go build -o bin/goclr ./cmd/goclr
bin/goclr doctor
```

`go build` produces the `bin/goclr` binary. Everything below assumes you run it from
the repository root (the linker searches upward from the working directory for the
runtime projects — see [Troubleshooting](#troubleshooting)).

## 2. The runtime DLLs (built for you on first run)

goclr emits IL that calls into two C# assemblies:

- **`GoCLR.Runtime`** ([`runtime/dotnet`](../runtime/dotnet/)) — Go value and runtime
  semantics on .NET: `GoString`, slices, maps, pointers, interfaces, `defer`/`panic`/
  `recover`, goroutines, channels, closures.
- **`GoCLR.Stdlib`** ([`runtime/stdlib`](../runtime/stdlib/)) — the standard-library
  shims that present a Go-shaped API over .NET.

These `.dll`s are **gitignored** (built per machine). The **first** `goclr build` or
`goclr run` builds them automatically via `dotnet build -c Release`; later runs use the
cached output. The cache is invalidated whenever a runtime `.cs`/`.csproj` is newer than
the built DLL — so after a `git pull` that changes the runtime, the next build
recompiles it for you (this prevents a stale assembly from failing at load with
`MissingMethodException`).

To build them by hand (e.g. to warm the cache before timing a run):

```bash
dotnet build runtime/stdlib/GoCLR.Stdlib.csproj -c Release   # builds Stdlib *and* Runtime
```

> **dotnet incremental-build caveat.** `dotnet build` is incremental and occasionally
> reuses stale output across large changes. If you see a `MissingMethodException` or a
> `TypeLoadException` at run time right after editing the runtime, force a clean build:
> `dotnet build runtime/stdlib/GoCLR.Stdlib.csproj -c Release -t:Rebuild` (or
> `goclr clean` to drop goclr's own artifacts).

### Pointing at prebuilt DLLs

To skip the on-demand build (e.g. in CI, or to share one build across checkouts), set
the paths explicitly:

```bash
export GOCLR_RUNTIME_DLL=/abs/path/to/GoCLR.Runtime.dll
export GOCLR_STDLIB_DLL=/abs/path/to/GoCLR.Stdlib.dll
```

## 3. Your first program

```bash
mkdir -p hello
cat > hello/main.go <<'EOF'
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
EOF

bin/goclr run ./hello
```

`goclr run` lowers the package, links it, and runs the resulting `.dll` on `dotnet`.
Its output is byte-for-byte identical to `go run ./hello`.

To produce a standalone, runnable assembly:

```bash
bin/goclr build ./hello -o bin/hello.dll
dotnet bin/hello.dll
```

`build` writes `hello.dll`, its `hello.runtimeconfig.json`, and copies
`GoCLR.Runtime.dll` / `GoCLR.Stdlib.dll` next to it, so `dotnet bin/hello.dll` runs
standalone.

## 4. Running the framework demos

The demos under [`examples/`](../examples/) depend on third-party packages that need a
goclr **overlay** (a goclr-safe rewrite of a hard-to-lower dependency file). Overlays
apply only when the dependency is **vendored**, so on a fresh checkout you must recreate
`vendor/` once:

```bash
go mod vendor                         # recreate vendor/ from go.mod (gitignored)
bin/goclr run ./examples/demo_gin     # Gin router on :8080
bin/goclr run ./examples/demo_goja    # goja evaluating JavaScript
```

Without `vendor/`, goclr reads those dependencies straight from the module cache where
the overlay cannot apply, and fails with `GCLR0201: unsupported unsafe operation`. The
Quick Start hello-world (standard library only) does **not** need vendoring.

Each demo has its own `README.md` describing what it serves; see the table in the
[README](../README.md#running-the-demos). To smoke-test all of them at once:

```bash
bash scripts/validate_demos.sh
```

## 5. Checking compatibility before you compile

Before pointing goclr at a large dependency tree, ask it what would block:

```bash
bin/goclr analyze ./...                       # human-readable compatibility report
bin/goclr analyze ./... --html -o report.html # self-contained HTML report
bin/goclr coverage                            # per-function stdlib coverage matrix
```

`analyze` flags cgo, Go assembly, and blocking `unsafe.Pointer` usage (with `GCLRxxxx`
diagnostics), and reports each package against the `echo-goja` profile. `coverage` shows
how much of each standard-library package's exported API goclr covers (snapshot in
[COVERAGE.md](COVERAGE.md)).

## Troubleshooting

| Symptom | Cause | Fix |
| --- | --- | --- |
| `could not locate runtime/.../*.csproj` | Run from outside the repo, or the runtime tree was moved. | Run goclr from the repo root, or set `GOCLR_RUNTIME_DLL` / `GOCLR_STDLIB_DLL`. |
| `GCLR0201: unsupported unsafe operation` building a demo | `vendor/` missing, so the overlay can't apply. | `go mod vendor` at the repo root. |
| `MissingMethodException` / `TypeLoadException` at run time | Stale runtime DLL after a runtime edit. | Rebuild: `dotnet build runtime/stdlib/GoCLR.Stdlib.csproj -c Release -t:Rebuild`, or `goclr clean`. |
| `GCLR0100` (cgo) / `GCLR0200` (assembly) | The package uses cgo or Go assembly — unsupported. | Use a pure-Go build path / build tag, or an overlay. |
| Output differs from `go run` for `time.Now()` | `time` is UTC-only in goclr. | Use `.UTC()` / `time.UTC` for deterministic output (see [LIMITATIONS.md](LIMITATIONS.md)). |
| A recovered panic is fine but the origin is unclear | The throw site is masked by `recover()`. | Set `GOCLR_PANIC_TRACE=1` to print the panic's .NET throw-site stack. |

## Next steps

- [ROADMAP.md](ROADMAP.md) — what's implemented vs. in progress.
- [LIMITATIONS.md](LIMITATIONS.md) — the deliberately-deferred edges (each fails predictably).
- [COVERAGE.md](COVERAGE.md) — standard-library coverage matrix.
- [REFLECT.md](REFLECT.md), [DESIGN-typed-box.md](DESIGN-typed-box.md) — how the
  reflection and named-type machinery work.
</content>
