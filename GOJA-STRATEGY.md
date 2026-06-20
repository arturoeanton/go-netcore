# Goja & unsafe.Pointer — strategy (M3)

`goclr analyze ./cmd/server` shows the goja+Echo target has **exactly three hard
blockers**, all `unsafe.Pointer` (GCLR0201). Everything else in the closure
compiles or is a benign "overlay not yet provided" warning.

## The three blockers

| Package | File(s) | What the unsafe does |
|---|---|---|
| `github.com/dop251/goja` | `typedarrays.go`, `builtin_typedarrays.go` (~18 sites) | reinterpret a `[]byte` ArrayBuffer as `[]uint8/int16/float64/…` (TypedArray views) and pointer-index it |
| `github.com/dop251/goja/unistring` | `unistring.go` | reinterpret a `string` as `[]uint16` (and back) for UTF-16 JS string storage |
| `github.com/dlclark/regexp2/v2/helpers` | `indexof.go` | reinterpret `[]byte`↔`[]int32` for a fast IndexOf |

The patterns are all the same shape:
```go
*(*uint64)(unsafe.Pointer(&b[0]))            // read 8 bytes as uint64 (native endian)
*(*uint64)(unsafe.Pointer(&o.data[idx])) = v // write uint64 as 8 bytes
(*uint8)(unsafe.Add(unsafe.Pointer(p), idx)) // index a typed view
```

## Strategy: overlay + managed byte conversion (no unsafe)

These are **byte ⟷ numeric reinterpretations**, which have an exact, safe managed
equivalent — and goclr already ships it: the **`encoding/binary`** shim
(`LittleEndian/BigEndian` + `Uint16/32/64` + `PutUint16/32/64`, verified in fixture
302). So:

```go
*(*uint64)(unsafe.Pointer(&b[0]))   →   binary.NativeEndian.Uint64(b)
*(*uint64)(unsafe.Pointer(&p))= v   →   binary.NativeEndian.PutUint64(b, v)
typedView[idx]                      →   a managed view struct over the []byte
```

A TypedArray view becomes a small managed struct `{buf []byte; off int}` whose
`get(i)`/`set(i,v)` call `binary` — no pointer reinterpretation, identical bytes.

The delivery vehicle is the **overlay-resolution mechanism** (ROADMAP-M2.5 §0.1):
the frontend loads with the `goclr` build tag and a `go/packages` `-overlay` JSON
map that replaces the three offending files with goclr-safe versions kept in this
repo under `overlay/`:

```
overlay/github.com/dop251/goja/typedarrays_goclr.go        (//go:build goclr)
overlay/github.com/dop251/goja/unistring/unistring_goclr.go
overlay/github.com/dlclark/regexp2/v2/helpers/indexof_goclr.go
```

The originals are excluded for the `goclr` build (a `//go:build !goclr` tag on the
upstream file via the overlay, or a whole-file content replacement through
`-overlay`).

## Why this is bounded (not open-ended)

- The **primitive is done**: `encoding/binary` shim already produces byte-exact
  reinterpretation.
- The **blocker set is closed**: 3 files, ~20 unsafe sites, all the same pattern.
- The rest of goja (`ast`, `parser`, `token`, `ftoa`, `file`) already shows **OK**
  in `analyze` — pure Go, compiles directly once the backend handles its language
  features.

## Remaining work to actually run goja (M3)

1. **Overlay mechanism** — frontend: honor a `goclr` build tag + build the
   `-overlay` map from `overlay/` (the one missing foundation, ~frontend change).
2. **Three safe overlays** — rewrite the unsafe sites with `encoding/binary` +
   managed view structs (mechanical; the hardest is `typedarrays.go`, ~1100 lines
   but repetitive).
3. **Compile the goja closure** — exercise the backend over goja's reflect-heavy
   evaluator; the read+write reflect path (done in M2.5 P0) is the prerequisite,
   already in place.
4. **Echo** — separately, compile `labstack/echo/v4` (pure Go on net/http; the
   net/http client+server shims are done).

## Bottom line

The unsafe.Pointer question has a concrete, low-risk answer: **replace the
reinterpret-casts with the `encoding/binary` shim through goclr build-tagged
overlays.** The primitive exists and is verified; what's left is the overlay
mechanism plus three mechanical file rewrites, then driving the backend over the
(pure-Go) goja evaluator. That is the M3 milestone.

---

## STATUS UPDATE — the unsafe path is done; the wall is the runtime type system

Everything in the section above is **complete and verified**:

- **Overlay mechanism — DONE.** `internal/frontend/overlays.go` applies goclr-safe
  replacements to the **vendored** copies of the unsafe files (`go mod vendor` +
  `ApplyOverlays`), plus a virtual `go/packages` overlay (`StdlibOverlay`) for
  stdlib source. The `goclr,clr,net8` build tags are on by default.
- **Three safe overlays — DONE.** `typedarrays.go`, `builtin_typedarrays.go`,
  `unistring/string.go`, `regexp2helpers/indexof.go`, and goja `value.go` are
  rewritten with `encoding/binary` byte access. goja now **runs correctly under
  `go run`** with these overlays (typed arrays, DataView, UTF-16, JSON all exact).
