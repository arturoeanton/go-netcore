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
- **A width flag on a non-numeric recursing verb is not per-element.** Numeric verbs
  (`%03d`, `%6.2f`, `%04b`, …) now pad each element of a recursed slice/map/struct like
  Go (`[005 042]`); the residual is `%s`/`%q`/`%x` width and the bad-verb operand pad of
  a string map key (`%03d` of `map[string]int` → goclr `%!d(string=a)` vs Go
  `%!d(string=00a)`) — both rare.
- **`%T`/`%#v` of an anonymous struct** prints the synthesized name
  (`main.__anonN`) instead of Go's structural form (`struct { X int; Y string }`).
  The field types can't be recovered byte-exactly from runtime values (`int` and
  `int64` both box to a 64-bit integer), so this needs a compiler-side reflect
  string built from the static field types — deferred. `%v`/`%+v` of an anonymous
  struct are correct.
- **`%v` of a nil map** prints `<nil>` instead of `map[]` (a nil map boxes to a
  null reference, indistinguishable from other nils). Nil slices are correct (`[]`).
  (`%#v` of a nil map field inside a struct renders correctly as `map[K]V(nil)` — its
  static type name is recovered from the field-type registry even though the value is
  a bare null.)
- **`%#v` of a `[]byte` field** spells the type `[]byte` rather than Go's reflect
  spelling `[]uint8` (Go uses `[]byte` at top level but `[]uint8` for a struct field —
  goclr uses `[]byte` in both). Element values and length are exact. Minor cosmetic
  divergence on the struct-field path only.
- **`%q` of a `[]byte` *nested inside* a slice** (e.g. a `[][]byte` from
  `regexp.FindAllSubmatch`) prints each inner `[]byte` rune-style (`['a' '1']`) instead
  of Go's double-quoted string form (`"a1"`). The top-level `%q` of a `[]byte` is exact;
  only the nested-element path misses the byte-slice→string special case. Use `%v` (exact)
  or `%s` for nested byte slices.
- **`%v` of a hand-built `net.Addr`** (`&net.UDPAddr{IP, Port}`) has no precomputed
  string (the shared `GoNetAddr` shim only fills it from parsing), so it prints empty;
  one from `ParseCIDR`, `ResolveReference`, or a connection's `LocalAddr`/`RemoteAddr`
  prints correctly. (`net.IP`/`net.IPMask`/`net.HardwareAddr` now print via `String()`.)
- **`%v`/`%+v` of a nested non-nil pointer-to-struct field** prints `&{…}` (the
  dereferenced content) instead of Go's `0x…` address. Go only expands a pointer to
  `&{…}` at the top level; deeper pointer fields print their address. Since the
  address is non-deterministic in both runtimes this can't be made byte-exact, and
  goclr's content form is more useful; nil pointer fields and top-level pointers are
  correct.
- **`json.Number` and `json.RawMessage`** are supported as struct fields and at the
  top level, both directions: `Unmarshal` keeps a `Number`'s raw numeric literal and
  captures a `RawMessage`'s value bytes verbatim; `Marshal` emits a `Number` unquoted
  and a `RawMessage` verbatim (struct fields resolve their identity through the field
  type registry; a top-level/`GoNamed`-boxed value through its type id). **Edge still
  deferred:** re-marshaling a `[]json.Number` or `map[string]json.RawMessage` (the
  elements lose their named identity once stored in a type-erased container, so they
  emit as a quoted string / byte array). Reading *into* such containers is correct.
- **`time.Time` in JSON** marshals/unmarshals as its RFC3339 string (Go's
  `Time.MarshalJSON`/`UnmarshalJSON`) as a struct field, slice element, map value, or
  top-level target — not the raw runtime struct.
- **`json.Marshal` of a *user* `json.Marshaler`** (a type with its own custom
  `MarshalJSON`, other than the built-ins special-cased above) is not honored: the
  runtime marshals the underlying value structurally (a `Temp float64` emits the
  number) because the named type loses its identity once stored as a struct field or
  slice element. Honoring it needs the marshaled type's static descriptor threaded
  into `Marshal` (as `Unmarshal` does).

## Stringer/Error of named types — the typed box (largely implemented)

