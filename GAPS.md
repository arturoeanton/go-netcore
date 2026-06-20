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
| Basic types + mapping (int/int32/int64/uint*/float*/bool/string/byte/rune) | ✅ | S | ✅ |
| Package init order + init() + globals (static fields, __goclr_init) | ✅ | M | ✅ |
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
| Generics: same- AND cross-package instantiation + explicit type args (Fn[T]) | ✅ | L | ✅ |
| Embedded-struct promotion (field + method, value/pointer embeds, multi-level) | ✅ | M | ✅ |
| Go 1.22 per-iteration loop variables (for + range, closure capture) | ✅ | M | ✅ |
| Multi-result function values (closures) + f(g()) multi-result spread | ✅ | M | ✅ |
| Bound method values (f := recv.M); copy builtin; elided-ptr literals | ✅ | M | ✅ |
| Sub-word integer overflow wraps (int8/16, uint8/16) | ✅ | M | ✅ |
| fmt Stringer/Error dispatch (struct + pointer types) | ✅ | M | ✅ |
| Struct/array value equality (==) + array value semantics (copy on assign) | ✅ | M | ✅ |
| `clear` builtin; `&slice[i]`; `&^`/`&^=`; keyed/fixed-array literals; errors.As | ✅ | S | ✅ |
| Reflection lowering + struct-tag descriptors (read + write path) | ✅ | L | ✅ |
| Multi-package lowering + globals + init() + C# shim/extern mechanism | ✅ | XL | ✅ |

## 3. .NET runtime (`GoCLR.Runtime`)

| Piece | State | Effort | MVP? |
|---|---|---|---|
| GoString/Slice/Map/Ptr/Interface/panic/defer/goroutine/channel/error | ✅ | — | ✅ |
| sync: Mutex/RWMutex/Once/WaitGroup/Map (Pool/Cond pending) | 🟡 | M | ✅ |
| sync/atomic | 🚧 | S | ✅ |
| complex64/128 ✅; GoArray, Bytes helpers | 🟡 | S | 🟡 |
| reflect runtime (read-path + settable write-path: Set*/Field/New) | ✅ | L | ✅ |
| Time (Duration + time.Time/Format), Console/GoFunc/struct value helpers | 🟡 | M | ✅ |
| select runtime, ASCII fast-path, intern pool | 🚧 | M | 🟡 |

## 4. Stdlib overlay (C# shim mechanism live; 168 conformance fixtures byte-exact; P0/P1/P2 hardened, typed-box + goja running)

| Package(s) | State | Effort | MVP? |
|---|---|---|---|
| errors/fmt/strconv/strings/bytes/sort/math/math-bits (float ftoa Go-exact) | ✅ | M | ✅ |
| strings.Builder / bytes.Buffer / io.WriteString / fmt.Fprint* | ✅ | M | ✅ |
| context (Background/WithValue/WithCancel/WithTimeout) ✅; io ifaces/bufio 🚧 | 🟡 | M | ✅ |
| encoding/json — Marshal + Unmarshal (descriptor-driven write-path) | ✅ | L | ✅ |
| net/http client + server (HttpListener) ✅ | ✅ | XL | ✅ |
| net/url (escapes + Parse) ✅; mime, mime/multipart 🚧 | 🟡 | M | ✅ |
| regexp (.NET Regex; common RE2 patterns) | 🟡 | L | ✅ |
| unicode/utf8 ✅; utf16 | 🟡 | S | ✅ |
| reflect (read-path + settable write-path: Set*/Field/New) | ✅ | L | ✅ |
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
| 168 conformance fixtures (000–368), all byte-exact vs `go run` | ✅ | M | ✅ |
| Backend unit tests (emit PE/determinism/fat-header, lower, linker) | ✅ | S | ✅ |
| Echo integration tests | 🚧 | M | ✅ |
| goja integration tests | 🚧 | M | ✅ |
| Echo+goja + 100 concurrent | 🚧 | M | ✅ |
| Benchmarks | 🚧 | M | — |
| CI (.github/workflows: lint+vet+test+conformance) | ✅ | S | — |

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

## goja end-to-end status (2026-06)

`goja` now **compiles** to a ~15 MB ECMA-335 `.dll`, the assembly **loads and JITs
cleanly** (ilverify-clean `Main`), and package **init runs** through most of x/text.
Reaching this required fixing a long series of codegen-correctness bugs (all with
conformance fixtures): the **4-byte metadata heap-index keystone** (heaps exceed
64 KiB in a program this large), generic instantiations introduced by package-var
initializers being left with empty bodies, the trailing `ret` of a non-void
panic-terminated function, the fat-header `InitLocals` flag, variadic interface
dispatch, **methods promoted from an embedded concrete field** in interface
dispatch, `nil` assigned to a GoSlice/GoMap struct field, and fixed-array struct
fields zeroing to a nil slice. Shim signatures were matched to the Go-derived
externs (atomic Int32 ops, reflect, time, runtime).

