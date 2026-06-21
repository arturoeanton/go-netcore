# demo_echo — Echo on .NET via goclr

An [Echo](https://github.com/labstack/echo) HTTP service compiled to an ECMA-335
assembly by goclr and run on the CLR. Echo is a **validation target**: it exercises
the compiler over a real third-party web framework, not a goclr product.

Echo is the heavier of the two web-framework targets — beyond the router/middleware
stack it pulls in the `crypto/x509` + `golang.org/x/crypto/acme/autocert` TLS
subsystem, so compiling it cleanly drives a much larger stdlib surface (crypto,
x509, ASN.1, PEM, `net` listeners, `io/fs`) than Gin does. The entire program,
framework included, is lowered to IL — no part of Echo runs as interpreted Go.

## Run

```sh
# from the module root, with a vendored dependency tree
go mod vendor

# compile to a .dll and run on dotnet
go run ./cmd/goclr run ./examples/demo_echo
```

Then:

```sh
curl localhost:8080/health          # ok
curl localhost:8080/ping            # {"message":"pong"}
curl localhost:8080/hello/world     # {"hello":"world"}
curl -i localhost:8080/missing      # 404 {"message":"Not Found"}
```

## Notes

Echo serves over goclr's `System.Net.HttpListener` backend rather than its own
`net.Listener`: `(*http.Server).Serve` releases the port Echo's `newListener` bound
and serves the same address with the registered handler. Plain HTTP is fully
exercised; the ACME/autocert TLS path compiles (so the whole framework lowers) but
is not driven — `tls.X509KeyPair` and friends are honest no-op shims on that path.
