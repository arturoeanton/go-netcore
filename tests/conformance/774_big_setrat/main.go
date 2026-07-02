package main

import (
	"fmt"
	"math/big"
)

func main() {
	r := big.NewRat(22, 7)
	f := new(big.Float).SetRat(r)
	fmt.Printf("setrat: %.6f\n", f)

	r2 := big.NewRat(-3, 4)
	f2 := new(big.Float).SetRat(r2)
	fmt.Printf("neg: %.4f\n", f2)
}
