package main

import (
	"fmt"
	"math/rand/v2"
)

func main() {
	r := rand.New(rand.NewPCG(2024, 7))
	var nsum, esum float64
	for i := 0; i < 100000; i++ {
		nsum += r.NormFloat64()
		esum += r.ExpFloat64()
	}
	fmt.Printf("nsum=%.10f esum=%.10f\n", nsum, esum)

	r2 := rand.New(rand.NewPCG(5, 5))
	for i := 0; i < 4; i++ {
		fmt.Printf("%.17g %.17g\n", r2.NormFloat64(), r2.ExpFloat64())
	}

	r3 := rand.New(rand.NewPCG(99, 100))
	fmt.Println("perm", r3.Perm(12))
	deck := []int{0, 1, 2, 3, 4, 5, 6, 7}
	r3.Shuffle(len(deck), func(i, j int) { deck[i], deck[j] = deck[j], deck[i] })
	fmt.Println("shuffle", deck)
}