- **`goclr analyze ./tests/validation/goja` → OK.** The unsafe blockers are gone.
- Driving the **backend** over goja surfaced and fixed a long series of real
  language gaps (now in `main`): fixed arrays, int8/16/32, `unsafe.Pointer`→object,
  `&slice[i]`, `&^`, keyed array/slice literals, **long-form local opcodes**
  (256+ locals), **chunked package-var init** (64KB IL limit), `unicode` compiled
  from real source, **cross-package interface dispatch**, and `sort` compiled from
  a reflectlite-free overlay (interface `Sort`/`Stable`/`Search`/`Find`).

### The actual remaining blocker: a runtime type system (still M3)

goja compilation through the backend now stops on two things that are the **same
root cause** — goclr erases per-value runtime type identity:

1. **`reflect`** (the big one). goja's Go↔JS interop is built on `reflect.ValueOf`,
   `MakeSlice`, `MakeMap`, `New`, and field/method access by name across ~25 files.
   goclr compiles *every* function in a package, so these are reached even by a bare
   `vm.RunString("1+2")`. A faithful `reflect` needs runtime type descriptors.
2. **Representation-keyed interface dispatch.** `sort.StringSlice` (pulled in by
   goja's `golang.org/x/text/collate` dependency) and any other named-slice
   `sort.Interface` implementer box to one indistinguishable `GoSlice`. See
   LIMITATIONS.md → "Interface dispatch keys on the boxed representation."

Both are dissolved by the same M3 feature: **per-value type tags / an itable**, so
that (a) an interface value carries its concrete type, and (b) `reflect` can read
it. (**Resolved** — this was implemented; see the final status section below. goja
now evaluates JavaScript.) The feature was deliberately not rushed — a half-
implemented `reflect` would be silent, dangerous tech debt (the "sin deuda técnica"
rule).

**Recommendation for M3:** add a `TypeId` (and a small type-descriptor table) to
*every* boxed value, not just `GoPtr`; route interface dispatch and a new `reflect`
shim through it. That single foundation unblocks goja, precise `%T`/`%#v`, nil-map
formatting, and multi-named-slice interface dispatch at once.

---

## STATUS UPDATE — goja runs JavaScript (the recommendation above was carried out)

The **typed box** (`docs/DESIGN-typed-box.md`) gives every named non-struct value a
runtime type identity (`GoNamed{TypeId, Value}`); interface dispatch, `==`, `fmt`,
and a sample-based `reflect` overlay recover it. With that keystone in place, goja
went all the way from "compiles partway" to **evaluating JavaScript**: `vm.New()`
→ full package init → parse → compile → run. `RunString("1+2")` returns `3`;
`1+2*3` → 7; string concat, string methods (`toUpperCase`/`slice`), `Math`, objects
and property access, function calls/closures, and `for`/`while` loops all evaluate
with output identical to `go run`. See `examples/demo_goja` and `GAPS.md`.

Getting there required, on top of the typed box and the safe unsafe-overlays, a long
series of general codegen/runtime fixes (each landed with a conformance fixture).
The load-bearing ones:

- **4-byte metadata heap indices (`HeapSizes=0x07`).** The emitter assumed
  2-byte heap references, valid only while every metadata heap stays under 64 KiB.
  A program as large as goja overflows them, so once a heap crossed 64 KiB every
  signature/name reference was misread (`TypeLoadException`). Foundational for any
  large program, not just goja.
- **Typed-box identity preserved across slices/interfaces.** A named value stored
  into an interface-element slice (`code[pc] = jne(target)`, the bytecode the
  compiler backpatches) kept its type tag — the shared root cause of goja's loops
  *and* arrays failing.
- **Slice capacity region holds element zero values** (`make`/`append`), so code that
  reslices into `s[len:cap]` reads zeros, not nulls.
- Generic instantiations introduced by package-var initializers, non-void
  panic-tail `ret`, `InitLocals`, variadic/promoted interface dispatch, identical-
  layout struct conversion (`type Tag compact.Tag`), pointer-receiver methods
  promoted from embedded value fields, `*p = v` / `a,b = f()` / `return s, nil`
  boxing, and matching shim signatures (atomic Int32, reflect, time, runtime).

### Still open (the remaining frontier)

1. **Array callbacks** — `[].map`/`reduce`: a typed-nil pointer crosses the
   JS-callback ↔ native-function boundary (`getStr("length")` returns a typed nil),
   tied to the typed-nil-in-interface representation gap.
2. **`JSON.stringify`** of objects.
3. `fmt` formatting a non-nil `*goja.Exception` (`Exception.String`), only hit when
   surfacing one of the above as an error.

Tagged milestones: `0.0.21.goja-compiles-loads-jits`, `0.0.22.goja-runs-1plus2`,
`0.0.23.goja-evaluates-js`, `0.0.24.goja-loops-arrays-objects`.
