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
| `s[i].field = v` and `&s[i]` for a `*[N]T` (pointer-to-array auto-deref) | ✅ | S | ✅ |
| Reflection lowering + struct-tag descriptors (read + write path) | ✅ | L | ✅ |
| **reflect runtime type descriptors** — precise kind/name/string/fields, MapOf/SliceOf/PtrTo, Implements/AssignableTo, Zero/New (static + dynamic); see [REFLECT.md](REFLECT.md) | ✅ | L | ✅ |
| Cross-package function values (`pkg.Func` as a value); promoted shim-type methods | ✅ | S | ✅ |
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

## 4. Stdlib overlay (C# shim mechanism live; 199 conformance fixtures byte-exact; P0/P1/P2/P3/P4 hardened, typed-box + goja + Gin + Echo running)

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
| goja | — runs a large JS subset (see goja status) | ✅ | L | ✅ |
| regexp2 | goclr-safe overlay (unsafe→encoding/binary) | ✅ | M | ✅ |
| x/sys/unix | dropped via go-isatty/sha3 overlays | ✅ | M | ✅ |
| Gin v1.10.1 | runs end to end — router/middleware/binding/render + full CRUD over `database/sql` + the pure-Go SQLite engine | ✅ | L | ✅ |
| Echo v4 | runs — router, path params, JSON, status codes serve on the CLR; `crypto/x509`+`acme`/`autocert` closure lowers (TLS path a no-op shim) | ✅ | L | ✅ |
| KrakenD / Lura | measured — no goclr language gap is the blocker; the walls are third-party native deps (quic-go HTTP/3, Go `plugin`); fixed the two real goclr gaps it surfaced (type aliases, `regexp.FindAllStringSubmatch`) | 🟡 | XL | — |
| ~200 stdlib pkgs in closure | overlay or direct compile | 🟡 | L | ✅ |

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
| 199 conformance fixtures (000–400), all byte-exact vs `go run` (200 total, 1 skipped) | ✅ | M | ✅ |
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

## reflect — runtime type descriptors (2026-06)

`reflect` is now driven by compile-time **type descriptors** rather than runtime
samples — the foundation reflection-heavy libraries (encoding/json, validator, ORMs)
need. `Kind`/`Name`/`String`/`NumField`/`Field`/`Elem`/`Key` are precise (including
sized-integer kinds and struct field types/tags), for both the static path
(`reflect.TypeOf(concreteValue)`) and the dynamic path (`reflect.TypeOf(interface{})`,
recovered from the value's identity). Type construction (`MapOf`/`SliceOf`/`PtrTo`/
`ArrayOf`), the method set (`NumMethod`/`Method`/`Implements`/`AssignableTo`/
`ConvertibleTo`), and `Zero`/`New`/`MakeSlice`/`MakeMap` are descriptor-backed.
Verified byte-identical to `go run` (conformance 375–378). Full design and the one
remaining limit (a *bare* unnamed sized scalar reflected only dynamically) in
[REFLECT.md](REFLECT.md). Tag `0.0.29.reflect-complete`.

## gin end-to-end status (2026-06)

Gin (pinned to **v1.10.1**; v1.12+ pulls an HTTP/3 / BSON tree) is the second
web-framework validation target after the goja work. The compiler builds with the
`purego` and `nomsgpack` tags (no-assembly/no-unsafe code paths; drops the MessagePack
`ugorji/go/codec` dependency), and a handful of project overlays cut the genuinely
unlowerable third-party code at gin's own binding/render seams (go-toml, protobuf,
encoding/xml) and replace unsafe in dependency libraries (validator, sha3, fasttemplate,
bytesconv).

Driving the build surfaced — and closed — a broad swath of **Go-stdlib coverage** (all
shims are stdlib; the compiler stays third-party-agnostic): `net` address parsing and
`net.IP` methods, `net/mail`, `net/textproto`, `io/fs.FileMode`, `sync/atomic.Value`,
`time.ParseDuration`/`ParseInLocation`/`LoadLocation`, `encoding/base64.Encoding`
length/Encode/Decode, `crypto/sha3` (+SHAKE), `crypto/subtle.XORBytes`, `bytes.Runes`/
`IndexRune`/`IndexAny`/`Trim`, `http.Header` (live response headers) + form/header/query
binding, `url.URL.Query`/`RequestURI`, and several general lowering fixes (cross-package
function values, parenthesized inc/dec, `*[N]T` element field-write and address-of, a
shim-type method promoted from an embedded field). gin compiles through the
`go-playground/validator` (which exercises `reflect` heavily), `yaml.v3`, and all of
gin's form/header/query/JSON binding and rendering.

**Status: gin runs end to end.** `examples/demo_gin` (router) and
`examples/demo_gin_sql` (a Gin REST API over `database/sql` + the pure-Go zero-cgo
SQLite engine `go-r2-sqlite`, full CRUD byte-accurate) both serve correct responses
on the CLR. The server runtime bridges gin's `*Engine` across goclr's static-dispatch
boundary via a per-`http.Handler` ServeHTTP adapter (`Http.RegisterHandler`), driven
by an `HttpListener` loop. The HTTP/1 path is fully exercised; HTTP/2 (`x/net/http2`)
is not on the served path.

## Echo end-to-end status (2026-06)

