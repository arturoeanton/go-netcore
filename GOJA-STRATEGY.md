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
