package main

import (
	"fmt"
	"math"
)

// math.Cbrt, Sinh, Cosh and Log are byte-exact with go run: ported from Go's fdlibm
// sources (Cbrt self-contained; Sinh/Cosh atop the byte-exact Exp; Log via Frexp).
// The platform versions differ by a ULP for some inputs.
func main() {
	for _, x := range []float64{8, 27, 2, 0.5, 100, -64, 1e10, 0.001, 1e-100, 1e300} {
		fmt.Printf("cbrt(%g)=%x\n", x, math.Cbrt(x))
	}
	for _, x := range []float64{0.5, 1, 2, 5, 10, 20, 21, 22, -3, 0.1, 0.3, 0.6, 0.01, 700} {
		fmt.Printf("sinh(%g)=%x cosh(%g)=%x\n", x, math.Sinh(x), x, math.Cosh(x))
	}
	for _, x := range []float64{2, 10, 0.5, 100, 0.01, 1.5, 1e10, 0.001, 1e-300, 1e300, 0.999, 1.001} {
		fmt.Printf("log(%g)=%x\n", x, math.Log(x))
	}

	// Special cases.
	fmt.Println(math.Cbrt(0), math.Cbrt(math.Inf(1)), math.Cbrt(math.Inf(-1)), math.IsNaN(math.Cbrt(math.NaN())))
	fmt.Println(math.Sinh(math.Inf(1)), math.Sinh(math.Inf(-1)), math.IsNaN(math.Sinh(math.NaN())))
	fmt.Println(math.Cosh(math.Inf(-1)), math.IsNaN(math.Cosh(math.NaN())))
	fmt.Println(math.Log(0), math.IsNaN(math.Log(-1)), math.Log(math.Inf(1)), math.IsNaN(math.Log(math.NaN())))
	fmt.Printf("%x %x %x\n", math.Log(1), math.Sinh(0), math.Cbrt(math.Copysign(0, -1)))
}
