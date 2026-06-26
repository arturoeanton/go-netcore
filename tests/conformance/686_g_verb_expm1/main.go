package main

import (
	"fmt"
	"math"
)

// Two byte-exact fixes:
//   - %.<prec>g of a small-magnitude value kept the exponent sign (e-06), not flipping it
//     to e+06 (the explicit-precision %g path dropped the sign).
//   - math.Expm1 / math.Log1p use Go's fdlibm algorithm, so they stay accurate for small
//     |x| where a naive Exp(x)-1 / Log(1+x) loses precision to cancellation.
func main() {
	for _, v := range []float64{2.65e-06, 1.5e-10, 1e-7, 4.567e-20, 1e6, 1.23e15, 0.0032} {
		fmt.Printf("%.15g | %.3g | %g\n", v, v, v)
	}
	for _, x := range []float64{1e-10, 1e-5, 0.1, 0.5, 1, 2, 5, -0.5, -1e-8, 700} {
		fmt.Printf("expm1(%g)=%.17g log1p(%g)=%.17g\n", x, math.Expm1(x), x, math.Log1p(x))
	}
	fmt.Println(math.Expm1(0), math.Log1p(0), math.Log1p(-1), math.Expm1(math.Inf(-1)))
}
