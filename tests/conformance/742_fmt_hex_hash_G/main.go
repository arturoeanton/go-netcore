package main

import (
	"fmt"
	"math"
)

// The '#' flag on %x/%X of a string or []byte adds an 0x/0X prefix — once for the whole
// run, or once per byte when combined with the space flag (0xde 0xad). And %G uppercases
// the exponent marker (1E-05) in the shortest form, like %E does for the fixed form.
func main() {
	b := []byte{0xde, 0xad, 0xbe}
	fmt.Printf("[%x][%X][%#x][%#X][%% x %x][%%# x %# x][%%# X %# X]\n", b, b, b, b, b, b, b)
	fmt.Printf("[% x][% X]\n", b, b)
	s := "Hi!"
	fmt.Printf("[%x][%#x][% x][%# x][%#X]\n", s, s, s, s, s)
	// Empty input keeps no prefix.
	fmt.Printf("[%#x][%#x]\n", "", []byte{})

	// %G / %g shortest, exponent casing.
	for _, f := range []float64{0.00001, 100000.0, 1.5, 6.022e23, 1e-9, 0.0, 3.14159} {
		fmt.Printf("g=%g G=%G\n", f, f)
	}
	// %G with explicit precision.
	fmt.Printf("[%.3G][%.3g][%10.3G]\n", 12345.678, 12345.678, 0.00012345)
	// %G of Inf/NaN keeps their form (no exponent to uppercase).
	fmt.Printf("[%G][%G][%G]\n", math.Inf(1), math.Inf(-1), math.NaN())
}