Echo v4 **runs** on the CLR (`examples/demo_echo`): `/health`→`ok`,
`/ping`→`{"message":"pong"}`, `/hello/:name`→`{"hello":"x"}` (path params),
`/missing`→404 with Echo's own JSON body. The whole framework lowers — including its
`crypto/x509` + `acme`/`autocert` TLS closure (the TLS path is an honest no-op shim;
plain HTTP/JSON is fully exercised) — with **no overlays** (real echo/acme Go
compiled). Reaching it needed two compiler fixes (typing an opaque-shim field setter
from the field's Go type so `http.Server.Handler = e` boxes correctly; removing
`net.Listener` from the shim *method* registry so it dispatches as the interface it
is) and a serving bridge that releases echo's own bound port to the `HttpListener`.

## KrakenD / Lura distance (2026-06)

Measured by compiling KrakenD's core framework, **Lura**
(`github.com/luraproject/lura/v2`), through goclr: **no goclr language or compiler gap
is the blocker.** Every wall is a third-party *native* dependency (overlay / build-tag
territory): `go-playground/validator` unsafe (overlay exists), go-toml/v2's
`SubsliceOffset` (now compiles directly — see the `reflect.SliceHeader` offset views),
quic-go HTTP/3 (raw sockets + asm + unsafe — must be cut, as gin pinned to v1.10.1
avoids), `x/net` asm, and Go `plugin` (external plugins, unportable to .NET; the core
CE runtime runs without them). The probe surfaced two real goclr gaps, both fixed:
`regexp.(*Regexp).FindAllStringSubmatch` and Go 1.22+ type aliases (`types.Unalias` in
the type lowering). So the distance to KrakenD is an overlay/build-tag campaign on
Lura's deps plus the plugin limit — not compiler work.

## GORM distance (2026-06)

Measured by compiling `gorm.io/gorm/schema.Parse` (the reflect-heavy core that turns a
tagged struct into a table/column schema) through goclr. **No goclr language/compiler gap
blocks it** — the walls are a chain of small stdlib/dependency method gaps, each a shim:
fixed so far are `time.Time.Date`/`Clock`/`AddDate` (via jinzhu/now) and the
`runtime.Callers`/`CallersFrames`/`(*Frames).Next`/`runtime.Frame` caller-location
machinery (stubbed — goclr has no Go stack metadata, so gorm logs SQL without a
`file:line`, which gorm tolerates). The next wall is gorm's `log/slog` handler wrapper
(`slog.Handler.Enabled`/`Handle`/`WithAttrs`). Beyond schema parsing, full ORM operations
also need a **pure-Go dialector/driver** (the cgo-free `glebarez/sqlite` or a goclr port of
the existing `go-r2-sqlite`). So GORM is a multi-step shim/overlay campaign — not a single
compiler gap — and is left as a staged target; the generally-useful shims it surfaced
(time multi-return methods, the runtime caller stubs) are landed independently.

## Performance & AOT distance (2026-06)

Measured, not yet engineered — the levers and their distance:

- **Startup is already good for typical programs**: a hello-world goclr `.dll` starts in
  ~20 ms. A *large* assembly is JIT-bound: goja (~15 MB) takes ~3.2 s to first output,
  almost entirely first-run JIT of its method set. Tiered compilation (quick-JIT-first) is
  already on by default, so config tuning yields little here.
- **ReadyToRun (crossgen)** is the realistic lever for large-program startup: precompiling
  the app + `GoCLR.Runtime`/`GoCLR.Stdlib` to native via `dotnet publish
  -p:PublishReadyToRun=true` would cut goja's cold JIT. It needs a generated publish project
  (the current output is loose framework-dependent dlls) — a packaging task, not a compiler
  change. This is the highest-value next perf step.
- **NativeAOT is infeasible without rework.** The shim runtime is reflection-heavy by
  design — `Closures.InvokeShim` (`MethodInfo.Invoke`), the `[GoShim]` attribute scan
  (`GoShim.cs`), `reflect`'s `Value_FieldByName`/`TypeReg`, the callback bridge — all of
  which NativeAOT's trimming removes or can't invoke. AOT would require routing the shim
  surface through source-generated, statically-rooted dispatch (no `MethodInfo.Invoke`,
  no attribute scanning). Large; tracked, not started.
- **Throughput** is bounded by the object-boxed value model (every `any`/interface/slice
  element is a boxed `object`). Typed IL / specialized slices (roadmap "Typed IL, no mass
  boxing") is the lever — a substantial backend change, also the prerequisite that makes the
  emitted code more AOT/trim-friendly.

The emitted assembly already links against Release-built runtime/stdlib; the
runtimeconfig carries a `configProperties` block as the place to tune host options.

## Fiber distance (2026-06)

Measured by compiling a minimal `gofiber/fiber/v2` app. Fiber is built on **fasthttp** (its
own HTTP stack, not net/http), so the distance is a fasthttp campaign, not a quick target.
Fiber's own packages compile after closing generally-useful gaps: the `testing` overlay is
now applied to ALL builds (fiber's `utils` imports `testing` in non-test code for a `TB`-based
assert helper), `os.Args` is shimmed (and shimmed value-typed vars now unbox correctly — a
general fix), and `text/tabwriter` compiles from source (fiber's assert helper formats with
it; it is dead code when serving). The wall is fasthttp's dependency tree: `andybalholm/brotli`
(a compression dep) hits a goclr lowering gap — **nested field assignment through a slice
element** (`nodes[pos].u.shortcut = …`, i.e. `s[i].a.b = v`) — and fasthttp itself is
unsafe-heavy (its own buffer/socket code). So supporting Fiber means: (1) the `s[i].a.b = v`
lowering, (2) working through brotli/gzip compression, (3) fasthttp's unsafe (overlay or the
safe build tag where it offers one). A staged target, like gin's x/net/http2 was.
