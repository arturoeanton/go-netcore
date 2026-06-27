package main

import (
	"fmt"
	"math/rand"
)

// math/rand.NewZipf — the rejection-inversion Zipf generator (math/rand/zipf.go). Its h/hinv
// use math.Exp/Log and it draws from the same seeded *rand.Rand, so the sequence is byte-exact
// with go run. NewZipf returns nil when s <= 1 or v < 1.
func main() {
	configs := []struct {
		s, v float64
		imax uint64
	}{
		{1.5, 1, 100}, {2.0, 1, 50}, {1.1, 5, 1000}, {3.0, 2, 10}, {1.01, 1, 10000},
	}
	for _, c := range configs {
		z := rand.NewZipf(rand.New(rand.NewSource(12345)), c.s, c.v, c.imax)
		var sum, max uint64
		for i := 0; i < 200; i++ {
			k := z.Uint64()
			sum += k
			if k > max {
				max = k
			}
		}
		fmt.Printf("s=%.2f v=%.0f imax=%d sum=%d max=%d inrange=%v\n",
			c.s, c.v, c.imax, sum, max, max <= c.imax)
	}

	// Out-of-domain parameters return a nil generator.
	fmt.Println(rand.NewZipf(rand.New(rand.NewSource(1)), 1.0, 1, 10) == nil)
	fmt.Println(rand.NewZipf(rand.New(rand.NewSource(1)), 2.0, 0, 10) == nil)

	// An exact draw sequence.
	z := rand.NewZipf(rand.New(rand.NewSource(999)), 1.3, 2, 500)
	for i := 0; i < 20; i++ {
		fmt.Print(z.Uint64(), " ")
	}
	fmt.Println()
}
