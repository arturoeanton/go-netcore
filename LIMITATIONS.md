# Known limitations (tracked technical debt)

These are the deliberately-deferred gaps after the P0 hardening pass. Each is
documented so it fails predictably (or is avoidable), not silently. None block the
P0 stdlib surface; they are edges or larger features.

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

## goja validation target

The typed box resolved goja's headline blocker (the `sort.StringSlice` /
representation-collapse dispatch). Addressable struct fields (`&s.field`, incl.
correct `sync/atomic` on a field) are now implemented (field-alias pointers that
re-navigate a stable root under the atomic shim's lock). goja now **compiles
through** `sort`/`cmp`/`slices`, all of `regexp2`, `go-sourcemap`, and into goja's
own packages (`unistring`, `file`, `ast`). It does **not** yet run end-to-end. The
current frontier is **interface dispatch to a generic-type implementer**
(`Optional[T]` satisfying goja's `ast` node interfaces — the implementer's method
is monomorphized per instantiation, so dispatch can't yet resolve it), and beyond
that goja's Go↔JS interop is heavily `reflect`-based (a large surface beyond the
current reflect shim). `examples/demo_goja` and `tests/validation/goja` track this
target; the harness reports it skipped until it runs.

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

## P1 items still deferred

These P1 packages need a larger feature or external module and are deferred:
- **`container/heap`** — `heap.Init/Push/Pop` call back into the user type's
  `Less/Swap/Push/Pop` (interface methods); calling Go methods from a shim needs an
  interface-method-callback bridge.
- **`flag`** — needs command-line args forwarded from `goclr run` to the program
  (`os.Args` plumbing).
- **`net` UDP** — `PacketConn.WriteTo` takes a `net.Addr`; needs
  `net.ResolveUDPAddr`/`*net.UDPAddr`. TCP (Listen/Dial/Conn) works.
- **`x/sync/errgroup`** — shim written, but the import needs the external x/sync
  module present to type-check.
- `log/slog`, `mime/multipart`, `os/signal`, `net/http` cookiejar/httptest,
  `google/uuid` — not yet shimmed.

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

## goja / full runtime reflection

Running goja (and similar reflection-heavy engines) end-to-end is **not** yet
possible: goja's Go↔JS interop layer is built on the full `reflect` API
(`reflect.ValueOf`, `MakeSlice`, `MakeMap`, `New`, field/method access by name)
across ~25 files. goclr's value model erases runtime type identity, so a faithful
`reflect` needs the same per-value type tags / runtime type descriptors as the
interface-dispatch item above. The supporting infrastructure is in place
(`go mod vendor` + unsafe-pointer overlays, `unicode` and `sort` compiled from
source, long-form local opcodes, `&slice[i]`); the remaining blocker is the
runtime type system. Tracked as an M3 milestone. See GOJA-STRATEGY.md.

## `slices` / `cmp`

Not yet provided. The path is a source overlay like `sort`'s (cross-package generic
instantiation now works, so the generic functions monomorphize correctly). The one
remaining constraint is **API completeness**: overlaying `slices` replaces its
source for the whole build's type-checking, so the overlay must define every
exported function any package in the graph uses — `os`, for instance, calls
`slices.Grow`. A partial overlay breaks unrelated stdlib type-checking, so the
overlay must implement the full non-iterator API (~32 functions).

## Misc

- `strings.NewReplacer`, `strings.EqualFold` full-Unicode folding, and
  `unicode.SimpleFold` are not implemented.
- `math/bits` int8/int16 are typed as int32 at the boundary; the `bits.*8/*16`
  helpers mask correctly but very unusual signedness edges may differ.
- Goroutine scheduling order is the .NET thread pool's, not Go's scheduler — keep
  concurrent test output order-independent (as Go's map-range convention already
  requires).
