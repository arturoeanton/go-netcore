# demo_testify

`github.com/stretchr/testify/assert` running on the CLR under `goclr test`: the assertion
surface (Equal incl. struct deep-equal, NotEqual, Greater, Len, ElementsMatch, Contains on
strings/maps, NoError/Error/Nil) and testify's rich failure messages all work, exercising
goclr's reflect (DeepEqual, Value.CanConvert, StructField) and the `goclr test` harness.

The test files carry `//go:build goclr` so the normal toolchain skips them; goclr (which sets
the `goclr` tag) runs them. Requires `go mod vendor`.

```bash
go mod vendor
goclr test ./examples/demo_testify
# === RUN   TestArithmetic
# --- PASS: TestArithmetic (0.00s)
# ...
# PASS
```

Driving testify closed several stdlib/reflect gaps: the `safe` build tag (go-spew's no-unsafe
path), `encoding/hex.Dump`, `reflect.Value.CanConvert`, `reflect.StructField.IsExported`,
`(*regexp.Regexp).Match`, `os.Lstat`, and `runtime.Goexit`.