Custom **struct and pointer** types that implement `fmt.Stringer`/`error` format
via their method under `%v`/`%s`. A named **non-struct** type with a method set
(`type Money int64` with `String()`, an `int` enum, a named slice) now also carries
its identity through interfaces via the **typed box** (`GoNamed`, see
`DESIGN-typed-box.md`): top-level `%v`/`%s`/`%T` dispatch correctly, and
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
integer divide-by-zero, index out of range, nil dereference). An **uncaught** panic now
crashes in Go's shape — `panic: <value>`, a blank line, a `goroutine 1 [running]:` header,
and **exit status 2** — instead of the .NET unhandled-exception dump. A synthetic entry
wrapper runs `init()`/`main()` inside a top-level handler (`Rt.FatalPanic`); see
`tests/panicfmt`. The frames printed under the header are the **CLR** stack (goclr has no
Go-format stack metadata with source positions / `+0x` offsets), so the crash is Go-shaped
and debuggable but not byte-identical to `go run`'s goroutine trace. The `exit status 2`
line itself is printed by `go run` (the wrapper), not the program; the program exits 2.

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

Supported. A shimmed stdlib function taken *as a value* (`up := strings.ToUpper`),
passed as a callback (`strings.Map(unicode.ToUpper, s)`), stored in a slice of func
values, or used as a shim method value (`w := b.WriteString`) all work — the reference is
wrapped in a native closure (`Closures.FromShim`) that invokes the shim by reflection.
Variadic shim function values (`sp := fmt.Sprintf; sp("%d", 1)`) pack their trailing
arguments into the shim's slice parameter. Fixture 406_shim_func_value.

## range-over-func with return / defer / labeled jump

`for x := range seq` over an `iter.Seq`/`iter.Seq2` (Go 1.23 range-over-func) works,
including `break` and `continue`. A **`return`, `defer`, or labeled `break`/`continue`/
`goto` inside the loop body** is not yet lowered (it needs Go's state-machine rewrite
that returns from the enclosing function through the yield protocol) and reports a
clear error rather than miscompiling. `iter.Pull` is supported.

## strings.EqualFold special folds

`strings.EqualFold` uses .NET ordinal case-insensitive comparison, which matches Go for
ASCII and the common Latin/Greek/Cyrillic letters but not the few multi-codepoint
Unicode fold orbits (e.g. `K` U+212A KELVIN SIGN ↔ `k`, `ſ` long-s ↔ `s`, `Å` U+212B
↔ `å`). Go folds these via `unicode.SimpleFold`'s orbit table; goclr does not.

## math transcendental last-ULP

