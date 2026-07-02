package main

import (
	"fmt"
	"math"
	"strconv"
)

// 13 significant digits: Go's own Erfinv/Erfcinv differ in the last ULP across
// architectures (linux/amd64 vs darwin/arm64), so shortest-round-trip prints are
// not portable; 13 digits still locks the ported polynomial tightly.
func g13(v float64) string { return strconv.FormatFloat(v, 'g', 13, 64) }

func main() {
	// Erfinv/Erfcinv on the polynomial path (|x|<=0.85).
	for _, x := range []float64{0, 0.1, 0.25, 0.5, 0.7, 0.85, -0.1, -0.5, -0.85, 0.333, -0.666} {
		fmt.Printf("%s ", g13(math.Erfinv(x)))
	}
	fmt.Println()
	for _, x := range []float64{1, 0.75, 0.5, 0.25, 1.5, 1.75, 1.25} { // 1-x in [-0.75,0.85]
		fmt.Printf("%s ", g13(math.Erfcinv(x)))
	}
	fmt.Println()
	fmt.Println(g13(math.Erfinv(1)), g13(math.Erfinv(-1)), math.IsNaN(math.Erfinv(2)), math.IsNaN(math.Erfinv(-2)), g13(math.Erfcinv(0)), g13(math.Erfcinv(2)))
}
