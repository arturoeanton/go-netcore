package main

import (
	"fmt"
	"math"
)

// math.Exp is ported from Go's math/exp.go (reduction + the byte-exact expmulti tail), so
// it matches go run bit-for-bit far more often than the platform exp (which differs by a
// ULP for ~10% of inputs). Everything built on Exp — Sinh, Cosh, Tanh — improves with it.
// (A small fraction of inputs still inherit the back-end FMA last-ULP edge; these curated
// values are byte-exact.)
func main() {
	for _, x := range []float64{
		0, 1, -1, 0.5, -0.5, 2, -2, 5, -5, 10, -10, 0.1, -0.1, 0.001,
		3.14159, 7.5, -7.5, 15, -20, 0.625, -0.625, 1.5, -3.25, 0.0625, 12.0, -8.0, 21, -21,
	} {
		fmt.Printf("exp(%g)=%x sinh=%x cosh=%x tanh=%x\n", x, math.Exp(x), math.Sinh(x), math.Cosh(x), math.Tanh(x))
	}
	// Special cases.
	fmt.Println(math.Exp(0), math.Exp(math.Inf(1)), math.Exp(math.Inf(-1)), math.IsNaN(math.Exp(math.NaN())))
	fmt.Println(math.Exp(710), math.Exp(-746))
	fmt.Println(math.Sinh(math.Inf(1)), math.Cosh(math.Inf(-1)), math.Tanh(math.Inf(1)), math.Tanh(math.Inf(-1)))
	fmt.Println(math.IsNaN(math.Sinh(math.NaN())), math.IsNaN(math.Tanh(math.NaN())))
	fmt.Printf("%x %x %x\n", math.Exp(1e-10), math.Sinh(0), math.Tanh(0))
}
