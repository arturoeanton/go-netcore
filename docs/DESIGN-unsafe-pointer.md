# DESIGN — unsafe.Pointer in goclr

## The hard ceiling

goclr's value model has **no raw memory**: a `GoSlice` is `object[]`-backed, a `GoString`
is a .NET string, a `GoPtr` is a cell with get/set closures. There is no `*byte` pointing
into a slice's backing array, and no addressable struct layout. So:

- **Pointer arithmetic / header *writes* are fundamentally impossible** —
  `reflect.SliceHeader{Data,Len,Cap}` field writes (`bh.Data = sh.Data`) that reconstruct a
  slice/string from a foreign header, `unsafe.Offsetof` used for real layout, `unsafe.Add`
  over real memory, quic-go's OOB socket buffers. These stay **overlay** territory (replace
  the file with goclr-safe Go) — supporting `unsafe.Pointer` would NOT remove them.
- **Header *reads* for offset arithmetic ARE supported** — comparing two slices' `.Data`
  fields to recover a sub-slice's byte offset (go-toml/v2 `internal/danger.SubsliceOffset`)
  is now a first-class capability: `(*reflect.SliceHeader)(unsafe.Pointer(&x)).Data` lowers
  to a read-only header view (`Reflect.SliceHeaderOf`/`SH_Data`) whose `.Data` is
  `stable_per_backing_base*scale + Off`, so two views over the same backing differ by exactly
  the offset (with Go's zero-cap-collapses-to-base quirk replicated). `.Len`/`.Cap` are the
  slice's. No overlay needed. String header *offset diffs* remain unsupportable (.NET
  substrings don't share backing storage); only `.Len` is meaningful there.
- **The `string ↔ []byte` zero-copy reinterpret is semantically a conversion** — it just
  avoids the copy. goclr can compile it as the safe `string(b)` / `[]byte(s)`. Correct,
  not zero-copy (irrelevant to correctness).

So the goal is not "implement unsafe.Pointer" (impossible in general) but **recognize the
safe `string↔[]byte` idioms and lower them as conversions**, plus reject the rest with a
clear diagnostic (today `internal/analysis/unsafe.go` already does the blocking).

## The two safe idioms

### Modern (Go 1.20+ builtins) — clean, both directions
```go
func b2s(b []byte) string { return unsafe.String(unsafe.SliceData(b), len(b)) }
func s2b(s string) []byte { return unsafe.Slice(unsafe.StringData(s), len(s)) }
```
`unsafe.Slice/String/SliceData/StringData` are already on the `approvedUnsafe` allow-list
in the analysis pass, but **lowering does not implement them** (today they fail with
GCLR0301 "selector / type of expression"). This is the highest-value, fully-correct
target: both directions reduce to a conversion with no header manipulation.

### Old (pre-1.20) — only the bytes→string direction is easy
```go
func b2s(b []byte) string { return *(*string)(unsafe.Pointer(&b)) }            // easy → string(b)
func s2b(s string) []byte {                                                    // HARD → header writes
	bh := (*reflect.SliceHeader)(unsafe.Pointer(&b)); sh := (*reflect.StringHeader)(unsafe.Pointer(&s))
	bh.Data = sh.Data; bh.Len = sh.Len; bh.Cap = sh.Len; return b
}
```
The `*(*string)(unsafe.Pointer(&b))` form is recognizable; the SliceHeader-write form is
not (raw memory). Old deps (fasttemplate, validator) mix both, so they still need their
overlay regardless — which is why the modern builtins are the better investment.

## Implementation plan (modern builtins)

1. **Analysis** (`internal/analysis/unsafe.go`): keep the allow-list; no change needed —
   these selectors are already approved.
2. **Lowering** — special-case the four builtins in `callExpr`/`namedFuncCall`:
   - `unsafe.SliceData(b)` → lower `b` (a `GoSlice`); carry it as the result (the `*byte`
     is represented by the slice itself).
   - `unsafe.StringData(s)` → lower `s` (a `GoString`); carry it.
   - `unsafe.String(p, n)` → `p` is the carried `GoSlice`; emit `string(p[:n])`
     (`GoStrings.FromBytesN(slice, n)` — a new runtime helper, or `OpStrFromBytes` after a
     `[:n]` slice when n is the full length).
   - `unsafe.Slice(p, n)` → `p` is the carried `GoString` (the only supportable case, T=byte);
     emit `[]byte(p[:n])` (`GoStrings.ToByteSlice` + truncation to n).
   - Any other `unsafe.Slice`/`unsafe.String` over a real `*T` pointer → GCLR0301 (the
     pointer can't be represented).
   - Type caveat: `SliceData`/`StringData` "return" `*byte`/`*T` but we leave a slice/string
     on the stack; this is safe only because the sole consumer we support is the matching
     `String`/`Slice` builtin. If the value flows elsewhere, fail (don't silently miscompile).
3. **Old easy idiom** (DONE): in the `*X` deref lowering, recognize
   `*(*string)(unsafe.Pointer(&b))` / `*(*[]byte)(unsafe.Pointer(&s))` and emit the
   conversion. The SliceHeader-*write* form stays an overlay.
3b. **Header-read offset idiom** (DONE): in `conversion()`, recognize
   `(*reflect.SliceHeader)(unsafe.Pointer(&x))` / `(*reflect.StringHeader)(...)` and lower to
   a read-only `GoHeaderView` (`Reflect.SliceHeaderOf`/`StringHeaderOf`); `.Data`/`.Len`/`.Cap`
   read via `SH_Data`/`SH_Len`/`SH_Cap`. Agnostic — general compiler capability, no per-lib
   overlay. Covers go-toml's `SubsliceOffset` ([]byte-backed). See fixture
   `400_reflect_sliceheader_offset`.
4. **Fixtures**: round-trip `b2s`/`s2b` byte-exact vs `go run`; assert a real pointer-arith
   `unsafe` still errors with GCLR0201.

## What this buys

- Modern Go code using the idiomatic 1.20+ `string↔[]byte` conversions compiles (a real
  completeness/compat win; e.g. newer stdlib-style helpers).
- It does NOT remove the fasttemplate/validator overlays — those use the hard header *write*
  forms (reconstructing a slice from a foreign header), which remain overlay-only.
- KrakenD/Lura's go-toml `SubsliceOffset` no longer needs an overlay: the read-only
  `reflect.SliceHeader.Data` offset path (idiom 3b above) compiles it directly.
