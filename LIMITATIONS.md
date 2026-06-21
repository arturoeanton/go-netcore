# Known limitations (tracked technical debt)

These are the deliberately-deferred gaps after the P0 hardening pass. Each is
documented so it fails predictably (or is avoidable), not silently. None block the
P0 stdlib surface; they are edges or larger features.

## reflect — runtime type descriptors

reflect is driven by compile-time **type descriptors** (goclr's `*rtype`): every
named and struct type, and every type observed at a `reflect.TypeOf`/`ValueOf` site
(recursively, its element/key/field types), is registered at startup with its
precise kind, name, type string, element/key types, and struct fields
(name/tag/type/anonymous). `reflect.TypeOf`/`ValueOf` carry the static type's
descriptor id, and a value reached through an interface recovers its descriptor from
its identity (a struct's emitted type, a named type's typed-box id, or a boxed
scalar). So `Kind`, `Name`, `String`, `NumField`, `Field`, `Elem`, `Key`, and the
sized-integer kinds are all precise — for struct/named/scalar types — without a
sample value.

Remaining gaps (tracked):

- **A *bare* unnamed sized scalar reflected *only* dynamically loses its width.**
  `reflect.TypeOf(interface{}(uint8(5))).Kind()` reports the wide bucket (`Int`/`Uint`)
  because a narrow scalar (`uint8`/`int16`/`int32`/`float32`/…) boxes to a .NET
  representation that doesn't carry its width. **This does not affect the dominant
  reflection pattern — reflecting over struct fields** (validator, encoding/json,
  ORMs): a struct's descriptor carries each field's exact type, so a `uint8` field
  reflects as `uint8` even when the struct is reached dynamically through an
  interface. Named scalar types (with a method set) are also exact via the typed box.
  The only fix would tag *every* scalar boxed into an interface — overhead and risk on
  a very common operation for a rare benefit — so it is deliberately not done.
- **An unnamed composite (`[]int`, `map[string]int`) reflected *only* dynamically**
  can't recover its element/key type — the runtime slice/map header carries no type
  tag. Reflected from a concrete static site (the common case) it is exact; named
  composite types and struct fields are exact. `reflect.MapOf`/`SliceOf`/`PtrTo`/
  `ArrayOf` construct precise composite types regardless.

## Type-info erasure (runtime is non-generic)

The runtime slice/map representation erases element types, so a few things can't
be exact without compiler-emitted type descriptors (json.Unmarshal already carries
one; these don't yet):

- **`%#v` of a slice/map** prints `[]interface {}{…}` / `map[string]interface {}{…}`
  instead of the concrete element type (`[]int{…}`). Scalars, structs, pointers
  are exact.
- **`%T` of a slice/map** prints `[]interface {}` / `map[string]interface {}`
  rather than the precise element types.
- **`%v` of a nil map** prints `<nil>` instead of `map[]` (a nil map boxes to a
  null reference, indistinguishable from other nils). Nil slices are correct (`[]`).

## Stringer/Error of named types — the typed box (largely implemented)

Custom **struct and pointer** types that implement `fmt.Stringer`/`error` format
via their method under `%v`/`%s`. A named **non-struct** type with a method set
(`type Money int64` with `String()`, an `int` enum, a named slice) now also carries
its identity through interfaces via the **typed box** (`GoNamed`, see
`docs/DESIGN-typed-box.md`): top-level `%v`/`%s`/`%T` dispatch correctly, and
interface dispatch distinguishes named types that share a representation (so two
named slices both satisfying `sort.Interface` dispatch to their own methods — the
representation collapse that blocked goja is resolved).

Remaining edges (documented, not silent):
- A named-type value **nested inside a concrete container** (`[]Money`, a struct
  field) formats by its underlying value under `%v` — Go calls `String()` per
  element. Top-level args and `[]any`/`map[K]any` elements are tagged and dispatch
  correctly; concrete containers are not (they must stay comparable/indexable).
- `%T`/`%#v` of a **method-less** named type, and of a slice/map element type,
  still print the underlying representation (only method-bearing named types get an
  identity tag so far).
- `%v` of a **nil map** prints `<nil>` instead of `map[]`.

## Uncaught panic output format

A panic that is **recovered** behaves exactly like Go (including runtime panics:
integer divide-by-zero, index out of range, nil dereference). A panic that reaches
the top of a goroutine prints the .NET unhandled-exception format
(`Unhandled exception. GoCLR.Runtime.GoPanicException: panic: …`) rather than Go's
`panic: …` followed by a goroutine stack trace and `exit status 2`. The panic value
and message are correct; the surrounding framing and the stack trace are not
reproduced. (Conformance compares recovered panics, whose output is exact.)

## goja validation target — runs a large JS subset; full spec needs deeper reflect

The typed box resolved goja's headline blocker (the `sort.StringSlice` /
representation-collapse dispatch), and the addressable-fields + dispatch work that
followed cleared the rest of the language tail. goja **compiles, loads, JITs, runs its
package init, and evaluates a large JavaScript subset** byte-identical to `go run`
(arithmetic, strings + string methods, `Math`, objects/property access, closures,
loops, array callbacks `map`/`filter`/`reduce`/`sort`, `Object.keys`, and
`JSON.stringify`/`parse` round-trips); `tests/validation/goja` passes. The remaining
gap is goja's deepest Go↔JS interop into **`reflect`** (`MakeFunc`, deep `Value`/`Type`
operations) needed for the *full* JS spec — a large surface beyond the current
read/write reflect shim, and the deep-reflect milestone.

