// demo_uuid shows github.com/google/uuid running on the CLR: deterministic UUIDs from a
// fixed seed, parse/format round-trips, and version/variant. Requires `go mod vendor`.
package main

import (
	"fmt"

	"github.com/google/uuid"
)

func main() {
	// Deterministic v4 from a fixed reader so output is reproducible.
	uuid.SetRand(repeatReader{})
	u := uuid.New()
	fmt.Println("v4:", u.String())
	fmt.Println("version:", u.Version(), "variant:", u.Variant())

	// Parse + round-trip + comparison.
	parsed, err := uuid.Parse(u.String())
	fmt.Println("parse err:", err, "equal:", parsed == u)

	// A v5 name-based UUID (deterministic by namespace+name).
	v5 := uuid.NewSHA1(uuid.NameSpaceDNS, []byte("example.com"))
	fmt.Println("v5:", v5.String())

	// Nil UUID.
	fmt.Println("nil:", uuid.Nil.String())
}

// repeatReader yields a fixed byte stream so uuid.New() is deterministic.
type repeatReader struct{}

func (repeatReader) Read(p []byte) (int, error) {
	for i := range p {
		p[i] = byte(i * 7)
	}
	return len(p), nil
}
