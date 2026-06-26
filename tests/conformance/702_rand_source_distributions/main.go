package main

import (
	"fmt"
	"math/rand"
)

// math/rand with an explicit source is byte-exact with Go (the PRNG is ported): the integer
// and float methods, the Gaussian/exponential distributions, Perm, Shuffle, and Read all
// match. (The deprecated top-level rand.Seed/global source is documented as divergent.)
func main() {
	r := rand.New(rand.NewSource(12345))
	fmt.Println(r.Int(), r.Int31(), r.Int63())
	fmt.Println(r.Intn(1000), r.Int31n(1000), r.Int63n(1000000))
	fmt.Println(r.Uint32(), r.Uint64())
	fmt.Printf("%.6f %.6f\n", r.Float64(), r.Float32())
	fmt.Printf("%.6f %.6f\n", r.NormFloat64(), r.ExpFloat64())
	fmt.Println(r.Perm(8))
	s := make([]int, 10)
	for i := range s {
		s[i] = i
	}
	r.Shuffle(len(s), func(i, j int) { s[i], s[j] = s[j], s[i] })
	fmt.Println(s)
	buf := make([]byte, 8)
	r.Read(buf)
	fmt.Printf("%x\n", buf)

	// two sources with the same seed produce the same sequence
	ra := rand.New(rand.NewSource(42))
	rb := rand.New(rand.NewSource(42))
	same := true
	for i := 0; i < 100; i++ {
		if ra.Int63() != rb.Int63() {
			same = false
		}
	}
	fmt.Println("deterministic:", same)
}
