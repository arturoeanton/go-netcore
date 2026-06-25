package main

import (
	"fmt"
	"math/big"
)

// fmt's integer verbs on a *big.Int (which implements fmt.Formatter): %d/%b/%o/%x/%X format
// the arbitrary-precision value — beyond int64's range — honoring the #, +, space, width,
// zero-pad and left-align flags exactly like a built-in integer.
func main() {
	big1 := new(big.Int)
	big1.SetString("123456789012345678901234567890", 10)
	fmt.Printf("%d\n%x\n%X\n%o\n%b\n", big1, big1, big1, big1, big1)

	neg := new(big.Int).Neg(big1)
	fmt.Printf("%d %x\n", neg, neg)

	fmt.Printf("%#x %#X %#o %+d % d\n", big.NewInt(255), big.NewInt(255), big.NewInt(64), big.NewInt(5), big.NewInt(5))
	fmt.Printf("%08d|%8x|%-8d|\n", big.NewInt(42), big.NewInt(255), big.NewInt(7))
	fmt.Printf("%05d|\n", big.NewInt(-42))

	fmt.Println(big.NewInt(0))
	fmt.Printf("%x %d %v\n", big.NewInt(0), big.NewInt(0), big.NewInt(0))

	fmt.Printf("%d\n", new(big.Int).Exp(big.NewInt(2), big.NewInt(128), nil))

	xs := []*big.Int{big.NewInt(1), big.NewInt(255), big.NewInt(-16)}
	fmt.Printf("%d %x\n", xs, xs)
}
