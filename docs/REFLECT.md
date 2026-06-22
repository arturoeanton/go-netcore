# reflect — runtime type descriptors

`reflect` is the most leveraged package in the Go ecosystem: `encoding/json`,
`go-playground/validator`, ORMs, template engines, and most serious libraries are
built on it. goclr therefore implements `reflect` as a dedicated piece, driven by
**compile-time type descriptors** rather than by inspecting a sample value at
runtime. With good `reflect` coverage, reflection-heavy third-party libraries compile
from source and run correctly — the compiler stays agnostic to any particular library.

## The model

Every Go type the program can reflect on gets a **type descriptor** (`GoTypeDesc`,
goclr's analogue of Go's `*rtype`) carrying its precise metadata:

| Field | Meaning |
|---|---|
| `Kind` | the exact `reflect.Kind` (sized integers distinguished: `uint8` ≠ `int32` ≠ `int`) |
| `Name`, `PkgPath` | `"User"`, `"main"` (empty for unnamed/composite types) |
| `Str` | the type string: `"int"`, `"[]string"`, `"map[string]int"`, `"main.User"` |
| `Elem`, `Key` | element/key descriptors (slice/array/ptr/map/chan) |
| `ArrayLen` | element count for `[N]T` |
| `Fields` | struct fields: `Name`, `Tag`, field-type descriptor, `Anonymous` |
| `Methods` | method-set names (an interface's requirements, or a concrete type's method set) |

Descriptors are **built at compile time** from `go/types` (which has the precise
information: tags, integer widths, named types, method sets) and registered with the
runtime at startup (`TypeReg`). Composite types reference their parts by id, lazily
linked through the registry, so recursive types register in any order.

`reflect.Type` and `reflect.Value` each carry a descriptor. So `Kind`, `Name`,
`String`, `NumField`, `Field`, `Elem`, `Key`, and the sized-integer kinds are all
**precise without a sample value** — even for an empty slice, a zero map, or a type
constructed at runtime.

## Static and dynamic paths

- **Static** — `reflect.TypeOf(x)` / `ValueOf(x)` carry the descriptor id of the
  argument's *static* type, computed at the call site (the compiler knows it). This
  is the common case (reflecting over a concrete value) and is fully precise.
- **Dynamic** — when the argument is an `interface{}`, the descriptor is recovered
  from the *value's* identity: a struct from its emitted CLR type, an identity-bearing
  named type from its typed-box id, or a boxed scalar from its representation.
  Descriptors are built for **every named and struct type in the program**, so a type
  reflected only dynamically (the way `json.Marshal`/`validator` reflect over
  `interface{}`) is still precise.

## What works (verified byte-identical to `go run`)

Conformance fixtures `375`–`378`, plus the validator/gin closure:

- **Type introspection** — `Kind`, `Name`, `String`, `NumField`, `Field` and
  `FieldByName` (with `StructField.Name`, `.Type` — incl. `.Type.Name()`/`.Kind()` for a
  basic field type — `.Tag.Get`/`.Tag.Lookup`, `.Anonymous`, `.PkgPath`), `Elem`, `Key`,
  `Len` — the surface validators/ORMs/serializers read (fixture 405).
- **Precise sized-integer kinds** — `uint8`/`int16`/`int32`/`float32`/… no longer
  collapse to `Int`/`Uint`/`Float64`. `reflect.Kind` stringifies in `fmt` (`struct`,
  not `25`); `reflect.Type` formats via `String()`.
- **Type construction** — `reflect.MapOf`, `SliceOf`, `PtrTo`/`PointerTo`, `ArrayOf`
  synthesize composite descriptors (including nesting, e.g. `MapOf(k, SliceOf(e))`).
- **Method set** — `NumMethod`, `Method`, `Implements`, `AssignableTo`,
  `ConvertibleTo`, `Comparable`, `PkgPath`.
- **Construction & values** — `Zero`, `New` (so `New(t).Elem().Set(...)` works),
  `MakeSlice`, `MakeMap`; the settable write path (`Set*`, `Field`, `Index`, `Elem`).

## Known limit

A **bare** unnamed sized scalar reflected **only** dynamically loses its width:
`reflect.TypeOf(interface{}(uint8(5))).Kind()` reports the wide bucket (`Uint`),
because a narrow scalar boxes to a .NET representation that does not carry its width.
This **does not affect struct-field reflection** — the dominant pattern (validator,
`encoding/json`, ORMs) — because a struct's descriptor carries each field's exact
type, so a `uint8` field reflects as `uint8` even dynamically. Named scalar types are
also exact via the typed box. The only complete fix would tag *every* scalar boxed
into an interface — overhead and risk on a very common operation for a rare benefit —
so it is deliberately not done. See [LIMITATIONS.md](LIMITATIONS.md).

## Where it lives

- Compile time: `internal/lower/lower_typedesc.go` (descriptor builder + emission),
  `internal/lower/lower_expr.go` (`reflect.TypeOf`/`ValueOf` carry the static id).
- Runtime: `runtime/stdlib/TypeDesc.cs` (`GoTypeDesc` + `TypeReg`),
  `runtime/stdlib/Reflect.cs` (the `reflect` surface), `runtime/stdlib/Fmt.cs`
  (`reflect.Type`/`reflect.Kind` formatting).
