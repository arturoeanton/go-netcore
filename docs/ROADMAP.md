# goclr roadmap

`goclr` is a large compiler. This roadmap tracks the MVP milestones from the
spec (§38) and what is implemented in this repository today.

Legend: ✅ done · 🟡 partial · 🚧 not started

## Current state (this commit)

| Milestone | Item | State |
| --- | --- | --- |
| — | Module scaffold + directory layout | ✅ |
| — | Diagnostics model (`GCLRxxxx`, severities, positions, human + JSON) | ✅ |
| — | Frontend loader (`go/packages`, types, syntax, build tags, `CGO_ENABLED=0`) | ✅ |
| — | Analysis: cgo rejection (`GCLR0100`) | ✅ |
| — | Analysis: Go assembly / native source rejection (`GCLR0200`) | ✅ |
| — | Analysis: unsafe classification (approved vs blocking, `GCLR0201`) | ✅ |
| — | Analysis: stdlib overlay map + coverage summary | ✅ |
| — | `echo-goja` compatibility profile + report (human + JSON) | ✅ |
| — | CLI: `analyze` | ✅ |
| — | CLI: `doctor` | ✅ |
| — | CLI: `clean` | ✅ |
| — | CLI: `build` / `run` (emit real DLL for M0 subset) | 🟡 |
| — | CLI: `test` (front half + honest backend gate) | 🟡 |
| M0 | IR + lowering (println/print subset) | ✅ |
| M0 | ECMA-335 emitter (Go-native managed PE, runs on dotnet) | ✅ |
| M0 | linker (runtimeconfig.json + runtime copy) | ✅ |
| M0 | conformance runner (go vs goclr) | ✅ |
| M1 | general emitter (multi-method, typed sigs, locals, branches, box, calls) | ✅ |
| M1 | AST→IR lowering: int/bool, arithmetic, if/for/switch, funcs, recursion | ✅ |
| M1 | runtime strings (GoString): len/index/concat/compare/range, valuetype sigs | ✅ |
| M1 | struct value types: TypeDef+Field tables, composite literals, field access, value semantics | ✅ |
| M1 | slices (object[]-backed GoSlice): make/append/index/range/sub-slice, []byte/[]rune | ✅ |
| M1 | maps (Dictionary-backed GoMap): make/literal/index/comma-ok/delete/len/range | ✅ |
| M1 | pointers (GoPtr cell model): &x/*p, &T{}, new, ptr-to-struct fields, nil, aliasing | ✅ |
| M1 | methods (value + pointer receivers), all call combinations, auto-address | ✅ |
| M1 | multiple return values + multiple/parallel assignment (object[] tuples) | ✅ |
| M2 | empty interface `any` + type assertions + type switch (isinst/unbox) | ✅ |
| M2 | named interfaces + `error` + method dispatch (isinst over implementers) | ✅ |
| M2 | defer / panic / recover (runtime defer stack — defer at any nesting) | ✅ |
| M2 | closures / function values (lambda-lift + GoClosure + dispatcher), by-ref capture | ✅ |
| M2 | variadic functions (`f(args ...T)`, spread `args...`) | ✅ |
| M2 | numeric types: float64/float32, uint64/uint32 (arith/compare/convert/box) | ✅ |
| M2 | goto / labels / labeled break-continue / fallthrough / range-over-int | ✅ |
| M2 | goroutines + channels (send/recv/range/close/comma-ok) + select | ✅ |
| M2 | generics: functions AND types/methods (monomorphization) | ✅ |
| M2 | named return values + deferred-recover-to-named-error idiom | ✅ |
| M2 | string/complex/unsigned compound assignment (`+=` etc.) | ✅ |
| M2 | closures capturing & mutating struct locals; deferred func literals | ✅ |
| M2 | pointer-receiver interface implementers (GoPtr type-id tag + dispatch) | ✅ |
| M2 | ++/--/op= on struct fields, slice/map elements, pointer derefs | ✅ |
| M2 | anonymous struct types (structural registration) | ✅ |
| M2 | complex128 / complex64 (GoComplex runtime + complex/real/imag + arithmetic) | ✅ |
| — | .NET runtime: GoString (UTF-8), slices, maps, pointers | ✅ |
| — | .NET runtime: interfaces, type descriptors, method tables | ✅ |
| — | .NET runtime: defer / panic / recover, goroutines, channels, errors | ✅ |
| M2.5 | Multi-package lowering (main + transitive non-stdlib closure → 1 assembly) | ✅ |
| M2.5 | Package globals (static fields) + `init()` + var initializers (`__goclr_init`) | ✅ |
| M2.5 | C# shim / extern-ref mechanism + `GoCLR.Stdlib.dll` (2nd managed assembly) | ✅ |
| M2.5 | Opaque value-type shims, shim variables, native (runtime) function values | ✅ |
| M2.5 | 20 P0 stdlib packages byte-exact (see M2.5 section) | ✅ |
| M2.5 | reflect read + settable write path; compiler-emitted type/tag descriptors | ✅ |
| M2.5 | Go-exact float formatting (shortest ftoa: %v/%g/%e/println/strconv) | ✅ |
| M2.5 | encoding/json Marshal + Unmarshal (descriptor-driven) | ✅ |
| M2.5 | Embedded-struct promotion: field read/write/op-assign + value & pointer-receiver methods, value & pointer embeds, multi-level | ✅ |
| M2.5 | Go 1.22 per-iteration loop variables (`for` and `range`, captured by closures) | ✅ |
| M2.5 | Cross-package generic instantiation (a dependency's/subpackage's generic, incl. `S ~[]E` constraint-derived params) | ✅ |
| M2.5 | Stdlib classification by module (modules whose path has no dot now lower their subpackages) | ✅ |
| M2.5 | `unicode` + `sort` compiled from goclr source overlays; `&slice[i]`; `&^`/`&^=`; keyed array/slice literals; fixed `[N]T` arrays; `clear` builtin; `errors.As` | ✅ |
| M2.5 | Long-form local opcodes (256+ locals) + chunked package-var init (64 KB IL limit) | ✅ |
| M3 | Typed box: `TypeId` on every boxed named value; identity preserved across slices/interfaces; interface dispatch over representation-sharing types | ✅ |
| M3 | Sample-based `reflect` overlay covering goja's interop surface | ✅ |
| M3 | Large-program emitter: 4-byte metadata heap indices (`HeapSizes=0x07`), `InitLocals`, fat headers — required by goja-scale assemblies | ✅ |
| M3 | Slice capacity semantics (`make`/`append` cap region holds element zeros); identical-layout struct conversion; promoted pointer-receiver method mutation through a pointer | ✅ |
| M3 | **goja: compiles → loads → JITs → runs init → evaluates a large JS subset** (arithmetic, strings + methods, `Math`, objects, closures, `for`/`while`) | ✅ |
| M3 | goja: array callbacks (`map`/`filter`/`reduce`/`sort`), `JSON.stringify`/`parse` | ✅ |
| M3 | interface method-callback bridge (`container/heap` incl. named-slice impl, `io.Writer`, `io/fs.Stat`) | ✅ |
| M3 | `unsafe.Pointer` safe idioms — `string↔[]byte` reinterprets + read-only `reflect.*Header` offset views (go-toml `SubsliceOffset`); pointer-arith / header writes rejected | ✅ |
| M5 | **Echo v4 runs** — router, path params, JSON, status codes on the CLR; `crypto/x509`+`acme`/`autocert` closure lowers (TLS path a no-op shim) | ✅ |
| M3 | `goclr test` (real `testing.T` on the CLR — TestXxx, subtests, Fatal/Skip) | ✅ |
| M3 | CI conformance matrix (per-fixture pass/skip/fail in the job summary) | ✅ |
| M3 | stable compatibility report — `goclr analyze --html` / `--json` (package-by-package, summary counts + stdlib coverage) | ✅ |

**199 conformance fixtures pass byte-for-byte vs `go run`** (200 total, one skipped).
Recent tags: `0.0.21.goja-compiles-loads-jits` → `0.0.24.goja-loops-arrays-objects`,
`0.0.27.goja-json-array-callbacks` (goja JSON + array callbacks),
`0.0.28.reflect-type-descriptors` → `0.0.29.reflect-complete` (reflect descriptors),
`0.0.36.phase4-echo` (Echo serves on the CLR), `0.0.37` → `0.0.43` (shim-signature
validator, interface method-callback bridge + `container/heap`, type-id unification),
`0.0.44.lura-alias-regexp` (type aliases, `regexp.FindAllStringSubmatch`),
`0.0.45.unsafe-string-slice` → `0.0.46.reflect-header-offset` (the `unsafe.Pointer`
safe idioms).

**reflect — runtime type descriptors.** `reflect` is now descriptor-backed (precise
kind/name/string/fields/tags, `MapOf`/`SliceOf`/`PtrTo`, `Implements`/`AssignableTo`,
`Zero`/`New`; static + dynamic paths), the foundation reflection-heavy libraries need.
See [REFLECT.md](REFLECT.md).

**Validation targets.** goja evaluates a large JS subset (arithmetic, strings, Math,
objects, closures, loops, array callbacks, `JSON.stringify`/`parse`) byte-identical to
`go run`. **Gin runs end to end** — `examples/demo_gin_sql` is a Gin REST API over
`database/sql` and the pure-Go zero-cgo SQLite engine `go-r2-sqlite`, the whole stack
compiled to IL (full CRUD, byte-accurate). **Echo now runs too** — `examples/demo_echo`
serves plain HTTP/JSON (router, path params, status codes) with its entire
`crypto/x509` + `acme`/`autocert` TLS closure lowered to IL (the TLS path is a no-op
shim; plain HTTP is fully exercised).

## Checklist (done / pending)

Tracks what is implemented vs outstanding. Done items are verified byte-exact vs
`go run` (conformance/validation) unless noted.

### Compiler & runtime
- [x] Multi-package lowering, globals + `init`, generics (monomorphized), goroutines/channels/`select`, `defer`/`panic`/`recover`, closures, complex
- [x] Embedded struct field/method promotion (value + pointer), per-iteration loop vars, fixed arrays + keyed literals, `&slice[i]` / `&s.field`, identical-layout struct conversion
- [x] Typed box (named non-struct identity through interfaces) + large-program emitter (4-byte heaps, `InitLocals`, fat headers)
- [x] `reflect` runtime type descriptors — read + write paths (`Kind`/`Name`/`String`/fields/tags, `Set*`, `MapOf`/`SliceOf`/`PtrTo`, `Implements`/`AssignableTo`)
- [x] Pointer-to-non-struct type discrimination (`*int64` vs `*string` vs `*[]byte`)
- [x] General, agnostic shim-type-in-interface dispatch (`[GoShim]` registry + `types.Implements`; no Go type hardcoded)
- [x] Deep `reflect` (minimal): `Value.Call`, `MakeFunc` (build a callable from
  `func([]Value)[]Value`), `Value.Method`/`MethodByName(...).Call` for the program's own
  types — fixture 407. Remaining: reflecting methods across a large dependency closure
- [ ] Per-value runtime type tags / itable — two named-slice (or named-map) implementers of one interface
- [x] Typed-nil pointer kept distinct inside an interface (`var p *T; any(p) == nil` ⇒ false;
  the `err != nil` gotcha is faithful). Residual: the recovered pointer's own `== nil` — see LIMITATIONS
- [x] Uncaught-panic output in Go's shape: `panic: <value>` + `goroutine 1 [running]:` header
  + exit status 2 (a synthetic entry wrapper → `Rt.FatalPanic`; frames are the CLR stack, see LIMITATIONS)
- [ ] `%T`/`%#v` precise element types for dynamically-reached slices/maps; nil-map `%v` → `map[]`

### Standard library
- [x] **P0** fmt/strconv/strings/bytes/unicode/utf8/sort/math(+bits/big)/errors/reflect/encoding-json/time/sync/math-rand/context/io/os
- [x] **P1** net/http client+server, net TCP, crypto (sha/sha3/md5/hmac/rand/subtle), regexp, path/filepath, net/url, bufio, log, container/list, os/exec, mime, net/mail, net/textproto, io/fs, flag
- [x] **P2** encoding (csv/hex/base64/base32/binary), compress (gzip/zlib/flate), crypto/aes-GCM, hash family
- [x] **P3** net/http/httptest + cookiejar, net UDP, log/slog, os/signal (real SIGINT/TERM), `database/sql` + `database/sql/driver` (+ the `go-r2-sqlite` engine compiled through goclr), mime/multipart
- [x] **P4** `crypto/x509` + `acme`/`autocert` closure (lowered for Echo; TLS path a no-op shim), `container/heap` (via the interface method-callback bridge, incl. the named-slice implementer), `encoding/xml` (real reflection Marshal/Encode + token API), `encoding/pem`/asn1, `reflect.SliceHeader`/`StringHeader` read-only offset views
- [ ] `container/ring`, `encoding/gob`, `text/template` / `html/template`
- [ ] `crypto/rsa·ecdsa·x509·tls` (full key/cert surface), `net/smtp`, `archive/zip·tar`, `runtime/debug`, `text/tabwriter` / `text/scanner`
- [ ] `golang.org/x/sync/errgroup` (needs the external module to type-check), `google/uuid`

### Validation targets
- [x] **goja** — large JS subset, byte-identical to `go run`
- [x] **Gin** — router/middleware/binding/render + full CRUD over `database/sql` + SQLite
- [x] **Echo** — compiles + serves plain HTTP/JSON on the CLR (router, path params, status
  codes); the `crypto/x509` + `acme`/`autocert` TLS closure lowers (TLS path is a no-op
  shim, plain HTTP fully exercised) — see `examples/demo_echo`

### Tooling & milestones
- [x] `goclr test` — compiles a package's tests (via the real-Go `testing` overlay) to a
  .NET assembly and runs them: `TestXxx(t *testing.T)`, subtests, `Error`/`Errorf`/`Fatal`/
  `Fatalf`/`Fail`/`FailNow`/`Log`/`Logf`/`Skip`/`Cleanup`, go-test-like report + exit code
  (`tests/gotest`). Benchmarks/fuzzing/examples/`TestMain`/`-flags` not run (see LIMITATIONS)
- [ ] CI conformance matrix published (visible per-fixture status)
- [ ] Stable compatibility report (HTML/JSON) from `analyze`
- [ ] Per-function coverage matrix (covered / stub / blocked), checked in CI
- [ ] AOT / performance pass (P4)
- [ ] Third-party ecosystem via overlay/compile-direct (GORM, redis, testify, gRPC, JWT, websocket)

## Milestones

### M0 — CLI + hello world ✅
- ✅ CLI surface and dispatch
- ✅ minimal IR (`internal/goir`) + lowering of the println/print subset (`internal/lower`)
- ✅ Go-native ECMA-335 emitter (`internal/emit`): real managed PE that `dotnet` runs
- ✅ linker (`internal/linker`): runtimeconfig.json + copies GoCLR.Runtime.dll
- ✅ `goclr build`/`goclr run` produce and execute a `.dll`
- ✅ conformance runner: `goclr run` output matches `go run` (hello, print, empty main, unicode/UTF-8, fat-header body)
- ✅ backend unit tests: emitter (debug/pe validity, determinism, tiny/fat header), lowering (subset + rejections), linker bundle
- ✅ on-demand runtime build from a clean checkout produces clean output

**M0 is closed.**

### M1 — basic language ✅ CLOSED
Done this increment (lowered to typed CIL, verified `goclr run` == `go run`):
- ✅ functions (int/int32/bool params + single result), recursion, nested calls
- ✅ variables, constants, numeric conversions
- ✅ arithmetic, comparison, logical (short-circuit `&&`/`||`), bitwise
- ✅ if / else / else-if
- ✅ for (all three forms) + break + continue
- ✅ switch (tag and tagless) — no fallthrough yet
- ✅ println/print of typed args (boxing int/bool)
- ✅ conformance fixtures: int_add, if_else, for_sum, switch, bool, fib

String increment (done, verified `goclr run` == `go run`):
- ✅ runtime strings on the GoString value type (not System.String)
- ✅ `len(s)` (bytes), `s[i]` (byte), `a + b`, string comparisons
- ✅ range-over-string (UTF-8 runes + byte index), incl. unicode
- ✅ string params/returns; fixtures str_basic, str_index, str_range, str_func

Struct increment (done, verified `goclr run` == `go run`):
- ✅ struct value types (CLR ValueType TypeDef + Field table), Go value semantics
- ✅ composite literals (keyed and positional), zero value (`var p T`)
- ✅ field read/write, nested structs + nested field write
- ✅ struct params (by value), struct returns; fixtures struct_basic/value/func/nested

Slice increment (done, verified `goclr run` == `go run`):
- ✅ slices on a non-generic object[]-backed GoSlice (boxed elements; perf TODO: specialize)
- ✅ make([]T, n[, c]), slice literals, index get/set, len/cap, append, sub-slice s[lo:hi]
- ✅ range over slices, []byte(s) / []rune(s) conversions
- ✅ slice params/returns; fixtures slice_basic/append/literal/bytes

Map increment (done, verified `goclr run` == `go run`):
- ✅ maps on a non-generic Dictionary-backed GoMap (reference type, boxed key/value)
- ✅ make, map literals, m[k] read/write, `v, ok := m[k]`, delete, len
- ✅ range-over-map, nil map; fixtures map_basic/commaok/range

Pointer increment (done, verified `goclr run` == `go run`):
- ✅ pointers via the GoPtr cell model (address-taken locals escape to a cell)
- ✅ &x, *p read/write, &T{...}, new(T), pointer-to-struct field access, nil, ptr ==/!=
- ✅ correct aliasing (writes through a pointer are observed by the variable)
- ✅ fixtures ptr_basic/ptr_struct/ptr_new_nil

Method increment (done, verified `goclr run` == `go run`):
- ✅ methods as static Type_Method with the receiver as the first parameter
- ✅ value + pointer receivers; all four call combinations (value/ptr × value/ptr base)
- ✅ implicit address-of for pointer-receiver calls on a value (auto-cell)
- ✅ fixtures method_value/method_pointer/method_mixed

Multi-value increment (done, verified `goclr run` == `go run`):
- ✅ multi-return functions (`func f() (int, int)`) packed as an object[] tuple
- ✅ multi-assign from a call (`a, b := f()`), parallel assignment / swap (`a, b = b, a`)
- ✅ blank discards; fixtures multi_return/multi_swap/multi_mixed

**M1 is CLOSED.** Remaining basic-language gaps deferred to later milestones:
fallthrough, labeled break/continue, goto, untyped-const edge cases, interfaces
(needed for `error` and multi-return with error) — interfaces land in M2.

### M2 — critical runtime ✅ CLOSED
Done (verified `goclr run` == `go run`):
- ✅ empty interface `interface{}` / `any` → opaque object (auto-box on conversion)
- ✅ type assertion `x.(T)` (single + comma-ok `v, ok := x.(T)`) via isinst/unbox.any
- ✅ type switch `switch v := x.(type)` with per-case binding
- ✅ interface == / != nil; fixtures any_basic/any_commaok/type_switch

Named-interface increment (done, verified `goclr run` == `go run`):
- ✅ named interfaces (error, Stringer, custom) → object; method dispatch via isinst
  over value-receiver implementers (closed-world enumeration)
- ✅ `error` works: custom error types, `func f() (T, error)`, `err != nil`, `err.Error()`
- ✅ slices/maps of interfaces; type switch on a named interface
- ✅ fixtures error_basic/iface_shapes/iface_slice

Defer/panic/recover increment (done, verified `goclr run` == `go run`):
- ✅ panic / recover / defer via hand-emitted CIL exception-handling clauses (try/catch)
- ✅ defer runs in LIFO order on normal and panic paths; args evaluated at defer time
- ✅ recover() catches the panic; recovered functions return zero values (§40 #7)
- ✅ fixtures defer_order, panic_recover

Closure increment (done, verified `goclr run` == `go run`):
- ✅ function values + closures via lambda-lifting to static methods + a GoClosure
  {id, env} runtime value + a generated dispatcher (no generated classes needed)
- ✅ by-reference capture (captured locals become shared GoPtr cells in env)
- ✅ closures as args/returns/callbacks; fixtures closure_basic/capture/callback

M2 must deliver ~complete Go (the targets are real projects using echo+goja, not
just those two libs). Remaining LANGUAGE work — ALL required to close M2:
- ✅ variadic functions (`f(args ...T)`)
- ✅ numeric types: float64/float32, uint64/uint32 (arithmetic, unsigned/float
  compare, conversions) — float64 was a hard blocker for goja (JS numbers).
  NOTE: float *printing* via println doesn't match Go's format yet (fmt overlay will fix)
- ✅ goto, labeled statements, labeled break/continue, fallthrough, range-over-int
- ✅ goroutines (`go f()` + `go func(){}()`) + channels (buffered/unbuffered, send/recv,
  comma-ok, range, close, len/cap) + select (recv/send/default, blocking) — non-generic
  GoChan + monitor; goroutines reuse the closure dispatcher via a registered GoInvoker
- ✅ generics — both generic FUNCTIONS and generic TYPES/METHODS, monomorphized
  per concrete instantiation (substType incl. Named re-instantiation + per-call
  shell + worklist + struct dedup by mangled name). Covers constraint unions,
  `any`/`comparable`/`~int`, multiple params, func-typed params, slices/maps of
  params, methods on generic types, generic struct fields, nested instantiation.
- ✅ named return values + the deferred-recover-to-named-result idiom
  (`func f() (err error) { defer func(){ err = ... }() }`).
- ✅ defer at ANY nesting (loops, conditionals, nested blocks, inside closures)
  via a per-goroutine runtime defer stack (Mark/Push/Run); supports deferred
  named funcs, methods, func literals (with args), func values, and builtins
  (println/print/close).
- ✅ consolidation hardening (3 adversarial sweeps): fixed string/complex/unsigned
  `op=`, closures over generics, type-param method calls, pointer type
  switch/assert, struct-capturing closures, deferred func literals.
- ✅ pointer-receiver interface implementers — GoPtr carries a TypeId (pointee's
  named-struct id); dispatch enumerates value AND pointer-only implementers and
  matches pointers via isinst GoPtr + TypeId check.
- ✅ ++ / -- / compound op= (`+=` etc.) on struct fields, slice/map elements and
  pointer derefs (read-modify-write; previously ident-only).
- ✅ complex128 / complex64 — GoComplex {Re,Im} runtime + complex/real/imag
  builtins + arithmetic (incl. division), negation, ==/!=, zero value, `a+bi`
  literals. (println format is best-effort, like floats.)
- ✅ anonymous struct types (`var s struct{ x int }`) — structural registration.
- ✅ generic struct types + methods on generic types — monomorphized; distinct
  instantiations dedup by mangled name.

**M2 language is effectively complete** for the echo/goja target. The only known
gaps are a few stdlib-format niceties (float/complex println exact formatting,
handled once fmt is overlaid). Next: **M2.5 stdlib overlay** → M3 goja.

### M2.5 — stdlib overlay ✅ (P0 hardened; P1; P2; P3 incl. database/sql)
Delivery mechanism + P0 (hardened) + P1 (incl. net/http client+server, net TCP, crypto)
+ P2 (encoding/compress/aes) + P3 (net/http server, httptest/cookiejar, net UDP, slog,
os/signal, `database/sql` + the go-r2-sqlite engine) done — each verified byte-exact vs
`go run`. See the **done/pending checklist** above for the full breakdown, `LIMITATIONS.md`
for tracked gaps, and `GOJA-STRATEGY.md` for the goja/unsafe.Pointer plan (M3).

Foundations (the "how"):
- ✅ **multi-package lowering** — compiles main + its transitive non-stdlib
  dependency closure into one assembly (CLR-name prefixes per package; cross-package
  calls resolve through a global `*types.Func`→method map).
- ✅ **package-level vars + `init()`** — globals as static fields (ldsfld/stsfld);
  var initializers + `init()` run in a generated `__goclr_init` before `main`.
- ✅ **C# shim / extern-ref mechanism** — dynamic external method references beyond
  the fixed token spine; `GoCLR.Stdlib.dll` (2nd managed assembly) holds shims that
  work on the runtime types; linker copies it. Supports variadic + multi-result
  (`object[]` tuples) shims.
- ✅ **opaque value-type pattern** — stdlib value types with methods (sync.\*,
  time.Time, strings.Builder, bytes.Buffer) map to runtime handles; zero value calls
  a registered constructor; `&v` shares the one handle.
- ✅ **shim variables** — `os.Stdout`/`os.Stderr`/`time.UTC` resolve to accessor
  externs.
- ✅ **reflect keystone** — reflection done in C# over the boxed values + a
  compiler-emitted struct-tag registry (read-path); gates fmt-%v / json / template.

P0 packages shimmed (byte-exact): `math`, `strings` (+`strings.Builder`), `bytes`
(+`bytes.Buffer`), `errors`, `unicode`, `unicode/utf8`, `strconv`, `math/bits`,
`os`, `reflect`, `encoding/json` (Marshal **and** Unmarshal), `fmt` (+`Fprint*`),
`io` (`WriteString`), `sort`, `sync` (Mutex/RWMutex/WaitGroup/Once/Map), `time`
(Duration + `time.Time` incl. `Format`), `math/rand` (seeded, deterministic),
`context` (Background/TODO/WithValue/WithCancel/WithTimeout + Done/Err/Value).
String conversions (`string(rune)`/`[]byte`/`[]rune`), the `error` model (IGoError
fallback), and **native function values** (runtime-produced closures, e.g.
context.CancelFunc) done.

`json.Unmarshal` decodes into structs (incl. nested + slice-of-struct), slices,
maps, primitives, and `interface{}` via a compiler-emitted type descriptor (the
runtime erases slice/map element types), writing back through the GoPtr cell.

Float formatting is Go-exact: a shortest-round-trip ftoa (`GoFtoa`) drives `%v`,
`%g`, `%e`/`%E`, `println`, and `strconv.FormatFloat` (Go's `exp<-4 || exp>=6`
layout rule, lowercase `e`, ≥2-digit signed exponent).

The `reflect` **write-path** is done: `reflect.ValueOf(&x).Elem()` is settable and
`Value.Set/SetInt/SetUint/SetFloat/SetBool/SetString`, settable struct `Field(i)`,
`CanSet`, and `reflect.New` write back through the GoPtr cell (threading through
parent structs for nested field sets).

**P0 is complete.** Remaining stdlib work (P1–P3 and beyond) is tracked in the
done/pending checklist above. The typed-box keystone (M3) is in place — see below.

Known documented limitations:
- `time.Time` operates in **UTC** (Go uses Local in `time.Unix`/`Now`); use `.UTC()`
  for cross-runtime determinism.
- a named numeric type with a `String()` method (e.g. `time.Duration`, an `int`
  enum) passed to `fmt` as `any` prints the raw value, not the Stringer output —
  call `.String()` explicitly. The general fix is the typed-box keystone below.
- a panic that reaches the top of a goroutine prints the .NET unhandled-exception
  framing rather than Go's `panic:` + stack trace + `exit status 2`; the value and
  message are correct, and **recovered** panics (incl. divide-by-zero, index OOB,
  nil deref) match Go exactly.

### Validation suite (`tests/validation/`)

Whole, idiomatic apps across the target classes that must be byte-exact under
`go run` vs `goclr run`, proving the compiler is application-agnostic (goja is a
validation target, not the product). `business-json`, `cli-csv`, `rules-engine`,
`http-basic` ✅; `goja` / `examples/demo_goja` ✅ (evaluates a large JS subset); Gin
(`examples/demo_gin`, `examples/demo_gin_sql`) and Echo (`examples/demo_echo`) serve
on the CLR.

### M3 — the typed-box keystone + goja running (largely done)

`unsafe.Pointer` (goja's old blocker) is **solved** via `goclr.overlays/` +
`encoding/binary`. The keystone — **per-value runtime type identity**, designed in
`DESIGN-typed-box.md` — is implemented and carried goja from "compiles
partway" to **evaluating JavaScript**:
- ✅ **typed box**: `TypeId` on every boxed named value (named-primitive Stringers,
  `%T`, interface dispatch over representation-sharing types)
- ✅ **precise interface dispatch** via the typed box (resolves the representation
  collapse, incl. across slices/interfaces and promoted/variadic methods)
- ✅ **sample-based reflect** overlay covering goja's interop surface
- ✅ **large-program emitter** (4-byte metadata heap indices, `InitLocals`),
  **slice capacity semantics**, and a long tail of codegen-correctness fixes that
  together make **goja compile, load, JIT, run init, and evaluate a large JS
  subset** (see `examples/demo_goja`, GAPS.md) — tagged `0.0.21`…`0.0.24`
- ✅ JS surface: array callbacks (`map`/`filter`/`reduce`/`sort`),
  `JSON.stringify`/`parse` — byte-identical to `go run` (tag `0.0.27`)
- 🚧 `goclr test` with a real `testing.T`; CI conformance matrix; stable
  HTML/JSON compatibility report

### M4 — net/http overlay (Kestrel)
- 🚧 overlay package + Kestrel host

### M5 — Echo
- ✅ compile Echo v4 + run basic app (router + path params + JSON over the CLR; `examples/demo_echo`)

### M6 — Echo + goja target
- 🚧 the `cmd/server` service end-to-end

### M7 — packaging
- 🚧 `server.dll`, `runtimeconfig.json`, copy `GoCLR.Runtime.dll` / `GoCLR.Stdlib.dll`

### M8 — analyze / test
- ✅ `analyze` report (human + JSON)
- 🚧 `goclr test` execution (needs `testing.T` harness on the backend)

## Known hard problems surfaced by `analyze ./cmd/server`

`goclr analyze ./cmd/server` already pinpoints the real blockers in the target's
dependency closure:

- **goja `typedarrays.go`** uses `unsafe.Pointer` reinterpretation — needs a
  managed typed-array strategy.
- **`github.com/dlclark/regexp2` helpers** use `unsafe.Pointer`.
- **`golang.org/x/sys/unix`** ships assembly — needs a pure-Go / overlay path
  selected via build tags for the CLR target.
- **~160 stdlib packages** in the closure need overlay coverage (most are pure Go
  and compile directly once the backend exists; a curated subset needs managed
  replacements).

These are tracked as the backend and overlay work proceeds.
