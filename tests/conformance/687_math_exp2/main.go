package main

import (
	"fmt"
	"math"
)

// math.Exp2 now uses Go's dedicated algorithm (argument reduction + the shared expmulti
// tail) rather than Pow(2, x), which differed at the last ULP (e.g. Exp2(0.5)=√2). A batch
// of fdlibm-ported math functions are also exercised for byte-exact regression.
func main() {
	for _, x := range []float64{0.5, 1, 2, 0.1, -0.5, 3.7, 10, -10, 0, 100, -1000, 0.333333} {
		fmt.Printf("%.17g\n", math.Exp2(x))
	}
	fmt.Println(math.Exp2(math.Inf(1)), math.Exp2(math.Inf(-1)), math.IsNaN(math.Exp2(math.NaN())))

	// other ported math fns (within byte-exact range)
	fmt.Printf("%.17g %.17g\n", math.Expm1(1e-10), math.Log1p(1e-10))
	fmt.Printf("%.17g %.17g\n", math.Erf(0.5), math.Erfinv(0.5))
	fmt.Printf("%.17g\n", math.Gamma(0.5))
	lg, sign := math.Lgamma(0.5)
	fmt.Printf("%.17g %d\n", lg, sign)
}
