package main

import (
	"fmt"
	"math/rand/v2"
)

// math/rand/v2's ChaCha8 source (rand.NewChaCha8) is byte-exact with go run: a faithful
// port of internal/chacha8rand's block + refill/reseed machine. Crosses the 32-word block
// boundary and the counter-driven reseed.
func main() {
	seed := [32]byte{1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16,
		17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32}
	r := rand.New(rand.NewChaCha8(seed))
	for i := 0; i < 12; i++ {
		fmt.Print(r.Uint64(), " ")
	}
	fmt.Println()
	fmt.Println(r.IntN(1000000), r.Int64(), r.Uint32())
	fmt.Printf("%.10f %.10f\n", r.Float64(), r.Float32())
	fmt.Println(r.Perm(6))

	// Zero seed; many draws to cross block/refill/reseed (256+ words).
	r2 := rand.New(rand.NewChaCha8([32]byte{}))
	var x uint64
	for i := 0; i < 300; i++ {
		x ^= r2.Uint64()
	}
	fmt.Println(x)

	// Direct source: Uint64 then re-Seed.
	c := rand.NewChaCha8([32]byte{0xaa, 0xbb, 0xcc})
	fmt.Println(c.Uint64(), c.Uint64())
	c.Seed([32]byte{0x11, 0x22})
	fmt.Println(c.Uint64())

	// PCG still works (regression).
	p := rand.New(rand.NewPCG(7, 99))
	fmt.Println(p.Uint64(), p.IntN(100))
}
