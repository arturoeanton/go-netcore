# demo_uuid

`github.com/google/uuid` compiled and run on the CLR by goclr: deterministic v4 (via a
fixed `SetRand` reader), name-based v5 (`NewSHA1`), parse/format round-trips, version and
variant, and the nil UUID. Requires `go mod vendor`.

Driving uuid through goclr closed several stdlib gaps: `os.Getuid`/`Getgid`,
`net.Interfaces` (empty — goclr doesn't enumerate host NICs), `bytes.EqualFold`,
`encoding/hex.Encode`/`Decode`, and the **io.Reader callback bridge** (so `io.ReadFull`
drives a user reader's own `Read`, symmetric to the io.Writer bridge).

```bash
go mod vendor
goclr run ./examples/demo_uuid
```