## Function values of shimmed stdlib functions

Passing a shimmed stdlib function *as a value* (e.g. `strings.TrimFunc(s,
unicode.IsSpace)`) is unsupported — only func literals / local func values work as
callback arguments. Wrapping a shim function reference in a native closure is a
separate feature.

## reflect.StructField direct access

`reflect.Type.Field(i).Name` / `.Tag` (field access on a `reflect.StructField`
value) is not wired — field access on a shim type needs the method-based shim
mechanism extended. The common reflect read/write paths (Value.Field, NumField,
Kind, Set*, …) and `encoding/json` (which reads tags internally) work.

## Unicode special-casing

`strings.ToUpper`/`ToLower` use simple 1:1 case mapping; the handful of Unicode
special-case expansions (e.g. `İ` U+0130 → `i` + combining dot) are not applied.
`ß`→`SS`, final-sigma, and the common Latin/Greek/Cyrillic mappings are correct.

## time is UTC-only

`time.Time` operates in UTC. Go's `time.Now()`/`time.Unix()` use the local zone;
for cross-runtime-deterministic output use `.UTC()` and `time.Date(..., time.UTC)`.

## Fixed-size arrays — value semantics edge

`[N]T` fixed-size arrays are supported (slice-backed). They carry Go value
semantics: copying an array — on assignment (`y := x`), argument passing (named
functions and closures), return, and storing into a container — duplicates its
backing storage; slicing an array (`a[:]`) shares it, as in Go.

The one residual case is an array that is a **field of a struct** which is then
copied by value: `b := a` where `a` has an `[N]T` field, followed by mutating that
field through `b`, still aliases `a`'s array. A correct fix needs a compiler-emitted
deep copy (the runtime cannot distinguish an array-backed `GoSlice` from a real
slice). Workaround: copy the array field explicitly, or hold it behind a pointer.

## Stdlib items still deferred

Done since this list was first written: **`net` UDP** (UDPConn/UDPAddr, loopback
round-trip), **`log/slog`** (text + JSON), **`os/signal`** (real SIGINT/SIGTERM
delivery), **`net/http/cookiejar`**, **`net/http/httptest`** (live server + recorder),
**`database/sql` + `database/sql/driver`** (with the `go-r2-sqlite` engine), and
**`mime/multipart`** (form parsing). Still deferred (need a larger feature or external
module):

- **`container/heap`** — works for a **struct** receiver type: `heap.Init/Push/Pop/
  Fix/Remove` drive the user type's `Less/Swap/Push/Pop` through the interface
  method-callback bridge (`Bridge.CallMethod` + compiler-generated per-method
  adapters; see `docs/DESIGN-callback-bridge.md`). A **named non-struct** receiver
  (the idiomatic `type IntHeap []int`) is not yet supported — its pointer carries no
  type id (`ptrNew` tags struct pointees only), so the bridge can't resolve its
  methods and throws a clear error. Use a struct wrapper (`type H struct{ data []int }`)
  meanwhile; unifying the pointer type id is the documented follow-up.
