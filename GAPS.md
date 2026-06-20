# goclr — gap analysis to a complete, usable product

Status: 🚧 not started · 🟡 partial · ✅ done.
Effort: S <1wk · M 1–2wk · L 3–6wk · XL >6wk (single engineer).
"MVP?" = blocks the §40 success criteria.

## 1. Compiler backend (core gap)

| Component | Missing | State | Effort | MVP? |
|---|---|---|---|---|
| `internal/frontend/ssa.go` | Build Go SSA via `x/tools/go/ssa` | 🚧 | M | ✅ |
| `internal/goir` | Own IR over SSA | 🟡 | M | ✅ |
| `internal/clrir` | CLR IR model | 🚧 | M | ✅ |
| `internal/lower` | SSA→CLR IR lowering | 🟡 | XL | ✅ |
| `internal/emit` | Real CIL + PE + metadata | 🟡 | XL | ✅ |
| `internal/emit/debug.go` | Portable PDB, positions, stack traces | 🚧 | L | — |
| `internal/linker` | runtimeconfig/deps/copy runtime | 🟡 | M | ✅ |
| `internal/metadata` | GoCLR metadata for runtime.Caller/reflect | 🚧 | M | 🟡 |
| `internal/cache` | Incremental cache by module hash | 🚧 | M | — |

## 2. Language lowering

