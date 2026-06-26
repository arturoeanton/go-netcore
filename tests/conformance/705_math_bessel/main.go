package main

import (
	"fmt"
	"math"
)

// Bessel functions J0/J1/Y0/Y1/Jn/Yn (fdlibm port). J0/J1 for |x| < 2 are a
// pure-polynomial evaluation and byte-exact with Go (printed at full precision).
// Y0/Y1 (which call Log) and any |x| >= 2 branch (which call Sin/Cos) inherit
// goclr's platform-trig/log rounding, differing from Go in the last ULP exactly
// as math.Sin/Cos/Log already do — those are printed at a safe precision.
// Special cases (0/Inf/NaN, order/sign relations) are exact regardless.
func main() {
	// J0/J1, |x| < 2: exact, full precision (pure polynomial).
	for _, x := range []float64{0.0001, 0.25, 0.5, 1, 1.5, 1.9999} {
		fmt.Printf("x=%g J0=%.17g J1=%.17g\n", x, math.J0(x), math.J1(x))
	}
	// J0 is even, J1 is odd — exact for |x| < 2.
	fmt.Printf("%.17g %.17g\n", math.J0(-1.3), math.J0(1.3))
	fmt.Printf("%.17g %.17g\n", math.J1(-1.3), math.J1(1.3))
	// Jn reduces to J0/J1 for n=0,1 — same code path, exact for |x| < 2.
	fmt.Printf("%.17g %.17g\n", math.Jn(0, 1.5), math.J0(1.5))
	fmt.Printf("%.17g %.17g\n", math.Jn(1, 1.5), math.J1(1.5))
	fmt.Printf("%.17g %.17g\n", math.Jn(-1, 1.5), -math.J1(1.5))
	// Tiny-argument Jn Taylor branch is exact.
	fmt.Printf("Jn(5,1e-3)=%.17g Jn(20,1e-3)=%.17g Jn(40,1e-3)=%.17g\n", math.Jn(5, 1e-3), math.Jn(20, 1e-3), math.Jn(40, 1e-3))

	// Y0/Y1 and |x| >= 2: correct to ~15 digits; print rounded so rows are stable.
	for _, x := range []float64{0.5, 1.5, 2, 3.5, 5, 10, 100, 1000} {
		fmt.Printf("x=%g J0=%.10g J1=%.10g Y0=%.10g Y1=%.10g\n", x, math.J0(x), math.J1(x), math.Y0(x), math.Y1(x))
	}
	for n := 2; n <= 6; n++ {
		fmt.Printf("Jn(%d,3)=%.10g Yn(%d,3)=%.10g Jn(-%d,3)=%.10g Yn(-%d,3)=%.10g\n",
			n, math.Jn(n, 3), n, math.Yn(n, 3), n, math.Jn(-n, 3), n, math.Yn(-n, 3))
	}
	fmt.Printf("Jn(10,1)=%.10g Jn(15,20)=%.10g Yn(7,3)=%.10g\n", math.Jn(10, 1), math.Jn(15, 20), math.Yn(7, 3))

	// Special cases — all exact.
	fmt.Println(math.J0(0), math.J1(0), math.J0(math.Inf(1)), math.J1(math.Inf(-1)))
	fmt.Println(math.Y0(0), math.Y1(0), math.Y0(math.Inf(1)))
	fmt.Println(math.IsNaN(math.Y0(-1)), math.IsNaN(math.Y1(-2)), math.IsNaN(math.Yn(3, -1)))
	fmt.Println(math.Jn(2, 0), math.Jn(-3, 0))
	fmt.Println(math.Yn(2, 0), math.Yn(-2, 0), math.Yn(-3, 0))
	fmt.Println(math.IsNaN(math.J0(math.NaN())), math.IsNaN(math.Jn(4, math.NaN())), math.IsNaN(math.Yn(2, math.NaN())))
}
