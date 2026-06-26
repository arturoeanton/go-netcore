package main

import (
	"fmt"
	"math"
	"math/cmplx"
)

// math/cmplx transcendental functions (Exp/Rect/Pow/Sin/Cos/Tan/Sinh/Cosh/Tanh/
// Cot and the inverse trig). These are faithful ports of Go's cmplx package; the
// values are computed from the real Sin/Cos/Exp/Log/Asin/Atanh primitives, which
// match Go to within the last ULP on this platform — so transcendental results
// are printed at a safe precision. The special cases (0/±Inf/NaN, sign
// conventions, the x==0 Pow table) are exact and printed in full.
func main() {
	zs := []complex128{complex(3, 4), complex(1, 1), complex(-2, 0.5), complex(0.5, -0.3), complex(2, 2), complex(-1.5, -0.7)}
	for _, z := range zs {
		fmt.Printf("Exp=%.10g Sin=%.10g Cos=%.10g Tan=%.10g Cot=%.10g\n",
			cmplx.Exp(z), cmplx.Sin(z), cmplx.Cos(z), cmplx.Tan(z), cmplx.Cot(z))
		fmt.Printf("Sinh=%.10g Cosh=%.10g Tanh=%.10g\n", cmplx.Sinh(z), cmplx.Cosh(z), cmplx.Tanh(z))
		fmt.Printf("Asin=%.10g Acos=%.10g Atan=%.10g\n", cmplx.Asin(z), cmplx.Acos(z), cmplx.Atan(z))
		fmt.Printf("Asinh=%.10g Acosh=%.10g Atanh=%.10g\n", cmplx.Asinh(z), cmplx.Acosh(z), cmplx.Atanh(z))
		fmt.Printf("Pow=%.10g Rect=%.10g\n", cmplx.Pow(z, complex(2, 1)), cmplx.Rect(cmplx.Abs(z), cmplx.Phase(z)))
	}

	// Round trips (printed rounded — they chain transcendentals).
	z := complex(0.7, 0.4)
	fmt.Printf("log(exp)=%.10g\n", cmplx.Log(cmplx.Exp(z)))
	fmt.Printf("sin(asin)=%.10g\n", cmplx.Sin(cmplx.Asin(z)))

	// Special / exact cases — printed in full.
	inf := math.Inf(1)
	fmt.Println(cmplx.Sin(complex(0, inf)))
	fmt.Println(cmplx.Cos(complex(inf, 0)))
	fmt.Println(cmplx.Cosh(complex(inf, 2)))
	fmt.Println(cmplx.Tanh(complex(inf, 1)))
	fmt.Println(cmplx.Tan(complex(2, inf)))
	fmt.Println(cmplx.Asin(complex(inf, 1)))
	fmt.Println(cmplx.Acosh(complex(0, 0)))
	fmt.Println(cmplx.Pow(complex(0, 0), complex(0, 0)))
	fmt.Println(cmplx.Pow(complex(0, 0), complex(2, 0)))
	fmt.Println(cmplx.Pow(complex(0, 0), complex(-1, 0)))
	fmt.Println(cmplx.Pow(complex(0, 0), complex(-1, 1)))
	fmt.Println(cmplx.IsNaN(cmplx.Pow(complex(0, 0), cmplx.NaN())))
	fmt.Println(cmplx.IsNaN(cmplx.Sin(cmplx.NaN())), cmplx.IsInf(cmplx.Exp(complex(inf, 0))))
}