| Feature | State | Effort | MVP? |
|---|---|---|---|
| Basic types + mapping (int/int32/bool done; rest pending) | 🟡 | S | ✅ |
| Package init order + init() + globals | 🚧 | M | ✅ |
| Funcs (recursion) ✅; methods (value + pointer receivers) ✅ | ✅ | M | ✅ |
| Multiple return values + multiple/parallel assignment (object[] tuples) | ✅ | M | ✅ |
| Closures + function values (lambda-lift + GoClosure, by-ref capture) | ✅ | M | ✅ |
| Control flow: if/for/switch ✅; range over string/slice/map ✅ | ✅ | M | ✅ |
| range over channel/int/array, goto, labels, labeled break/continue | ✅ | M | ✅ |
| Variadic functions (`f(args ...T)`, fmt.Println) — needed by fmt/Echo | ✅ | M | ✅ |
| Goroutines (`go f()` + `go func(a){}(x)`) + channels + select | ✅ | L | ✅ |
| Generics / type parameters (functions + types/methods, monomorphized) | ✅ | L | ✅ |
| Extra numeric types (uint*/float*/complex ✅), fallthrough ✅ | ✅ | M | 🟡 |
| range over string (runes + byte index) | ✅ | S | ✅ |
| runtime strings: GoString len/index/concat/compare | ✅ | M | ✅ |
| Structs as value types + composite literals + field access | ✅ | L | ✅ |
| Slices (object[]-backed): make/append/index/range/sub-slice, []byte/[]rune | ✅ | L | ✅ |
| Maps (Dictionary-backed): make/literal/index/comma-ok/delete/range | ✅ | L | ✅ |
| Managed pointers (GoPtr cell): &x/*p, &T{}, new, ptr-to-struct, nil, aliasing | ✅ | M | ✅ |
| Empty interface `any` + type assert + type switch | ✅ | L | ✅ |
| Named interfaces + error + method dispatch (value-receiver implementers) | ✅ | L | ✅ |
| Pointer-receiver interface implementers (GoPtr type-id tag + dispatch) | ✅ | M | ✅ |
| Defer/panic/recover (CIL exception-handling clauses, LIFO defers) | ✅ | M | ✅ |
| Goroutines lowering | ✅ | S | ✅ |
| Channels + select lowering | ✅ | M | 🟡 |
| Generics | ✅ | L | ✅ |
| Reflection lowering + emitted struct-tag descriptors (read-path) | 🟡 | L | ✅ |
| Multi-package lowering + globals + init() + C# shim/extern mechanism | ✅ | XL | ✅ |

## 3. .NET runtime (`GoCLR.Runtime`)

| Piece | State | Effort | MVP? |
|---|---|---|---|
| GoString/Slice/Map/Ptr/Interface/panic/defer/goroutine/channel/error | ✅ | — | ✅ |
| sync: Mutex/RWMutex/Once/WaitGroup/Map (Pool/Cond pending) | 🟡 | M | ✅ |
| sync/atomic | 🚧 | S | ✅ |
| complex64/128 ✅; GoArray, Bytes helpers | 🟡 | S | 🟡 |
| reflect runtime (read-path: kinds/fields/tags/values; write-path pending) | 🟡 | L | ✅ |
| Time (Duration + time.Time/Format), Console/GoFunc/struct value helpers | 🟡 | M | ✅ |
| select runtime, ASCII fast-path, intern pool | 🚧 | M | 🟡 |

## 4. Stdlib overlay (C# shim mechanism live; 82 conformance fixtures byte-exact)

| Package(s) | State | Effort | MVP? |
|---|---|---|---|
| errors/fmt/strconv/strings/bytes/sort/math/math-bits | 🟡 (shimmed; float ftoa parity pending) | M | ✅ |
| strings.Builder / bytes.Buffer / io.WriteString / fmt.Fprint* | ✅ | M | ✅ |
| context (Background/WithValue/WithCancel/WithTimeout) ✅; io ifaces/bufio 🚧 | 🟡 | M | ✅ |
| encoding/json — Marshal + Unmarshal (descriptor-driven write-path) | ✅ | L | ✅ |
| net/http overlay on Kestrel | 🚧 | XL | ✅ |
| net/url, mime, mime/multipart | 🚧 | M | ✅ |
| regexp (+ syntax) | 🚧 | L | ✅ |
| unicode/utf8 ✅; utf16 | 🟡 | S | ✅ |
| reflect (read-path: kinds/fields/tags/values) ✅; settable write-path 🚧 | 🟡 | L | ✅ |
| time (Duration + time.Time/Format) ✅; runtime/log/slog 🚧 | 🟡 | M | ✅ |
| os (env/exit/getpid/Stdout/Stderr) ✅; path/filepath 🚧 | 🟡 | M | ✅ |
| math/rand (seeded, deterministic — Go rngSource port) | ✅ | M | ✅ |
| GoCLR.Stdlib.dll packaging + linker copy | ✅ | M | ✅ |

## 5. Target dependency compatibility

| Target | Missing | State | Effort | MVP? |
|---|---|---|---|---|
| goja | unsafe.Pointer in typedarrays.go | 🚧 | L | ✅ |
| regexp2 | //go:build goclr replacement | 🚧 | M | ✅ |
| x/sys/unix | pure/overlay path (assembly) | 🚧 | M | ✅ |
| Echo v4 | compile package + middleware | 🚧 | L | ✅ |
| ~160 stdlib pkgs in closure | overlay or direct compile | 🚧 | L | ✅ |

## 6. CLI & packaging

| Item | State | Effort | MVP? |
|---|---|---|---|
| build/run producing a real DLL | 🟡 (honest gate) | — | ✅ |
| --emit-il/-ir/-ssa/-cs-stubs, --keep-temp, --explain | 🚧 | M | — |
| --aot/--no-aot, --trim, debug/release | 🚧 | L | — |
| test: testing.T harness, benchmarks | 🚧 | L | 🟡 |
| M7 output bundle (dll+runtimeconfig+runtime+stdlib) | 🟡 | M | ✅ |
| analyze: runtime requirements + reflect sites JSON | 🟡 | S | — |

## 7. Testing & tooling

| Item | State | Effort | MVP? |
|---|---|---|---|
| Conformance runner (go vs goclr: combined stdout/stderr + exit) | ✅ | S | ✅ |
| 86 conformance fixtures (000–286), all byte-exact vs `go run` | ✅ | M | ✅ |
| Backend unit tests (emit PE/determinism/fat-header, lower, linker) | ✅ | S | ✅ |
| Echo integration tests | 🚧 | M | ✅ |
| goja integration tests | 🚧 | M | ✅ |
| Echo+goja + 100 concurrent | 🚧 | M | ✅ |
| Benchmarks | 🚧 | M | — |
| CI | 🚧 | S | — |

## 8. Performance & production readiness

| Item | State | Effort | MVP? |
|---|---|---|---|
| Typed IL, no mass boxing | 🚧 | — | ✅ |
| Release optimizations | 🚧 | L | — |
| NativeAOT + trimming | 🚧 | L | — |
| Reasonable startup / warm JIT | 🚧 | M | — |
| Actionable emit/runtime errors (GCLR05xx/07xx) | 🟡 | S | 🟡 |

## Two definitions of done

- **(A) MVP per §40** — Echo+goja running on dotnet, /health, /eval, recover, 100
  concurrent, UTF-8, basic goja. Needs everything marked ✅ MVP. Rough order:
  **~6–9 engineer-months**, dominated by emit (XL), net/http+Kestrel (XL),
  goja/typedarrays (L), reflect (L).
- **(B) Polished product** — AOT, release opt, debug/PDB, full test harness,
  benchmarks, CI, broad overlay: **+3–4 months** on top of (A).

Shortest path to first demonstrable value: **M0** (emit `println` end-to-end) →
conformance runner → language features (M1) → big overlays. The `emit` backend is
the bottleneck: nothing else is verifiable until a `.dll` runs under `dotnet`.
