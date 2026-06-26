package main

import (
	"fmt"
	"math/big"
)

// big.Float arithmetic and setters. goclr backs big.Float with a float64, so results match
// Go for the common case where the values and default precision fit in float64 (precision
// beyond 53 bits is a documented gap). Previously Add/Mul/Quo/Neg/SetPrec/etc. were
// unregistered and failed to compile.
func main() {
	a := big.NewFloat(1.5)
	b := big.NewFloat(0.25)
	fmt.Println(new(big.Float).Add(a, b).Text('f', 4))
	fmt.Println(new(big.Float).Mul(a, b).Text('f', 4))
	fmt.Println(new(big.Float).Sub(a, b).Text('f', 4))
	fmt.Println(new(big.Float).Quo(a, b).Text('f', 4))
	fmt.Println(new(big.Float).Neg(a).Text('f', 2))
	fmt.Println(new(big.Float).Abs(big.NewFloat(-9.5)).Text('f', 2))

	// SetPrec is accepted (no-op beyond float64); a within-precision result is exact.
	f := new(big.Float).SetPrec(64)
	f.SetFloat64(3.14159265358979)
	fmt.Println(f.Text('f', 10))
	fmt.Println(f.Prec() >= 53)

	// Set / Copy / SetInt64 / Float64
	g := new(big.Float).Set(a)
	fmt.Println(g.Text('f', 2))
	fmt.Println(new(big.Float).SetInt64(42).Text('f', 0))
	v, _ := a.Float64()
	fmt.Println(v)
	fmt.Println(a.Cmp(b), a.Sign(), a.IsInt(), b.IsInt())
}
