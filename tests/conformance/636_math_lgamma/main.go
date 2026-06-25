package main

import (
	"fmt"
	"math"
)

// math.Lgamma(x) returns (ln|Γ(x)|, sign of Γ(x)). It is now registered (a faithful
// port of Go's lgamma) — previously it was unsupported. Like the other math
// transcendentals it inherits the platform log's last-ULP for a few inputs, so this
// covers the byte-exact spread (positive, negative half-integers, small and large x).
func main() {
	for _, x := range []float64{5, 0.5, 1, 2, 3, 4, 10, 100, 0.1, 0.2, 0.3, 1.5, 2.5, 3.5, 7, 50, 1000, -0.5, -1.5, -2.5, 0.001, 0.005, 0.02, 0.05, 0.15} {
		lg, sign := math.Lgamma(x)
		fmt.Println(x, lg, sign)
	}

	// Γ(0) is +Inf; Lgamma(+Inf) is +Inf
	l0, s0 := math.Lgamma(0)
	fmt.Println(math.IsInf(l0, 1), s0)
	lp, _ := math.Lgamma(math.Inf(1))
	fmt.Println(math.IsInf(lp, 1))

	// ln(n!) via Lgamma(n+1) for small n
	for n := 1; n <= 6; n++ {
		lg, _ := math.Lgamma(float64(n + 1))
		fmt.Printf("%.10f ", lg)
	}
	fmt.Println()
}
