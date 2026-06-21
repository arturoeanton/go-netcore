# demo_gin_sql — Gin + database/sql + a pure-Go SQLite driver on .NET via goclr

A [Gin](https://github.com/gin-gonic/gin) REST API backed by `database/sql` and the
pure-Go, zero-cgo SQLite driver [go-r2-sqlite](https://github.com/arturoeanton/go-r2-sqlite),
with the **entire stack compiled to ECMA-335 IL by goclr** and run on the CLR:

- the Gin router and middleware,
- the `database/sql` connection pool and `database/sql/driver` layer (compiled from
  source — see `compileFromSource` in `internal/lower/lower.go`),
- and the SQLite engine itself (B-tree, pager, SQL parser/VDBE — ~14k lines of Go).

Nothing native is loaded: there is no cgo and no managed database backend. The SQLite
storage engine runs as compiled Go on .NET.

## Endpoints

| Method | Path          | Description            |
| ------ | ------------- | ---------------------- |
| GET    | `/notes`      | list all notes (JSON)  |
| GET    | `/notes/:id`  | fetch one note         |
| POST   | `/notes`      | create a note (JSON)   |

## Run

```sh
# from the module root, with a vendored dependency tree
GOFLAGS=-mod=mod GOSUMDB=off go mod vendor

# compile to a .dll and run on dotnet (binds 127.0.0.1:8080)
go run ./cmd/goclr run ./examples/demo_gin_sql
```

```sh
curl localhost:8080/notes
curl localhost:8080/notes/1
curl -X POST -d '{"text":"hello"}' localhost:8080/notes
```

## Status

The whole program **compiles and runs end to end**, with full CRUD:

```
GET  /notes      -> [{"id":1,"text":"first note"},{"id":2,"text":"second note"}]
GET  /notes/1    -> {"id":1,"text":"first note"}
POST /notes      -> {"id":3,"text":"from goclr"}    (persisted, reflected by the next GET)
POST {} (empty)  -> 400 {"error":"text required"}
GET  /notes/999  -> 404 {"error":"not found"}
```

Every layer is exercised through goclr's backend — Gin routing and JSON rendering,
`database/sql`'s pool/`Rows`/`Scan`, and the `go-r2-sqlite` engine's `CREATE`/`INSERT`/
`SELECT` with `INTEGER`, `REAL` and `TEXT` columns scanned into their Go types. Driving
this stack surfaced and fixed a long list of general compiler/runtime features:

- comma-ok assertion results stored into struct fields (the `CREATE TABLE` catalog write),
- pointer-to-value-receiver interface dispatch and type-alias method resolution,
- `defer` of interface/shim methods, `&slice[i].field`, range-over-func,
- dependency-order package init, `sync/atomic` typed integers/pointers, `os.File` I/O,
- `sort.SliceStable`, `time.Timer.Reset`,
- `sync.Locker` dispatch on an opaque mutex handle (database/sql's `Rows` read lock),
- distinguishing `*int64` / `*string` / `*[]byte` and rejecting an opaque shim type
  (`time.Time`) in a type switch — exactly what `database/sql`'s `convertAssign` needs to
  scan numbers and strings correctly,
- `json.Decoder.Decode` / `json.Unmarshal` into anonymous structs and into interface-erased
  targets (the shape gin's request binding uses).

The one piece not yet supported is gin's struct-tag *validator* (the reflection-heavy
`go-playground/validator`), so the handler uses `ShouldBindJSON` and validates the
required field explicitly — see `main.go`.
