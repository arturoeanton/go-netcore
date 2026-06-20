# Validation apps

Whole, idiomatic Go programs — not feature probes — that must produce identical
output under `go run` and `goclr run`. They exist to show the compiler is
**application-agnostic**: goja is one hard validation target among several, not
the product. Run them with `go test ./tests/validation/`.

| App             | Class            | Exercises                                            | Status |
|-----------------|------------------|------------------------------------------------------|--------|
| `business-json` | Business / SaaS  | structs, methods, `encoding/json` (un/marshal), sort | ✅ |
| `cli-csv`       | CLI / ETL        | `encoding/csv`, maps, aggregation, sorting, `strconv`| ✅ |
| `rules-engine`  | Business logic   | interfaces, multi-result dispatch, composition       | ✅ |
| `http-basic`    | SaaS service     | `net/http` server + client round-trip, JSON          | ✅ |
| `goja`          | Hard dependency  | a pure-Go JS engine compiled to .NET                 | ⏳ blocked on the typed-box keystone (`docs/DESIGN-typed-box.md`) |

The harness reports an app as **skipped** (not failed) when `goclr` declines to
compile it (a `GCLR0xxx` diagnostic), so the suite stays green and begins
asserting automatically as the backend grows. `goja` reaches `sort.StringSlice`
dispatch through `golang.org/x/text/collate`, which needs per-value type identity
(representation-collapse problem) — the keystone that also unblocks precise `%T`,
reflect, and named-primitive Stringers.
