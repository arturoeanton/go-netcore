package main

import (
	"fmt"
	"math"
	"strconv"
)

// strconv.FormatFloat with the 'b' verb: the exact value as mantissa × 2^exp, printed
// "dddp±ddd" with no rounding (the full significand as an integer, exp = unbiasedExp -
// mantbits). NaN/±Inf format like every other verb; -0 keeps its sign. Covers float64 and
// float32, normals, denormals, and the smallest/largest magnitudes.
func main() {
	vals := []float64{
		255, 0, 1, -1, 0.5, 3.14159, 1e308, 5e-324,
		math.Inf(1), math.Inf(-1), math.NaN(), math.Copysign(0, -1),
		1.0 / 3.0, 1024, 2, math.MaxFloat64, math.SmallestNonzeroFloat64,
	}
	for _, v := range vals {
		fmt.Println(strconv.FormatFloat(v, 'b', -1, 64))
	}

	fmt.Println("--- 32 ---")
	for _, v := range []float64{255, 1, 0.5, 3.14, math.MaxFloat32, 1e-40, -2} {
		fmt.Println(strconv.FormatFloat(v, 'b', -1, 32))
	}

	// AppendFloat routes through the same path.
	fmt.Printf("%q\n", string(strconv.AppendFloat([]byte("v="), 255, 'b', -1, 64)))
}