- **`io/fs.Stat`** — real over **`os.DirFS`** and any `fs.FS` whose `Open` returns an
  `*os.File` (echo's defaultFS, `http.FS(os.DirFS(...))`): the `fsys.Open` call goes
  through the callback bridge and an `os.File`-backed `FileInfo` is read back. A user
  `fs.FS` whose `Open` returns the program's OWN `fs.File`/`fs.FileInfo` types is not
  dispatched (`io/fs.FileInfo` is an interface in the shim method registry, assuming
  `GoFileInfo`); such a call returns a clean not-found rather than crashing. Value-receiver
  / named-map `fs.FS` likewise fall back (bridge type-id covers GoPtr/GoNamed only).
- **`x/sync/errgroup`** — shim written, but the import needs the external x/sync
  module present to type-check.
- **`google/uuid`** — not yet shimmed.
- **slog edges**: the automatic timestamp is omitted (so output is reproducible —
  drop `slog.TimeKey` via `ReplaceAttr` to match `go run`); `WithGroup`/`LogAttrs` and
  the `HandlerOptions.Level`/`ReplaceAttr` fields are accepted but not applied.
- **os/signal edges**: `int(syscall.SIGINT)` (converting a signal constant to an
  integer) is unsupported — a signal is an opaque `GoSignal`, not a bare int; print it
  or compare `os.Signal` values instead.

## Interface dispatch keys on the boxed representation

goclr's interface method dispatch is resolved at compile time: it enumerates the
concrete types (across all lowered packages) that implement the interface and emits
an `isinst` chain on the boxed value's runtime representation. This is exact when
implementers have distinct representations — distinct struct types, or pointer
implementers (disambiguated by `GoPtr.TypeId`). It is **not** able to distinguish
two named types that share one runtime representation:

- Two or more **named slice** types implementing the same interface both box to
  `GoSlice`; a dispatch site reachable by both cannot tell them apart. The standard
  library's `sort.IntSlice`/`StringSlice`/`Float64Slice` adapters are therefore
  omitted from the goclr `sort` overlay, and `sort.Sort`/`Stable` on a *single*
  named-slice implementer works, but a program with two distinct named-slice
  implementers of one interface would mis-dispatch. Use a struct or pointer
  implementer (as goja does) when precise dispatch is required.
- The same applies to two named map types, or two named types over the same scalar.

A precise fix needs per-value runtime type tags (an itable), which is M3 scope.

**Resolved cases.** Pointers to *non-struct* types are now discriminated by the
pointee's runtime representation (`Rt.PtrPointeeKind`): a type switch / comma-ok over
`*int64` vs `*string` vs `*[]byte` matches correctly — exactly what `database/sql`'s
`convertAssign` needs to scan numbers and strings into their Go types. Residual: `*[]byte`
and `*sql.RawBytes` share the slice representation and still can't be told apart.
Opaque **shim** values flowing through an interface they satisfy (a `sync.RWMutex` as
`sync.Locker`, a `syscall.Signal` as `os.Signal`) also dispatch correctly now — a general
mechanism keyed on `types.Implements` + a self-declared `[GoShim]` CLR-class registry, with
no Go type hardcoded in the compiler. A shim type participates once its value class carries
the `[GoShim("pkg.Type")]` attribute.

### Incidental implementers whose method is a shim-type method

A large program's import closure contains many types that *incidentally* satisfy a
common interface (`io.Reader`, `io.ByteReader`, `fmt.Stringer`, …). When such an
implementer's method belongs to a C# shim type — it has no lowered Go body and no
shim extern — goclr cannot emit a real call for it. Rather than fail the whole
compilation, the dispatch still *matches* that type but its case body panics
("interface method X on T is not supported (shim type method)"). This is a guarded,
diagnosable failure that fires only if such a value actually reaches that call site
(it usually cannot — e.g. `*bufConn` in `x/net/http2/h2c` promotes `ReadByte` from
an embedded `*bufio.Reader` and is enumerated as an `io.ByteReader` implementer,
yet never flows into one). If a real program hits the panic, the fix is to register
that shim type's method as an extern (`shimMethodRegistry`).

## Typed-nil pointer inside an interface

A nil pointer stored into an interface (`var p *T; var i any = p`) compares
`i == nil` **true** in goclr, where Go reports **false** (Go's interface keeps the
static type, so the interface is non-nil even though the pointed-to value is nil).
A consequence: code that distinguishes "nil interface" from "interface holding a
typed nil" (some library `Value` hierarchies) can diverge. A precise fix needs a
typed-nil representation that survives boxing.

## `slices` / `cmp`

Provided. `cmp` and `slices` compile from source (`slices` via a `replaceOnly`
overlay that patches only `slices.go`'s `unsafe`-based `overlaps`/`startIdx` with a
pointer-identity scan; the rest of the package is its real GOROOT source). Caveat:
`slices.overlaps`/`startIdx` rely on slice-element pointer identity, which goclr's
GoPtr model does not preserve across `&s[i]` — so the in-place aliasing
optimizations they guard are conservative (they fall back to copying). This does
not affect element values, only the optimization choice.

## Misc

- `strings.NewReplacer`, `strings.EqualFold` full-Unicode folding, and
  `unicode.SimpleFold` are not implemented.
- `math/bits` int8/int16 are typed as int32 at the boundary; the `bits.*8/*16`
  helpers mask correctly but very unusual signedness edges may differ.
- Goroutine scheduling order is the .NET thread pool's, not Go's scheduler — keep
  concurrent test output order-independent (as Go's map-range convention already
  requires).
