package main

import (
	"fmt"
	"math"
)

// fmt's %b verb on a float produces the binary-exponent form (mantissa × 2^exp), the same
// as strconv.FormatFloat(f, 'b', -1, bitSize). NaN/±Inf print like the other float verbs.
func main() {
	for _, v := range []float64{1.5, 255, 0.5, 3.14159, 0, math.Inf(1), math.Inf(-1), math.NaN(), -7.5, 1e20, 5e-324} {
		fmt.Printf("%b\n", v)
	}
	var f32 float32 = 1.5
	fmt.Printf("%b\n", f32)
	fmt.Printf("%b\n", float32(255))
	fmt.Printf("%v\n", []float64{1.5, 2.5})
}
