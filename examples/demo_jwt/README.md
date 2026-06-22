# demo_jwt (work in progress)

`github.com/golang-jwt/jwt/v5` on the CLR. goclr **compiles the full package** and **signs
HS256 tokens** (HMAC-SHA256) correctly. `jwt.Parse` does not yet verify — reading the token
header reports "alg unspecified" (a JSON header round-trip gap, under debug), so the parse
lines fail for now. Requires `go mod vendor`.

Closed driving jwt: a general bug (shim method values now box a value-type receiver such as
`crypto.Hash`), plus `math.Modf`, `json.Number`, `encoding/hex.Dump`, `big.Int.FillBytes`,
`base64.Encoding.Strict`, and fail-closed stubs for the asymmetric crypto (ES*/RS*/PS*/EdDSA
reject, never wrongly accept — HMAC is the supported path).