Remaining known blockers on the `RunString("1+2")` path (x/text locale init), in
order encountered:

1. **Identical-layout named-struct conversion through a pointer.** `type Tag
   compact.Tag` makes `language.Tag` and `compact.Tag` distinct CLR structs (correct
   for interface dispatch), but Go converts between them as a no-op
   (`(*compact.Tag)(t)`). goclr emits no field reinterpretation, so the method call
   does `unbox.any compact.Tag` on a boxed `language.Tag` → `InvalidCastException`.
   Fix: the struct→struct / *struct→*struct conversion must copy/reinterpret fields
   (or re-box) when source and target share a layout but differ in CLR type.
2. **Struct value-semantics for fixed-array fields on copy.** Returning a struct by
   value should clone its fixed-array field; goclr keeps the backing shared (a slice
   field aliasing the array still observes the original after the copy). Edge case,
   surfaced by the scanner repro (conformance 358 covers the in-function path).

These are the next x/text-init items before the JS evaluator itself is exercised.

## goja runs JavaScript (2026-06)

goja now **evaluates JavaScript** through goclr: `vm.RunString` returns correct
results for arithmetic (`1+2*3` → 7), string concatenation (`"a"+"b"` → "ab"), and
function calls (`(function(x){return x*x})(9)` → 81). See `examples/demo_goja`.
This required, beyond the compile-path fixes above, several runtime-correctness
fixes (each with a conformance fixture): identical-layout named-struct conversion
(`type Tag compact.Tag`), a pointer-receiver method promoted from an embedded value
field mutating through a pointer, `*p = v` boxing a value type into an interface
cell, `a, b = f()` keeping a concrete result boxed for an `interface{}` target, and
multi-result `return s, nil` boxing a value-type nil as `NilSlice`.

### Update — much-expanded working set

Two further runtime fixes (each with a conformance fixture) unblocked a large swath:

- A named value stored into an interface-element slice (`code[pc] = jne(target)`,
  `code []instruction`, `jne` a named int32) kept its typed-box identity, so
  interface dispatch on the element matches (conformance 364). This was the SHARED
  root cause of both loops AND arrays: the compiler backpatches jump instructions
  into the bytecode this way.
- A slice's capacity region (`s[len:cap]`) now holds the element zero value for both
  `make(cap)` and append-grown backings (conformance 365), instead of nulls — goja
  reads a sentinel in the last cap slot.

With these, goja evaluates a **large** JavaScript subset on the CLR: arithmetic,
strings and string methods (`toUpperCase`/`slice`), `Math`, objects and property
access, function calls/closures, and `for`/`while` loops (see `examples/demo_goja`).

### Update — the remaining frontier is closed (array callbacks + JSON)

The three frontier items now evaluate byte-identically to `go run` (each with a
conformance fixture):

1. **Array callbacks** — `[].map`/`filter`/`reduce`/`sort(comparator)` work. Root
   cause: a field-alias `&a.prop` GoPtr carried no type id, so goja's
   `prop.(*valueProperty)` assertion failed (a typed nil). Field aliases now tag the
   pointee struct's type id (`Rt.FieldPtr(getter, setter, typeId)`, conformance 366).
2. **`JSON.stringify`** — objects, nested arrays, round-trips. Root cause: a type
   switch `case String:` matched `*Object` because `isinst object` matches every
   reference; the match now tests interface satisfaction (conformance 367).
3. **`JSON.parse`** — nested objects/arrays. Root cause: `tok.(json.Delim)` (both
   comma-ok and single-value) failed for the typed-box `json.Delim` — the assertion
   used `isinst` on the int32 representation and never matched the `GoNamed` wrapper.
   Type assertion to a named non-struct type now matches the wrapper id
   (conformance 368).

`examples/demo_goja` exercises all of these. The one true remaining representation
gap (orthogonal to goja's evaluator, no longer on the goja path): a typed-nil pointer
stored in an interface compares `== nil` true where Go yields false. Tracked in
LIMITATIONS.md.

`GOCLR_PANIC_TRACE=1` makes the runtime print a panic's throw-site .NET stack — the
key tool for locating these (a `recover()` otherwise masks the origin).
