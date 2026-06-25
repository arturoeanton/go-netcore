package main

import (
	"fmt"
	"math/big"
)

// big.Float (double-backed in goclr) formatting. String() == Text('g', 10); Text(fmt, prec)
// maps onto strconv.FormatFloat (prec<0 = shortest); and fmt's %v/%g/%e/%f/%G verbs format
// the value (big.Float implements fmt.Formatter). For big.NewFloat(float64) — which stores
// the exact float64 — all of this is byte-exact with Go.
func main() {
	for _, x := range []float64{3.14159265358979, 255, 0.1, 1234567.89, 2.5, 100, 1e20, 0} {
		f := big.NewFloat(x)
		fmt.Printf("String=%q g10=%q gm1=%q\n", f.String(), f.Text('g', 10), f.Text('g', -1))
	}

	f := big.NewFloat(3.14159)
	fmt.Println(f.Text('f', 4), f.Text('e', 6), f.Text('g', 3))
	fmt.Println(big.NewFloat(255.5).Text('f', 2), big.NewFloat(0.1).Text('f', 10))

	fmt.Printf("v=%v g=%g e=%e f=%f .2f=%.2f\n", f, f, f, f, f)
	fmt.Println(f)
	fmt.Println(big.NewFloat(255), big.NewFloat(0.1), big.NewFloat(1e20))
	fmt.Printf("%.4f %8.2f|\n", big.NewFloat(2.5), big.NewFloat(1.0))
}
