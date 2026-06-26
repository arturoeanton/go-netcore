package main

import (
	"fmt"
	"math"
)

// math.Pow, Log10, Log2 ported from Go's math sources, built on the byte-exact Exp/Log/
// Frexp/Ldexp — they match go run far more often than the platform versions (which differ
// by a ULP for ~40% of inputs). Pow's full special-case lattice (0, ±Inf, NaN, negative
// base with odd/even/non-integer exponent, ±0.5, huge exponents) is reproduced exactly.
func main() {
	for _, p := range [][2]float64{
		{2, 10}, {2, 0.5}, {2, -3}, {9, 0.5}, {8, 1.0 / 3}, {-2, 3}, {-2, 2}, {-2, 0.5},
		{-8, 1.0 / 3}, {0, 0}, {0, 5}, {0, -5}, {1, 1e10}, {-1, 1e10}, {-1, 1e10 + 1},
		{5, 0}, {2.5, 2.5}, {10, -2}, {0.5, 0.5}, {100, 0}, {2, 53}, {2, 64}, {3, 3},
		{-3, -3}, {2, 30}, {10, 10}, {7, -2}, {1.5, 8},
	} {
		fmt.Printf("%g^%g=%x\n", p[0], p[1], math.Pow(p[0], p[1]))
	}
	inf, ninf, nan := math.Inf(1), math.Inf(-1), math.NaN()
	fmt.Println(math.Pow(2, inf), math.Pow(0.5, inf), math.Pow(2, ninf), math.Pow(inf, 2), math.Pow(inf, -2))
	fmt.Println(math.Pow(ninf, 3), math.Pow(ninf, 2), math.Pow(ninf, -1))
	fmt.Println(math.IsNaN(math.Pow(nan, 2)), math.IsNaN(math.Pow(2, nan)), math.IsNaN(math.Pow(-1, 0.5)))
	fmt.Println(math.Pow(-1, inf), math.Pow(1, nan), math.Pow(0, -1))

	// Log10 / Log2 — exact powers and a few values.
	for _, x := range []float64{1, 10, 100, 1000, 1e10, 0.1, 0.01, 2, 5, 1000000} {
		fmt.Printf("log10(%g)=%x ", x, math.Log10(x))
	}
	fmt.Println()
	for _, x := range []float64{1, 2, 4, 8, 16, 1024, 0.5, 0.25, 0.125, 65536} {
		fmt.Printf("log2(%g)=%x ", x, math.Log2(x))
	}
	fmt.Println()
	fmt.Println(math.Log10(0), math.IsNaN(math.Log10(-1)), math.Log2(0), math.IsNaN(math.Log2(-2)))
}