The `math` package maps the transcendental functions (`Log`, `Log10`, `Sin`, …) onto
the platform's `System.Math`, which can differ from Go's own implementations by the
last ULP for some inputs (e.g. `math.Log(0.01)`). Functions built on them — including
`math.Lgamma` (a faithful port of Go's algorithm, otherwise byte-exact) — inherit that
last-ULP edge on the affected inputs; the value is correct to ~1 ULP.

## regexp POSIX leftmost-longest

`regexp.CompilePOSIX`/`MustCompilePOSIX` compile and match, but use the default
leftmost-**first** semantics rather than POSIX leftmost-**longest** (the underlying
.NET engine is leftmost-first). For most patterns the result is identical; it differs
only where an earlier alternative is a prefix of a later one — `a|ab` on `"ab"`
matches `"a"` here, `"ab"` under Go's POSIX mode. `(*Regexp).Longest()` is a no-op.

## Unicode special-casing

`strings.ToUpper`/`ToLower` use simple 1:1 case mapping; the handful of Unicode
special-case expansions (e.g. `İ` U+0130 → `i` + combining dot) are not applied.
`ß`→`SS`, final-sigma, and the common Latin/Greek/Cyrillic mappings are correct.

## time is UTC-only

`time.Time` operates in UTC. Go's `time.Now()`/`time.Unix()` use the local zone;
for cross-runtime-deterministic output use `.UTC()` and `time.Date(..., time.UTC)`.

`Format`/`Parse` accept fractional-second layout tokens of **any** width — a `.`
or `,` separator followed by a run of `0`s (fixed width, trailing zeros kept) or
`9`s (trailing zeros trimmed, separator dropped when empty) — not just the canonical
`.000`/`.000000000`/`.999999999` forms (`.9`, `.99`, `.000000`, `05,000`, … all work).

## Fixed-size arrays — value semantics edge

`[N]T` fixed-size arrays are supported (slice-backed). They carry Go value
semantics: copying an array — on assignment (`y := x`), argument passing (named
functions and closures), return, and storing into a container — duplicates its
backing storage; slicing an array (`a[:]`) shares it, as in Go.

Copying a **struct that holds an array field** (directly or through nested structs)
now deep-copies those array fields too, so mutating the copy no longer aliases the
original. The compiler emits the deep copy field-by-field using the static type
(arrays of structs and arrays of arrays recurse element-wise); slice/map/pointer
fields stay shared, as in Go. This applies at every value-copy site arrays already
covered (assignment, argument, return, container store, range binding).

## Stdlib items still deferred

Done since this list was first written: **`net` UDP** (UDPConn/UDPAddr, loopback
round-trip), **`log/slog`** (text + JSON), **`os/signal`** (real SIGINT/SIGTERM
delivery), **`net/http/cookiejar`**, **`net/http/httptest`** (live server + recorder),
**`database/sql` + `database/sql/driver`** (with the `go-r2-sqlite` engine), and
**`mime/multipart`** (form parsing). Still deferred (need a larger feature or external
module):

- **`container/heap`** — works, including the idiomatic **named-slice** implementer
  (`type IntHeap []int` reached as `*IntHeap`): `heap.Init/Push/Pop/Fix/Remove` drive
  the user type's `Less/Swap/Push/Pop` through the interface method-callback bridge
  (`Bridge.CallMethod` + compiler-generated per-method adapters; see
  `DESIGN-callback-bridge.md`). Struct ids and typed-box named ids now share one
  counter, so `&IntHeap{...}` carries `IntHeap`'s unified id and the bridge resolves its
  methods through the pointer. Implementers reached **by value** (a value-receiver struct
  passed by value, a named non-struct value) are also dispatched: `Bridge.TypeIdOf`
  recovers a struct value's id from its CLR type (a compiler-emitted `LinkClrId` map), and
  value-receiver adapters normalize any receiver form via `Bridge.RecvValue` (fixture
  401_bridge_byvalue_writer).
- **`io/fs.Stat`** — works over **`os.DirFS`**, any `fs.FS` whose `Open` returns an
  `*os.File` (echo's defaultFS, `http.FS(os.DirFS(...))`), AND a **user `fs.FS` whose `Open`
  returns the program's own `fs.File`/`fs.FileInfo` types**: `fs.Stat` takes the `StatFS`
  fast path or `fsys.Open` + `file.Stat` through the callback bridge, and the returned
  `fs.FileInfo` dispatches to its own methods. `io/fs.FileInfo`/`os.FileInfo` are no longer
  short-circuited as a single shim handle — an interface-typed receiver with a user
  implementer routes through interface dispatch (the shim's own `GoFileInfo`, tagged
  `[GoShim("io/fs.FileInfo")]`, still matches via `IsShimKindStrict`). Fixture
  403_fs_fileinfo_dispatch. (Note: `testing/fstest.MapFS` is standard-library code that is
  not lowered, so its methods can't be bridged — that is a stdlib-coverage gap, not a
  dispatch gap; a user-defined in-memory `fs.FS` works.)
- **`x/sync/errgroup`** — works: compiles from source and runs (concurrent goroutines +
  first-error propagation), now that `context.WithCancelCause`/`context.Cause` are shimmed.
  See `examples/demo_errgroup` (requires `go mod vendor`).
- **`google/uuid`** — works: compiles from source and runs (v4 with a custom rand reader,
  v5 `NewSHA1`, parse/format, version/variant, nil). Closed the stdlib gaps it needs:
  `os.Getuid`/`Getgid`, `net.Interfaces` (empty list — no host-NIC enumeration),
  `bytes.EqualFold`, `encoding/hex.Encode`/`Decode`, and the io.Reader callback bridge.
  See `examples/demo_uuid` (requires `go mod vendor`).
- **slog edges**: the automatic timestamp is omitted (so output is reproducible —
  drop `slog.TimeKey` via `ReplaceAttr` to match `go run`). Grouping is supported:
  `(*Logger).WithGroup` and `slog.Group` nest attributes (JSON objects / dotted text
  keys) and compose with `With`. `LogAttrs` and the `HandlerOptions.Level`/`ReplaceAttr`
  fields are accepted but not applied.
- **os/signal edges**: `int(syscall.SIGINT)` (converting a signal constant to an
  integer) is unsupported — a signal is an opaque `GoSignal`, not a bare int; print it
  or compare `os.Signal` values instead.
- **`encoding/xml` is marshal-only**: `xml.Marshal`/`MarshalIndent`/`Encoder` are
  reflection-based and cover element/attr/chardata/innerxml/omitempty tags, `XMLName`,
  and nested-element paths (`xml:"a>b>c"`, with consecutive fields sharing a prefix
  merged into one wrapper). `xml.Unmarshal`/`Decoder.Decode` return an honest error
  (`xml: decoding is not supported under goclr`) — a reflection-driven decoder is the
  larger deferred piece.

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

### Shimmed-package error types in a type switch

`encoding/json`/`encoding/xml` are shimmed, and their error types
(`*json.SyntaxError`, `*json.UnmarshalTypeError`, `*xml.SyntaxError`,
`*xml.UnsupportedTypeError`) are opaque shim handles so a `case *json.SyntaxError:` and
the field/method reads inside it (`.Offset`, `.Error()`, …) compile (echo's binder uses
them). The decode shims always return a **plain `GoError`**, never one of these typed
errors, so a `case *json.SyntaxError:` is matched by the *precise* `IsShimKindStrict`
path — it never falsely captures an unrelated error (`errors.New`, `fmt.Errorf`), which
the old loose heuristic did. The residual: a genuine JSON/XML syntax error returned by
the shim is a generic `GoError`, so that `case` does not match it and the error falls to
`default` (an under-match — the conservative direction, vs. the previous dangerous
over-match). `err.Error()` (the message) is still correct through the `error` interface.

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

A nil pointer stored into an interface (`var p *T; var i any = p`, or `return p` where the
result type is `error`) now compares `i == nil` **false**, matching Go — the interface
keeps the dynamic type even when the pointer is nil (the classic `err != nil` gotcha is
faithful). `exprCoerced` boxes a nil concrete pointer into a non-null `GoPtr` carrying the
pointee's type id (`Rt.BoxNilPtr`), so `== nil` (reference compare) is correctly false and
type assertion / method dispatch resolve the dynamic type. Fixture 404_typed_nil_interface.

**Residual:** the *recovered* pointer compares against `nil` as non-nil. After
`p := i.(*T)` (or inside a method called on the typed-nil interface), `p == nil` is
**false** in goclr where Go yields **true** — because the recovered value is a non-null
`GoPtr` with a null payload, and goclr compares pointer identity, not payload. So a
method with an `if recv == nil { … }` guard called on a typed-nil interface does not take
the guard (and will nil-deref if it then uses the receiver, as Go would only if the guard
were absent). Fixing this precisely needs pointer `== nil` to test the payload for every
pointer, a wide change deferred to keep the bare-pointer path (the common case) low-risk.

## `slices` / `cmp`

Provided. `cmp` and `slices` compile from source (`slices` via a `replaceOnly`
overlay that patches only `slices.go`'s `unsafe`-based `overlaps`/`startIdx` with a
pointer-identity scan; the rest of the package is its real GOROOT source). Caveat:
`slices.overlaps`/`startIdx` rely on slice-element pointer identity, which goclr's
GoPtr model does not preserve across `&s[i]` — so the in-place aliasing
optimizations they guard are conservative (they fall back to copying). This does
not affect element values, only the optimization choice.

## `unsafe.Pointer` — the safe idioms only

goclr's value model has **no raw memory** (a `GoSlice` is `object[]`-backed, a
`GoString` is a .NET string, a `GoPtr` is a cell), so general pointer arithmetic and
header *writes* are fundamentally impossible. What **is** supported (see
[DESIGN-unsafe-pointer.md](DESIGN-unsafe-pointer.md)):

- the **`string ↔ []byte` zero-copy reinterprets** — the modern builtins
  `unsafe.String(unsafe.SliceData(b), n)` / `unsafe.Slice(unsafe.StringData(s), n)`
  and the old `*(*string)(unsafe.Pointer(&b))` / `*(*[]byte)(unsafe.Pointer(&s))` form
  — lowered as the equivalent (copying) conversions;
- **read-only `reflect.SliceHeader`/`StringHeader` offset views** —
  `(*reflect.SliceHeader)(unsafe.Pointer(&x)).Data` for offset arithmetic (two views
  over the same backing differ by exactly the byte offset; this is what go-toml's
  `SubsliceOffset` needs). `StringHeader` offset *diffs* are not recoverable (.NET
  substrings don't share backing storage); only `.Len` is meaningful there.

Everything else — a bit-cast like `*(*float32)(unsafe.Pointer(&u64))`, pointer
arithmetic, or a header *write* that reconstructs a slice from a foreign header (the
pre-1.20 `s2b`, fasttemplate/validator) — is **rejected** with `GCLR0301`/`GCLR0201`,
not silently miscompiled. Those stay overlay territory (`goclr.overlays/`).

## `goclr test`

`goclr test ./pkg` compiles the package's tests (driven by a minimal real-Go `testing`
overlay, `internal/frontend/overlays/testing`) to a .NET assembly and runs them, printing
a go-test-like report and exiting non-zero if any test fails. Supported: `TestXxx(t
*testing.T)`, subtests (`t.Run`), and the common `T` surface (`Error`/`Errorf`/`Fatal`/
`Fatalf`/`Fail`/`FailNow`/`Log`/`Logf`/`Skip`/`Skipf`/`SkipNow`/`Helper`/`Name`/`Cleanup`/
`Parallel`/`Failed`/`Skipped`). Validated in `tests/gotest`.

Not yet supported (documented, not silent): benchmarks (`Benchmark*`), fuzzing
(`Fuzz*`), examples (`Example*` with `// Output:`), `TestMain(m *testing.M)`, and test
`-flags` (`-run`/`-v`/`-count`/…) — `goclr test` runs all tests with verbose-style output.
Log lines carry no `file:line:` prefix (goclr lacks per-call caller metadata), and
durations print as `0.00s`. `t.Parallel()` is a no-op (tests run sequentially).

## Misc

- `strings.EqualFold` full-Unicode folding and `unicode.SimpleFold` are not
  implemented (ASCII/common folds only). `strings.NewReplacer` and `strings.Title`
  are byte-exact (Replacer matches on UTF-8 bytes with Go's priority + empty-key
  rule; Title uses Go's `isSeparator`, so `_` is not a word boundary).
- `math/bits` int8/int16 are typed as int32 at the boundary; the `bits.*8/*16`
  helpers mask correctly but very unusual signedness edges may differ.
- `strconv.FormatFloat` supports all verbs byte-exactly (`f/e/E/g/G/b/x/X`),
  including hexadecimal-float `0x1.…p±dd` with shortest and fixed precision;
  `fmt`'s `%x`/`%X` of a float routes through the same path.
- `math/big.Float` is **double-backed** (53-bit), not arbitrary precision:
  `SetPrec`/high-precision arithmetic are unsupported. `String`, `Text(fmt,
  prec)`, and `fmt`'s `%v/%g/%e/%f/%G` are byte-exact for the common
  `big.NewFloat(float64)` case (which stores the exact float64). `big.Int` and
  `big.Rat` are exact, including `fmt`'s `%d/%b/%o/%x/%X` integer verbs on a
  `*big.Int` (arbitrary precision, with the #/+/space/width/zero-pad flags).
- `html.UnescapeString` is byte-exact: the full HTML5 named-reference table
  (with/without-semicolon), decimal/hex numeric references, and the
  Windows-1252 / U+FFFD fix-ups. `net/url.URL.Path` is percent-decoded like Go,
  with the raw encoding preserved in `RawPath`; `EscapedPath`/`String`/
  `RequestURI` reproduce it, and a malformed `%XX` yields the matching
  `parse "<raw>": invalid URL escape "%xx"` error. Each escape mode follows
  Go's reserved-character rules: `PathEscape` (keeps `$&+:=@`), `QueryEscape`
  (escapes all, space→`+`), the fragment (`RawFragment`, keeps all reserved),
  and userinfo (escapes `@/?:`); `String()` escapes the fragment.
- Goroutine scheduling order is the .NET thread pool's, not Go's scheduler — keep
  concurrent test output order-independent (as Go's map-range convention already
  requires).
