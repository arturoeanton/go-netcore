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
- **`reflect.Type` is comparable** with `==` (interned per descriptor, so identical types
  from any producer compare equal and a `reflect.Type` works as a map key). `Value.Convert`
  carries the target type (incl. `int`→`string` as `string(rune)`) and `reflect.Indirect`
  keeps the qualified element type. `reflect.Zero`/`reflect.New(...).Elem()` build the zero
  of scalars, slices, maps **and structs** (a Go-zeroed CLR instance — numeric/bool 0/false,
  slices/maps/pointers nil, string fields `""`, recursing into nested struct fields).
  A **func type's signature** is reflectable: `NumIn`/`NumOut` and `In(i)`/`Out(i)` report
  the parameter/result types (the descriptor records them at startup), alongside the working
  `Value.Call`/`MethodByName`/`MakeFunc`. Fixture 746.

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
  correct — including a top-level pointer taken from a struct field (`&s.f`) or an
  array/slice element (`&a[i]`), which expand to `&{…}`/`&[…]`/`&map[…]` like Go (these
  alias their target through a getter rather than holding it, and fmt now resolves that).
- **`fmt` of a nil pointer / nil interface** matches Go's `fmtPointer` byte-for-byte:
  an untyped `nil` prints `<nil>` for `%v`/`%T` and `%!verb(<nil>)` for every other
  verb; a typed nil pointer (`var p *int`) prints `<nil>` for `%v`, its type name for
  `%T`, `0x0` for `%p`, the zero address `0` for the integer verbs `%b/%o/%d/%x/%X`
  (honoring `#`), `(*int)(nil)` for `%#v`, and `%!verb(*int=<nil>)` for the rest. (A
  *live* pointer's `%p`/`%d` address is non-deterministic and not byte-exact, as in any
  runtime.)
- **`json.Marshal` string escaping** matches Go byte-for-byte: `\b \f \n \r \t`
  short forms (`\u00XX` for other controls), `<`/`>`/`&` → `<`/`>`/
  `&` under the default HTML-escaping (off via `Encoder.SetEscapeHTML(false)`),
  and U+2028/U+2029 always escaped (they break JavaScript) regardless of that flag.
