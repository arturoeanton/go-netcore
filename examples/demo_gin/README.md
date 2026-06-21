# demo_gin — Gin on .NET via goclr

A [Gin](https://github.com/gin-gonic/gin) HTTP service compiled to an ECMA-335
assembly by goclr and run on the CLR. Gin is a **validation target**: it exercises
the compiler over a real third-party web framework, not a goclr product.

## Run

```sh
# from the module root, with a vendored dependency tree
go mod vendor

# compile to a .dll and run on dotnet
go run ./cmd/goclr run ./examples/demo_gin
```

Then:

```sh
curl localhost:8080/health          # ok
curl localhost:8080/ping            # {"message":"pong"}
curl localhost:8080/hello/world     # {"hello":"world"}
```

## Notes

Gin is pinned to **v1.10.1** (v1.12+ adds an HTTP/3 / quic-go and BSON dependency
tree that pulls in raw-socket syscalls). goclr builds with the `purego` and
`nomsgpack` tags so the dependency closure takes no-assembly / no-unsafe paths and
drops the MessagePack (`ugorji/go/codec`) binding. A handful of goclr-safe overlays
(under `goclr.overlays/`) replace the remaining `unsafe.Pointer` reinterpretations
in Gin's binding dependencies — the same overlay strategy used for goja.
