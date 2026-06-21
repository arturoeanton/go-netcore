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
curl -X POST -d '{"text":"hello"}' localhost:8080/notes
```

## Status

The whole program **compiles** — driving every layer (Gin, `database/sql`, the
`go-r2-sqlite` engine) through goclr's backend, which surfaced and fixed a long list of
general compiler features (comma-ok cell capture, pointer-to-value-receiver interface
dispatch, type-alias method resolution, `defer` of interface/shim methods,
`&slice[i].field`, range-over-func via a `slices`/`maps`/`iter` overlay, dependency-order
package init, `database/sql`-needed shims for `sync/atomic` typed integers and pointers,
and the `os.File` random-access I/O the pager needs). It also runs end to end through
driver registration, connection, opening the database file, and statement execution.

Remaining: a SQLite-engine correctness bug under goclr where a `CREATE TABLE` reports
success but its schema is not visible to the next statement (the table catalog write is
lost between statements). The same program is byte-correct under `go run`, so this is a
goclr codegen issue isolated to the engine's catalog/transaction path — under
investigation. `demo_gin` (Gin alone) runs fully and serves correct responses.
