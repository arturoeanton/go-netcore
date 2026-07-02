package main

import (
	"fmt"
	"math"
	"strconv"
)

// g13 prints with 13 significant digits: Go's own Erf/Erfc differ in the LAST ULP
// across architectures (linux/amd64 vs darwin/arm64), so a shortest-round-trip
// print cannot be byte-exact against every `go run`. 13 digits still locks the
// ported algorithm tightly while staying arch-stable.
func g13(v float64) string { return strconv.FormatFloat(v, 'g', 13, 64) }

func main() {
	// Erf on the polynomial range (|x|<1.25) and the saturated tail.
	for _, x := range []float64{0, 0.1, 0.25, 0.5, 0.75, 0.84375, 1, 1.1, 1.2, 3, 5, 6, 7, -0.5, -1, -1.2, -3, -6, 1e-10, 1e-300} {
		fmt.Printf("%s ", g13(math.Erf(x)))
	}
	fmt.Println()
	// Erfc on |x|<1.25.
	for _, x := range []float64{0, 0.1, 0.25, 0.5, 0.75, 1, 1.2, -0.1, -0.5, -1, -1.2} {
		fmt.Printf("%s ", g13(math.Erfc(x)))
	}
	fmt.Println()
	fmt.Println(g13(math.Erf(math.Inf(1))), g13(math.Erf(math.Inf(-1))), math.IsNaN(math.Erf(math.NaN())),
		g13(math.Erfc(math.Inf(1))), g13(math.Erfc(math.Inf(-1))), g13(math.Erfc(30)), g13(math.Erfc(-30)))
}
