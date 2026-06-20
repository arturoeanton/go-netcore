# Typed-box keystone (M3) — design note

Status: **designed, not yet implemented.** This is the foundation for tasks 76
(typed box), 77 (deep reflect), and 78 (precise interface dispatch).

## Problem

A boxed value loses its Go *named-type* identity. `type Money int64` boxes to a
plain CLR `Int64`, indistinguishable from a real `int64`. Consequences:

- `fmt` `%s`/`%v` of a named primitive with a `String()`/`Error()` method does
  **not** dispatch the method — it prints `%!s(int=14997)` instead of `$149.97`
  (surfaced by `tests/validation/business-json`). Struct and pointer Stringers
  already work (keyed by CLR type name / pointer type-id); named *non-struct*
  types are the gap.
- `%T` of a named type prints the underlying CLR name (`main.Int64`) not
  `main.Money`.
- `reflect` cannot recover the dynamic type.
- Interface dispatch for named types that share a representation is imprecise.

Root cause: `goType(named)` erases the name to its representation
(`goir.TInt64`), and **all** boxing funnels through `OpBox{BoxTy}` which boxes by
representation. The IR at the box site does not even know it was `Money`.

## Design

1. **Carry identity in `goir.Type`.** Add `NamedId int64` + `NamedName string`
   (e.g. `"main.Money"`). Populate in `goType` for named types — at minimum the
   ones that need identity (have a method set, or are used through `any`/reflect).
   Keep it `0`/empty for plain primitives to bound the blast radius.

2. **Type registry.** Assign each identity-bearing named type a stable id; record
   `{id, displayName, stringerClosure?, itable}`. Extend `collectStringers` to
   cover named non-struct types (today it iterates structs only).

3. **Runtime wrapper.**
   ```csharp
   public sealed class GoNamed { public long TypeId; public object? Value; public string TypeName; }
   ```
   `Rt.MakeNamed(value, id, name)` / `Rt.Unwrap(obj)`.

4. **Box site.** In `emitBox`, if `t.NamedId != 0`, box the underlying value then
   wrap via `Rt.MakeNamed`.

5. **Consumers must be wrapper-aware** (this is the invasive part — must land
   atomically with step 4 to avoid regressions):
   - `OpUnbox` to a named type → unwrap then unbox.
   - interface dispatch `isinst` (`lower_iface.go`) → unwrap before the type test;
     dispatch named-type methods via the registry/itable.
   - type assert / type switch / `==` → unwrap operands.
   - `fmt` (`Fmt.cs`): `Format`/`TryStringer`/`GoTypeName` recognize `GoNamed`
     (dispatch stringer by `TypeId` for string verbs, unwrap for `%d`/`%x`/etc.,
     use `TypeName` for `%T`).

## Why not a shortcut

- Wrapping only at `emitBoxedElem` (containers/variadic) still reaches interface
  dispatch via `[]error{e}` → `e.Error()`, so `isinst` would miss the wrapped
  value — a silent regression.
- Wrapping only at the `fmt` variadic-packing site is safe (fmt args are terminal)
  but is a fmt-specific special case, not the real per-value identity the rest of
  M3 (reflect, precise dispatch) needs. Rejected as a non-foundational hack.

## Increment plan

1. `goir.Type.NamedId/NamedName` + `goType` population + registry + extend
   `collectStringers` to named non-struct types. (No behavior change yet.)
2. `GoNamed` runtime + `emitBox` wrap + make `Fmt` recognize `GoNamed`.
3. Make unbox / isinst-dispatch / assert / type-switch / `==` unwrap. Land with 2.
4. `%T` precision + `reflect.TypeOf().Name()/Kind()` off the registry (task 77).
5. itable-based precise interface dispatch (task 78).

Guard every step with the full conformance suite (`tests/conformance`, 139
fixtures) **and** the validation apps (`tests/validation/*`).
