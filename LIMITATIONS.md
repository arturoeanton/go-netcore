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

## Stringer/Error in fmt — named *primitive* types only

Custom **struct and pointer** types that implement `fmt.Stringer` or `error` now
format via their `String()`/`Error()` method under `%v`/`%s` (and inside slices and
maps) — see the dispatch tables registered at startup.

The remaining case is a named **primitive** type with a `String()`/`Error()` method
(e.g. `time.Duration`, `time.Month`, `time.Weekday`, an `int`-based enum) passed to
fmt **as `any`**: it boxes to a bare primitive, so fmt can't recover the named type
and prints the underlying value. **Workaround:** call `.String()` explicitly. A
general fix needs type-tagged boxing of named primitives at interface conversions
(the same per-value type identity that precise `%T` and `reflect` need — M3). This
is the **typed-box keystone**; its execution-ready design is in
`docs/DESIGN-typed-box.md`, and it also unblocks the `%#v`/`%T`/nil-map cases above
and the goja validation target (below).

## Uncaught panic output format

A panic that is **recovered** behaves exactly like Go (including runtime panics:
integer divide-by-zero, index out of range, nil dereference). A panic that reaches
the top of a goroutine prints the .NET unhandled-exception format
(`Unhandled exception. GoCLR.Runtime.GoPanicException: panic: …`) rather than Go's
`panic: …` followed by a goroutine stack trace and `exit status 2`. The panic value
and message are correct; the surrounding framing and the stack trace are not
reproduced. (Conformance compares recovered panics, whose output is exact.)

## goja validation target

The pure-Go JavaScript engine `goja` does not yet compile under goclr: it pulls in
`golang.org/x/text/collate`, which uses `sort.StringSlice` through `sort.Sort`.
Dispatching `sort.Interface` to `sort.StringSlice` requires per-value type identity
(every named slice type currently collapses to one `GoSlice`, so slice-based
`Interface` implementers are mutually indistinguishable). This is the
representation-collapse problem solved by the typed-box keystone
(`docs/DESIGN-typed-box.md`); `cmp` and a few `x/text` support overlays are also
needed. `examples/demo_goja` and `tests/validation/goja` track this target.

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
