package main

import (
	"fmt"
	"math/rand"
)

func main() {
	r := rand.New(rand.NewSource(1))
	// Many draws to exercise the ziggurat rejection/base-strip branches.
	var nsum, esum float64
	for i := 0; i < 100000; i++ {
		nsum += r.NormFloat64()
		esum += r.ExpFloat64()
	}
	fmt.Printf("nsum=%.10f esum=%.10f\n", nsum, esum)

	// Print the first few raw draws byte-for-byte.
	r2 := rand.New(rand.NewSource(99))
	for i := 0; i < 5; i++ {
		fmt.Printf("%.17g %.17g\n", r2.NormFloat64(), r2.ExpFloat64())
	}
}
