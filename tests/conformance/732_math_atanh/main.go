package main

import (
	"fmt"
	"math"
)

// math.Atanh is byte-exact with go run: ported from Go's math/atanh.go (built on the
// byte-exact Log1p), since the platform atanh differs by a ULP for |x| >= ~0.25.
func main() {
	xs := []float64{
		0, 0.5, -0.5, 0.7, -0.25, 0.9, 0.99, 0.999999, -0.999999,
		1e-10, 1e-9, 0.4999999, 0.49999999, 0.123456789, 0.876543,
		0.25, 0.2499999, 0.001, 0.0001,
	}
	for _, x := range xs {
		fmt.Printf("atanh(%g)=%x\n", x, math.Atanh(x))
	}
	// Special cases: ±1 -> ±Inf, out of range / NaN -> NaN, signed zero preserved.
	fmt.Println(math.Atanh(1), math.Atanh(-1))
	fmt.Println(math.IsNaN(math.Atanh(1.5)), math.IsNaN(math.Atanh(-2)), math.IsNaN(math.Atanh(math.NaN())))
	nz := math.Copysign(0, -1)
	fmt.Println(math.Signbit(math.Atanh(nz)), math.Atanh(0) == 0)
}
