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

## Boxed Stringer (named types with a String() method)

Passing a named numeric type that has a `String()` method (e.g. `time.Duration`,
`time.Month`, `time.Weekday`, `reflect.Kind`) **directly to fmt as `any`** prints
the underlying value, not the Stringer output — fmt can't recover the named type
from a boxed primitive. **Workaround:** call `.String()` explicitly (supported).
A general fix needs type-tagged boxing at interface conversions.

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

## Fixed-size arrays

`[N]T` fixed-size array types (e.g. `var a [4]byte`, `[32]byte`) are not yet
supported (use slices). Consequence: `sha256.Sum256(data)` (returns `[32]byte`)
is unavailable — use `h := sha256.New(); h.Write(data); h.Sum(nil)` ([]byte). And
`hmac.New(sha256.New, key)` needs a shim function value (see above), so HMAC via
the func-constructor is deferred.

## Multi-value call as an argument list

`f(g())` where `g` returns multiple values (e.g. `fmt.Println(strconv.Atoi(s))`)
is not yet supported — assign the results first (`v, err := g(); f(v, err)`).

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
