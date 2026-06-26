package main

import (
	"fmt"
	"math"
)

// The '#' flag on %g/%G keeps trailing zeros so the result shows N significant figures
// (default 6, e.g. 1.0 -> 1.00000, 0.0 -> 0.00000 with the leading-zero special case),
// across the fixed and exponent forms and with explicit precision.
func main() {
	for _, f := range []float64{
		1.0, 3.14, 0.00001, 100000.0, 1.5, 6.022e23, 0.001, 0.0, 123.456,
		1e-9, -2.5, 1000000.0, 0.1, 9.999999, 42, -0.0005,
	} {
		fmt.Printf("%g | %#g | %#G\n", f, f, f)
	}
	// Explicit precision with '#'.
	fmt.Printf("[%#.3g][%#.3G][%#.8g][%#.1g][%#.10g]\n", 12345.678, 0.00012345, 1.5, 99.0, 3.14159)
	// float32.
	fmt.Printf("%#g %#g %#g\n", float32(1.5), float32(0.1), float32(0))
	// Inf/NaN untouched.
	fmt.Printf("%#g %#g %#g %#G\n", math.Inf(1), math.Inf(-1), math.NaN(), math.Inf(1))
	// %#v of a float (uses g) and a struct with float fields.
	type P struct{ X, Y float64 }
	fmt.Printf("%#v %#v %#v\n", 1.0, 0.0, P{1.5, 3.14})
}
