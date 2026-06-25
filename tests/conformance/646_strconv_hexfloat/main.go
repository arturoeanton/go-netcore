package main

import (
	"fmt"
	"math"
	"strconv"
)

// strconv.FormatFloat 'x'/'X' (and fmt's %x/%X for floats): hexadecimal floating-point,
// "0x1.<hexfrac>p±dd". prec<0 prints the shortest exact fraction; prec>=0 prints exactly
// that many hex digits with round-to-even (and the carry can ripple into the leading digit).
// NaN/±Inf and signed zero format like every other verb; float32 uses the 23-bit mantissa.
func main() {
	vals := []float64{
		255, 1, 0.5, 3.14159, 0, math.Copysign(0, -1), 1024, 0.1, 2,
		math.Inf(1), math.Inf(-1), math.NaN(), -7.5, 1e308, 5e-324,
		math.MaxFloat64, 1.0 / 3.0, math.SmallestNonzeroFloat64,
	}
	for _, v := range vals {
		fmt.Printf("%s | %s\n", strconv.FormatFloat(v, 'x', -1, 64), strconv.FormatFloat(v, 'X', -1, 64))
	}

	fmt.Println("--- prec ---")
	for _, p := range []int{0, 1, 3, 4, 13, 14} {
		fmt.Println(strconv.FormatFloat(255, 'x', p, 64), strconv.FormatFloat(1.0/3.0, 'x', p, 64))
	}
	fmt.Println(strconv.FormatFloat(0.999999, 'x', 2, 64)) // rounding carry

	fmt.Println("--- float32 ---")
	for _, v := range []float64{255, 1, 0.5, 3.14, math.MaxFloat32, 1e-40} {
		fmt.Println(strconv.FormatFloat(v, 'x', -1, 32))
	}

	fmt.Println("--- fmt verb ---")
	for _, v := range []float64{255, 0.5, -7.5, 3.14159, math.Inf(1), math.NaN()} {
		fmt.Printf("%x %X\n", v, v)
	}
	fmt.Printf("%.3x %.0x %+x % x\n", 255.0, 255.0, 1.5, 1.5)
	var f32 float32 = 255
	fmt.Printf("%x | %8.2x|\n", f32, 1.0)
}
