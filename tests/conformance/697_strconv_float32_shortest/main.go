package main

import (
	"fmt"
	"strconv"
)

// strconv.FormatFloat with bitSize 32 and prec -1 (shortest) must use the float32
// round-trip — fewer significant digits — not the widened float64 (which prints a spurious
// tail like 0.10000000149011612). Also FormatFloat(0, 'E', -1, 64) is "0E+00".
func main() {
	for _, v := range []float64{0.1, 3.14159, 1.0 / 3.0, 100000.0, 1e20, 1e-20, 2.5, 0, -0.5, 65536.0} {
		x := float64(float32(v))
		fmt.Printf("%s|%s|%s|%s|%s\n",
			strconv.FormatFloat(x, 'g', -1, 32),
			strconv.FormatFloat(x, 'e', -1, 32),
			strconv.FormatFloat(x, 'f', -1, 32),
			strconv.FormatFloat(x, 'E', -1, 32),
			strconv.FormatFloat(x, 'G', -1, 32))
	}
	// fixed precision is unaffected by bitSize
	fmt.Println(strconv.FormatFloat(float64(float32(0.1)), 'f', 4, 32))
	// the 'E'/'e' zero forms
	fmt.Println(strconv.FormatFloat(0, 'E', -1, 64), strconv.FormatFloat(0, 'e', -1, 64))
	// float32 via fmt %v (already used the float32 overload, regression guard)
	var f float32 = 0.1
	fmt.Println(f, []float32{0.1, 0.2, 0.3})
}