- **`json.Unmarshal` type-mismatch errors** match Go: `json: cannot unmarshal
  <jsonkind> into Go value of type <T>` at the top level, and `...into Go struct
  field <Struct>.<key-path> of type <T>` inside a struct (the descriptor carries
  the precise Go type name; the field path uses the innermost struct + JSON keys).
  **`json` *syntax*-error messages differ** (malformed JSON surfaces the
  underlying .NET reader text, not Go's `invalid character … / unexpected end of
  JSON input`); the error is still non-nil, so `err != nil` checks behave the same.
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
- **`json.Marshal` of a *user* `json.Marshaler`/`encoding.TextMarshaler`** is honored,
  like Go: a type with its own `MarshalJSON` controls its JSON; failing that, a
  `MarshalText` type emits a quoted string (and is used for map keys). Works at the top
  level, as a struct field, a slice/array element, a map value, a `*T` (pointer-receiver
  marshaler) and a `TextMarshaler` map key. The method is driven through the callback
  bridge (`encoding/json.Marshaler`/`encoding.TextMarshaler` are registered bridge
  interfaces); a bare field/element value is re-tagged with its static type id (the
  field-type / composite-element registry) so the named identity it lost in a type-erased
  container is recovered. The `MarshalJSON` bytes are whitespace-compacted before
  embedding; a returned error propagates as `json: error calling MarshalJSON for type T`.
  Fixture 677_json_marshaler. (Deferred edge: the compaction does not re-escape raw
  `<`/`>`/`&` inside a marshaler's output, which Go's HTML-escaping compaction does.)
- **`json.Unmarshal` into a *user* `json.Unmarshaler`/`encoding.TextUnmarshaler`** is
  honored, like Go: a type with its own `UnmarshalJSON([]byte) error` decodes via that
  method; failing that, `UnmarshalText([]byte) error` receives the unquoted string. Works
  at the top level, as a struct field, slice element, map value, and a `*T` pointer (a nil
  pointer target receives an allocated value). The compiler emits a descriptor marker +
  the type's runtime id; the decoder builds a settable receiver and drives the method
  through the callback bridge, capturing the raw JSON token (or unquoted string). A
  returned error becomes `json.Unmarshal`'s error. The method may call `json.Unmarshal`
  re-entrantly (the alias trick) — the mismatch-context state is saved/restored across
  calls. Fixture 678_json_unmarshaler.

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
- A named Stringer/Error value as a **struct field** (named or anonymous) or a
  **slice/map element** (`[]Money`, `Reading.Temp`, `struct{ S Stringy }{…}`)
  dispatches its `String()` under `%v`/`%+v` — fmt re-tags via the field-type /
  composite-element registry, including a Stringer type used *only* as a field.
- `%T`/`%#v` of a **method-less** named *scalar* type still prints the underlying
  representation (only method-bearing named types get an identity tag so far). A
  composite over a named interface element (`[]error`, `map[error]int`) now names
  it correctly; only the empty interface erases to `interface {}` (as in Go).
- The **Stringer/error rule extends to `%x`/`%X`/`%q`** (as in Go, which applies it to
  `%v %s %q %x %X`): a value with `String()`/`Error()` formats its string form, which is
  then hex-encoded (`%x` of a `Color` Stringer → the hex of `"Green"`) or quoted. The
  Go-syntax flag (`%#x`) and a raw string/`[]byte` opt out (still hexed as bytes).
- **`%T` renders the builtin aliases via reflect**: in a composite, `byte`→`uint8` and
  `rune`→`int32` (`%T` of `[]byte` → `[]uint8`, `map[byte]rune` → `map[uint8]int32`).
  Two residuals remain (both the typed-box scalar gap): `%T` of a **bare `byte`/`rune`
  scalar** prints `int32` (a narrow scalar boxes to a .NET int32, losing byte/rune
  identity), and `%T` of a **channel** prints the runtime class (`main.GoChan`) rather
  than `chan uint8` — channels carry no reflect descriptor yet.
- A **type alias** to an identity-bearing named type (`os.FileMode =
  io/fs.FileMode`) is unaliased for identity, so `%T`/`%v`/`%s`, methods,
  constants, bit-ops and struct fields behave as the underlying named type.
- `%#v` (Go-syntax) is byte-exact: unsigned ints in hex (`0x5`), `[]byte`
  elements as hex bytes, a nil typed pointer as `(*int)(nil)`, and an anonymous
  struct by its reflect spelling (`struct { A int; B string }`) for both `%#v`
  and `%T`. (`%#v` of a `uint8`/`byte` *scalar* still prints decimal — it shares
  the int32 runtime representation.)
- **`fmt.GoStringer`** is honored: a user type with a `GoString() string` method
  controls its `%#v` (Go-syntax) rendering — at the top level, through a pointer, and
  nested inside a struct/slice/map (driven through the callback bridge). `%v`/`%s` are
  unaffected. Fixture 679_fmt_gostringer.
- **`fmt.Formatter`** is honored: a user type with `Format(f fmt.State, verb rune)` controls
  every verb's rendering. The `fmt.State` it receives (a `GoFmtState` shim) captures output —
  `fmt.Fprintf(f, …)` writes to it via the `IGoWriter` path — and reports the verb's
  `Width()`/`Precision()`/`Flag()`. Honored at the top level, through a pointer receiver, and
  per element inside a slice/map; the Formatter owns its own width padding. Fixture
  680_fmt_formatter.
- **`*big.Int` honors the `+` flag under `%v`/`%+v`** (it is itself an `fmt.Formatter`,
  so `%+v` of a non-negative value prints a leading `+`) — standalone and as a
  struct/slice/map field. Fixture 726_bigint_plus_verb. Two narrow residuals remain:
  the **space flag** (`% v` of a `big.Int`, a leading space) and the **`+` flag on a
  standalone `*big.Float` under `%v`** (it is unwrapped to its `float64` value before
  the flag is applied) are not yet honored — `%+d`/`% d` and the `*big.Int` field cases
  are exact.
- **`*big.Int`/`*big.Float` verb sets match their `Format` methods.** `big.Int` supports
  `%b %o %O %d %s %v %x %X`; `big.Float` supports `%e %E %f %F %g %G %x %b %v`. `big.Float`'s
  `%x` defaults to **6 hex-mantissa digits** (`0x1.c00000p+01`, not a `float64`'s shortest
  form), and `%X` is a **bad verb** (it has no uppercase-hex case). Any verb outside the set
  bad-verbs as `%!v(big.Int=<dec>)` / `%!v(*big.Float=<Text('g',10)>)`, naming the Go type.
  Fixture 729.
- **The `%O` verb** (Go 1.13+ `0o`-prefixed octal) is honored for every integer type,
  including `*big.Int` (`%O` of 255 → `0o377`, of 0 → `0o0`, with sign/width/`+`/`-`).
- A non-numeric verb's **width applied to a composite** (`%6v` of a `[]int`)
  pads the whole rendering rather than each element; Go pads per element.
  Numeric verbs (`%6d`, `%03d`) already pad per element. `%!(EXTRA …)` for
  surplus args and `%q` precision (rune truncation) match Go. The `%q` flags are
  honored too: `%#q` prefers a raw-string (back-quote) literal when the value can be
  back-quoted (else double-quotes), and `%+q` escapes all non-ASCII (`QuoteToASCII` /
  `QuoteRuneToASCII`), through strings, runes, `[]byte`, slices and maps. Fixture 741.
  `%#x`/`%#X` of a string or `[]byte` add the `0x`/`0X` prefix (once, or per byte under
  ` ` like Go's `0xde 0xad`), and `%G` uppercases the shortest-form exponent (`1E-05`).
  `%#g`/`%#G` keep trailing zeros to N significant figures (default 6: `1.00000`, `0.00000`
  with the leading-zero special case), across the fixed and exponent forms and with an
  explicit precision. Fixtures 742, 743.
- **Integer precision** sets the minimum number of digits (zero-padded), distinct
  from width: `%.3d` of 5 → `005`, `%.5x` of 255 → `000ff`, `%#.4o` of 8 → `0010`
  (the `#` prefix is applied after the precision pad), `%.0d` of 0 → empty. The `0`
  flag is ignored when a precision is given (`%08.3d` of 5 → `␣␣␣␣␣005`). Honored via
  `.N`, `.*`, and `.[i]*`, per element in a composite, and for `*big.Int`. Fixture 730.

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

The `math` package maps some transcendental functions (`Log10`, `Sin`, `Cos`, `Tan`, …)
onto the platform's `System.Math`, which can differ from Go's own implementations by the
last ULP for some inputs (e.g. `math.Sin(2)`, `math.Tan(1.5)`, `math.Cos(1e8)` where a
huge argument needs Payne–Hanek reduction, `math.Erfc(10)` in the far tail). Porting Go's
own `sin`/`cos`/`tan` source does **not** remove this for the trig functions: the residual
is a back-end floating-point codegen difference (the .NET ARM64 JIT fuses some `a*b+c`
into an FMA where Go's arm64 codegen rounds differently), which also shows up in pure
lowered-Go polynomial evaluation, so it cannot be fixed at the shim layer. Functions
built on them inherit that last-ULP edge on the affected inputs; the value is correct to
~1 ULP. `math.Exp`, `math.Exp2`, `math.Cbrt` are faithful fdlibm/Go ports and are
byte-exact across a broad sweep (0 divergences in 3000 inputs for `Exp`; the platform
`exp` differed for ~10%). `math.Sinh`, `math.Cosh`, `math.Tanh` build on the ported `Exp`
and are likewise byte-exact across the sweep (0 divergences — previously ≈9% / ≈1.4% via
`System.Math`). `math.Expm1`, `math.Log1p`, `math.Log`, `math.Atanh`, `math.Gamma`,
`math.Lgamma`, `math.Erf`, `math.Erfinv` are fdlibm ports that are byte-exact on their
fixtures and for the large majority of inputs, with a **small fraction** (≈0.1–0.5%) still
off by the last ULP — the back-end FMA-codegen residual, not an algorithm error (e.g.
`Exp(-13.38)`, and `Exp(-745)` rounds the smallest subnormal where Go flushes to 0).
`Asinh`/`Acosh` go through `System.Math` and match `go run` on the tested inputs.
`math.Pow`, `math.Log10` are Go-source ports built on the byte-exact `Exp`/`Log`/`Frexp`:
≈0.3% / ≈0.7% residual (down from ~44% / ~39% via `System.Math`), and Pow's full
special-case lattice is exact. `math.Log2` is also ported but its final `Log(frac)·(1/Ln2) +
exp` is an `a*b+c` that the back-end fuses differently from Go, so it keeps a ≈16% last-ULP
residual (still below `System.Math`'s ~36%); exact powers of two are exact. Fixtures 732,
733, 735, 736.
The Bessel functions `math.J0`/`J1`/`Y0`/`Y1`/`Jn`/`Yn` are now fdlibm ports too:
**byte-exact for `J0`/`J1` with `|x| < 2`** (pure polynomial) and all the special cases
(`0`/`±Inf`/`NaN`, order/sign relations, the tiny-argument `Jn` Taylor branch). For
`|x| >= 2` the asymptotic branch calls `Sin`/`Cos`, and `Y0`/`Y1` call `Log`, so those
inherit the same last-ULP trig/log edge as `math.Sin`/`Cos`/`Log` above (correct to
~1 ULP, ~15 significant digits).

`math/cmplx` is now complete — `Exp`/`Rect`/`Pow`/`Sin`/`Cos`/`Tan`/`Cot`/`Sinh`/`Cosh`/
`Tanh` and the inverse trig/hyperbolic (`Asin`/`Acos`/`Atan`/`Asinh`/`Acosh`/`Atanh`) are
faithful ports of Go's `cmplx` package. Their **special cases are exact** (`0`/`±Inf`/`NaN`,
sign conventions, the `Pow(0, y)` table), and transcendental values are correct to ~1 ULP:
they are built from the real `Sin`/`Cos`/`Exp`/`Log`/`Asin`/`Atanh` primitives, which match
Go to within the last ULP on this platform (`math.Asin`/`Atanh`/`Sin`/`Cos` already do).

`math/rand` with an explicit source — `rand.New(rand.NewSource(seed))` and all its methods
(`Int`/`Intn`/`Int63`/`Uint64`/`Float64`/`NormFloat64`/`ExpFloat64`/`Perm`/`Shuffle`/`Read`,
…) — is byte-exact with Go (Go's PRNG is ported). The **top-level** `rand.Seed`/`rand.Intn`
(the global source) does **not** reproduce Go 1.20+'s exact sequence: Go's deprecated global
`Seed` switches to an internal compatibility mode whose output is context-dependent (it
varies with prior global calls), whereas goclr's `Seed(s)` behaves like `New(NewSource(s))`.
Programs needing a reproducible sequence should use `rand.New(rand.NewSource(seed))`, as Go's
own docs recommend.

`hash/maphash` is likewise **not byte-exact in absolute value** — Go seeds each hash randomly,
so `Sum64`/`String`/`Bytes` differ run-to-run in Go itself. The full surface is implemented
(`MakeSeed`, `String`, `Bytes`, `(*Hash).SetSeed`/`Seed`/`Write*`/`Sum64`/`Reset`) and is
internally consistent — same seed + same bytes give the same hash, `SetSeed`+`Write` matches
the one-shot `String(seed, …)`, and distinct seeds (almost) never collide — but the backing
algorithm is a seeded FNV-1a, not Go's exact maphash, so only those relative properties hold.

## html/template contextual auto-escaping

`html/template` escapes interpolated values by context — HTML text, attribute
(quoted/unquoted), URL (with the path-vs-query distinction and the `isSafeURL`
http/https/mailto scheme allowlist → `#ZgotmplZ` for others), CSS, JS value and JS
string — matching Go including the `ZgotmplZ` neutralization of a dynamic attribute
**name** and dangerous CSS, and Go's `jsStrReplacementTable`/url-normalizer byte-for-byte.
HTML comments (`<!-- … -->`) are elided from the output (including any actions inside them,
even across a text/action boundary), while a `<!DOCTYPE …>` declaration is preserved and a
later contextual action still gets the right escaper. (text/template keeps comments verbatim.)

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

## time zones — DST-aware IANA + fixed-offset

`time.FixedZone`, `time.UTC`, and DST-aware IANA zones from `time.LoadLocation`
are supported: `time.Date(..., loc)` interprets the wall-clock fields in that
zone, `Format` renders the offset (`-0700`/`Z07:00`) and abbreviation (`MST`
token), and `UTC`/`In`/`Zone`/`Location` convert and report it; `Add`/`AddDate`/
`Truncate`/`Round` preserve the zone (and `AddDate` re-resolves DST at the new
date), and the instant (`Unix`) is zone-independent. **DST transitions ARE now
modeled** — `LoadLocation("America/New_York")` resolves `-0400 EDT` in July and
`-0500 EST` in January via the system zoneinfo, so offsets and cross-zone `Sub`
are correct year-round. Two residual edges: (1) the zone *abbreviation* is
derived from .NET's English zone name (whose uppercase initials equal the IANA
abbreviation for the US/EU/common zones — EDT, PST, CST, BST, GMT, JST, IST,
AEST/AEDT — but a few zones whose IANA abbreviation is not the English initials,
e.g. `Europe/Moscow` → `MSK`, will differ); (2) the exact wall-clock chosen
inside a spring-forward/fall-back transition hour may differ from Go's rule.
Go's `time.Now()`/`time.Local` use UTC (no local zone in the runtime). `Parse`
reads a zone offset from the input (`Z07:00`/`-0700`) into the value, and
`ParseInLocation` interprets a zoneless layout in the given location (at the
location's base offset).

`Format`/`Parse` accept fractional-second layout tokens of **any** width — a `.`
or `,` separator followed by a run of `0`s (fixed width, trailing zeros kept) or
`9`s (trailing zeros trimmed, separator dropped when empty) — not just the canonical
`.000`/`.000000000`/`.999999999` forms (`.9`, `.99`, `.000000`, `05,000`, … all work).

The **day-of-year** layout tokens are honored on both sides: `002` (zero-padded,
width 3 — `001`..`366`) and `__2` (space-padded, width 3 — `  1`..`366`). `Parse`
reconstructs the calendar month/day from the year and day-of-year, and rejects a
value past the year's length (`366` in a non-leap year → `day-of-year out of range`).

`Parse` error messages match Go: a failed layout token reports the unparsed value
remainder and the expected token (`cannot parse "9z" as "02"`), an out-of-range numeric
field reports `parsing time "…": <field> out of range` (month/day/hour/minute/second,
validated inline as Go does), a zero-padded token (`01`/`02`/`03`/`04`/`05`/`06`/`2006`)
requires exactly its width of digits (the non-padded `1`/`2`/`3`/`4`/`5`/`15`/`_2` forms
accept one), and trailing input is reported as `extra text: "…"`. Fixture
682_time_parse_errors. **Deferred:** a literal-separator mismatch still names the whole
remaining layout rather than just the literal chunk.

goclr's `time.Time` is backed by .NET's `DateTime` (year **1–9999**), so dates outside
that range aren't representable: a `Parse` with **no date fields** (a time-only layout such
as `time.Kitchen`) defaults the date to `1970-01-01` rather than Go's `0000-01-01` (the
parsed clock fields are correct). Years before 1 / after 9999 and Go's exact zero-date are
the documented gap; within the common range everything is byte-exact.

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
**`database/sql` + `database/sql/driver`** (with the `go-r2-sqlite` engine),
**`mime/multipart`** (form parsing), and **`image` + `image/color`** (compiled from real
source — the color types/Models/conversions, plus the Rectangle/Point geometry and the
buffered image types RGBA/NRGBA/Gray/Paletted/YCbCr with SubImage/Palette and the decoder
registry, all byte-exact; `image.Decode` returns `ErrFormat` since no format decoder
(png/jpeg/gif) is registered), and **`image/draw`** (Draw/DrawMask with the Src/Over
operators, Uniform sources, alpha masks and the blending math). Still deferred (need a
larger feature or external module):

- **`encoding/gob`** — not implemented (`gob.NewEncoder`/`NewDecoder` are unsupported): the
  self-describing binary format is a large reflection-driven codec; use `encoding/json` (or
  `encoding/binary` for fixed layouts) instead.
- **`math/rand/v2`** — works with both the **PCG** (`rand.NewPCG`) and **ChaCha8**
  (`rand.NewChaCha8`) sources, byte-exact (the latter a faithful port of `internal/
  chacha8rand`'s block + refill/reseed), plus the auto-seeded global functions.
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
  dispatch gap; a user-defined in-memory `fs.FS` works.) `os.Stat`/`ReadDir` now report the
  Unix permission bits via `FileInfo.Mode()` and a real `ModTime()`, but a **directory's
  `Size()` is 0** (the OS block size — 64 on macOS, 4096 on Linux ext4 — isn't read; it is
  platform-specific and not portable even in Go). Regular-file sizes are exact.
- **`path/filepath.Glob`** — works: returns the files matching a `Match`-syntax pattern
  (`*`, `?`, `[…]`, recursive `*/*.go`), sorted within each directory; file-system errors
  are ignored (a missing/unreadable directory yields no matches), a non-meta pattern checks
  existence, no matches returns a nil slice, and a malformed pattern returns `ErrBadPattern`.
  Fixture 731. (`filepath.Walk` is still a no-op that returns nil without walking, and
  `WalkDir` is unsupported — both need to invoke a Go walk callback from the shim, the same
  callback-bridge gap noted for a few other APIs.)
- **`x/sync/errgroup`** — works: compiles from source and runs (concurrent goroutines +
  first-error propagation), now that `context.WithCancelCause`/`context.Cause` are shimmed.
  See `examples/demo_errgroup` (requires `go mod vendor`). Cancellation now cascades to
  descendants: cancelling (or timing out) an ancestor closes the `Done()` channel of every
  cancelable child/grandchild — across intervening `WithValue` layers — so `<-child.Done()`
  unblocks, while `WithoutCancel` severs the chain. Fixture 676_context_cancel_propagation.
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
  reflection-based and cover element/attr/chardata/cdata/comment/innerxml/omitempty tags,
  `XMLName`, and nested-element paths (`xml:"a>b>c"`, with consecutive fields sharing a
  prefix merged into one wrapper; a comment/cdata/chardata field closes any open wrapper
  first). `,comment` emits `<!--…-->` and `,cdata` emits `<![CDATA[…]]>` (kept inline by
  `MarshalIndent`, like Go). `xml.Name`/`xml.Attr`/… are `[GoShim]`-tagged so `%v`/`%+v`/
  `%#v`/`%T` name them as their Go type. `xml.Unmarshal`/`Decoder.Decode` return an honest
  error (`xml: decoding is not supported under goclr`) and `Decoder.Token` reports `io.EOF`
  immediately (an empty token stream) — a reflection-driven decoder / tokenizer is the
  larger deferred piece.
- **`crypto/ed25519`** — fully working via a pure RFC 8032 implementation (the .NET BCL has
  no Ed25519): `NewKeyFromSeed`, `Sign`, `Verify`, `GenerateKey`, and the `PrivateKey`
  `Public`/`Seed`/`Sign` methods. Deterministic, so the derived public key and signatures are
  byte-exact with `go run`. Fixture 750. (One edge: a `priv.Public().(ed25519.PublicKey)`
  assertion through the `crypto.PublicKey` interface isn't supported — use `ed25519.PublicKey(
  priv[32:])` or `GenerateKey`'s typed return instead.)
- **asymmetric crypto** — `crypto/ecdsa` (`GenerateKey`/`Sign`/`Verify` on P-256/P-384/P-521)
  and `crypto/rsa` `SignPKCS1v15`/`VerifyPKCS1v15` (SHA-1/256/384/512) now work, backed by the
  same real .NET key handles `crypto/x509` produces — so JWT ES*/RS* and PKCS1v15 signatures
  round-trip and verify. **RSA-PSS** (`SignPSS`/`VerifyPSS`) now works too, via a from-scratch
  EMSA-PSS (RFC 8017 §9.1: MGF1 + the EM layout) over the raw RSA primitive with `BigInteger`
  — the .NET BCL only offers PSS with salt-length == hash-length, so this hand-rolled padding is
  what lets goclr honour Go's `PSSOptions.SaltLength` across all three modes (Auto = max salt,
  `PSSSaltLengthEqualsHash` = −1, and an explicit count) with the same cross-mode accept/reject
  rules as `go run` (Auto verification recovers the salt length; a fixed verifier rejects a
  different salt length). Fixture 751. Still **fail-closed** (an honest error / `false`, never a
  bogus accept): `SignPKCS1v15` with `crypto.Hash(0)` (raw, no DigestInfo), and DER public-key
  parsing (`x509.ParsePKIXPublicKey`/`ParsePKCS1PublicKey`). `crypto/hmac` (HS*) was already
  real. Signatures are non-deterministic (random key, a random nonce for ECDSA, and a random
  salt for PSS), so only verify outcomes are byte-stable across runs.
- **`crypto/elliptic`** — `Curve.Params()` returns the real NIST domain parameters (FIPS
  186-4): `Name`, `BitSize`, and the `P`/`N`/`B`/`Gx`/`Gy` `*big.Int` constants are byte-exact
  with `go run` for P-224/P-256/P-384/P-521, and the curve recovered from an `ecdsa` key now
  reflects its real size (it previously always reported P-256, and `Params()` itself was an
  unregistered nil-deref). Fixture 753. The point arithmetic — `IsOnCurve`, `Add`, `Double`,
  `ScalarMult`, `ScalarBaseMult` — is implemented in affine coordinates over the prime field
  (short Weierstrass, `a = -3`, point at infinity as `(0,0)`) with `BigInteger`, byte-exact with
  `go run` for all four curves. Fixture 754. Caveat: the .NET BCL has no `nistP224`, so **P-224
  key generation is unsupported** (its `Params()` and point arithmetic are still correct).

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

### Type assertions over opaque shim handles are precise

A type assertion to an opaque shim type — `x.(*rsa.PublicKey)`, `tok.(xml.StartElement)`,
the comma-ok and panic forms alike — now discriminates by the concrete `[GoShim]` CLR class
(`Rt.IsShimKind`), exactly like a type switch already did. Because every shim handle lowers to
`System.Object`, a plain `isinst` would have matched *any* boxed value: a `*rsa.PublicKey`
wrongly satisfied `x.(*ecdsa.PublicKey)`. The registry comparison normalizes both the queried
name and the registered `[GoShim]` name to Go's short `pkg.Type` form, so it is robust to the
attribute being written either short (`xml.StartElement`) or as a full import path
(`crypto/rsa.PublicKey`). Fixture 752. (Assertions to a *named non-shim* type — e.g.
`x.(ed25519.PublicKey)`, a named `[]byte` — go through the typed-box tag path instead and have
their own limitation, documented above under crypto/ed25519.)

### Shimmed-package error types in a type switch

`encoding/json`/`encoding/xml` are shimmed, and their error types
(`*json.SyntaxError`, `*json.UnmarshalTypeError`, `*xml.SyntaxError`,
`*xml.UnsupportedTypeError`) are opaque shim handles so a `case *json.SyntaxError:` and
the field/method reads inside it (`.Offset`, `.Error()`, …) compile (echo's binder uses
them). **`json.Unmarshal` now returns a real `*json.SyntaxError`** for malformed JSON
(with a positive byte `Offset`, `0` for empty input via "unexpected end of JSON input"),
so `errors.As(err, &*json.SyntaxError)` and the `.Offset` read work. Still **not** typed:
`*json.UnmarshalTypeError` — a type-mismatch decode returns a generic `GoError` (the
message is correct, but `errors.As(*json.UnmarshalTypeError)` is false), because its
`Type reflect.Type` field needs a runtime type descriptor built from the decode target
(the deep-reflect item). `*xml.SyntaxError` similarly stays a generic error.

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
- `slices`/`cmp` are supported, including functions that return a type
  parameter — the backend unboxes a generic shim's boxed result to the call's
  instantiated type. Covers `Sort`/`SortFunc`/`SortStableFunc`, `Contains(Func)`,
  `Index(Func)`, `Max`/`Min`/`MaxFunc`/`MinFunc`, `Equal(Func)`, `Reverse`,
  `IsSorted(Func)`, `BinarySearch(Func)`, `Clone`/`Compact(Func)`/`Concat`,
  `Insert`/`Delete`/`Replace`/`DeleteFunc`/`Repeat`, `Compare(Func)`, and
  `cmp.Compare`/`Less`/`Or`. The `maps` package provides `Clone`, `Copy`,
  `Equal`, `EqualFunc`, `DeleteFunc`. The **iterator (`iter.Seq`) functions are
  also supported** via range-over-func: `slices.Values`/`All`/`Backward`/
  `Collect`/`Sorted`/`SortedFunc` and `maps.Keys`/`Values`/`All` (map iteration
  order is unspecified, like Go).
- `strconv.FormatFloat` supports all verbs byte-exactly (`f/e/E/g/G/b/x/X`),
  including hexadecimal-float `0x1.…p±dd` with shortest and fixed precision;
  `fmt`'s `%x`/`%X`/`%b` of a float route through the same path. A `bitSize` of 32
  with shortest precision (`prec<0`) uses the float32 round-trip (so `FormatFloat(…,
  'g', -1, 32)` of a float32 `0.1` is `0.1`, not the widened `0.10000000149011612`).
  A `complex` formats both parts as `(re±imi)` under `%v`/`%f`/`%e`/`%E`/`%g`/`%G`/`%#v`.
  **Edge:** the shortest **`'f'`** form of the float64 extremes — `math.MaxFloat64` and
  `math.SmallestNonzeroFloat64` — differs (a last-digit rounding / underflow-to-`0` in the
  ~300-digit fixed expansion); the `'g'`/`'e'` forms of those values are exact.
- `math/big.Float` is **double-backed** (53-bit), not arbitrary precision. The arithmetic
  and setter methods are present (`Add`/`Sub`/`Mul`/`Quo`/`Neg`/`Abs`/`Set`/`Copy`/
  `SetFloat64`/`SetInt64`/`SetInt`/`Float64`/`Cmp`/`Sign`/`IsInt`/`String`/`Text`), and
  `SetPrec`/`SetMode` are accepted as no-ops (`Prec`/`MinPrec` report 53) — a computation
  that genuinely needs more than float64 precision is the documented gap. `String`,
  `Text(fmt, prec)`, and `fmt`'s `%v/%g/%e/%f/%G` are byte-exact for the common
  `big.NewFloat(float64)` / within-float64 case (which stores the exact float64). `big.Int` and
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
  `ParseQuery` records the first error (bad `%XX` or a `;` separator) but still
  parses the remaining valid pairs; `Query()` discards the error like Go.
- Goroutine scheduling order is the .NET thread pool's, not Go's scheduler — keep
  concurrent test output order-independent (as Go's map-range convention already
  requires).
